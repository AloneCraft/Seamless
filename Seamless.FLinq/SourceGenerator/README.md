# FLinq SourceGenerator Notes

このドキュメントは、以前 `interceptor.cs` 冒頭に置かれていた仕様メモ、TODO、編集方針を退避したものです。
実装本体は `FLinqIncrementalGenerator` の partial ファイルへ責務別に分割しています。

2026/05/23
# interceptor.cs の仕様まとめ

`FLinqIncrementalGenerator`（`IIncrementalGenerator` 実装）の主要な仕様を以下にまとめます。

---

## 1. 全体的な役割

**「`System.Linq.Enumerable` を呼び出しているメソッド」をコンパイル時にインターセプトし、LINQ チェーンをアロケーションゼロ・delegate呼び出しなしのインライン展開コードに置き換えるソースジェネレーター。**

---

## 2. インクリメンタルジェネレーターの初期化（`Initialize`）

2つのパイプラインを持つ。

| パイプライン | 役割 |
|---|---|
| `interceptTargets` | `InvocationExpressionSyntax` を検出し、LINQ を含むメソッドをインターセプト対象化 |
| `typeTargets` | `TypeDeclarationSyntax` を検出し、DBスキーマ等の型情報を生成 |

- `InterceptsLocationAttribute` の定義を `PostInitializationOutput` で必ず出力する。
- インターセプト先は呼び出し元ソースファイル単位でグループ化し、`{FileName}.g.cs` として出力。

---

## 3. インターセプト対象の選定条件（`TryCreateLocalMethodInterceptionTarget`）

以下を **全て満たす** メソッド呼び出しが対象。

### 包含するメソッドの条件
| 条件 | 理由 |
|---|---|
| `MethodKind.Ordinary` のみ | partial/ctor/operator は対象外 |
| 非ジェネリックメソッド | 型引数再構成が未整備 |
| 非ジェネリック含有型 | 型パラメータ漏れを防ぐ |
| `abstract`/`virtual`/`override` のインスタンスメソッドは除外 | 動的ディスパッチを壊さないため |
| `private`/`protected` な入れ子型を含まない | 生成コードから参照できないため |
| `ref struct` でない | 寿命制約 |
| 値型のインスタンスメソッドは除外 | receiver コピー差異 |
| ソース構文がある | LINQ 有無を判定するため |
| 戻り値・引数に匿名型を含まない | 生成署名に出せない |
| 戻り値・引数に型パラメータを含まない | 生成側で閉じられない |
| 引数に `ref`/`out`/`in` を含まない | aliasing 破壊を防ぐ |
| **メソッド内部に `System.Linq.Enumerable` 呼び出しがある** | 最重要条件 |

> ⚠️ 「Fragment に落とせるかどうか」でここでは絞らない。未対応は後段で例外にして見える化する。

---

## 4. Fragment の仕組み（`フラグメント候補一覧構築`）

- `Fragments/` フォルダ下の `Fragment` 継承クラスをリフレクションで自動列挙。
- 型名の命名規則: `{MethodName}_{VariantName}` 形式
  - 例: `Aggregate_func`, `Where_indexPredicate`, `Join`
- コンストラクタの第1引数は必ず `int`（一意な番号）
- パラメータ数の多い（より特化した）オーバーロードを優先して試行
- 新しい Fragment クラスを追加するだけで辞書が自動更新される

---

## 5. Fragment の選択・生成フロー

`呼び出し式からフラグメント生成` は **拡張メソッド呼び出し**（`source.Method(...)`）のみを対象とする。
静的呼び出し（`SystemEnumerable.Method(source, ...)`）は `呼び出しソース式取得` が false を返し、
`列挙式フラグメント生成` が `単独列挙ソースフラグメント生成` にフォールバックして不透明な Root として扱う。

```
呼び出し式からフラグメント生成()  ※拡張メソッド呼び出しのみ
  ├─ 呼び出しソース式取得() → source を抽出（ReducedFrom が null なら false を返す）
  ├─ Any → Any述語フラグメント生成()（Where + Any に変換）
  ├─ SelectMany のインライン化可能ならSelectManyインライン化を試行()
  └─ その他 → 登録済みフラグメント生成()
                 └─ フラグメント候補一覧から MethodName で候補を検索
                    └─ 各候補を試行（登録済みフラグメント生成を試行）

列挙式フラグメント生成()
  ├─ invocation かつ 呼び出し式からフラグメント生成 が成功 → Enumerable fragment
  └─ それ以外（静的LINQ呼び出し含む） → 単独列挙ソースフラグメント生成()（不透明な Root として扱う）
```

例:
- `source.Where(...).ToList()` → `.Where()` `.ToList()` ともに struct 展開
- `Enumerable.Select(source, ...).Where(...).ToList()` → `Enumerable.Select(...)` は不透明 Root、`.Where()` `.ToList()` を struct 展開
- `Enumerable.Select(source, ...).ToList()` → `Enumerable.Select(...)` は不透明 Root、`.ToList()` を struct 展開

---

## 6. ラムダ式 / FuncFragment の処理（`関数式フラグメント生成`）

| 種類 | 処理 |
|---|---|
| `static` ラムダ / キャプチャなしラムダ | 補助 `static` メソッドとして生成（`CanGenerateMethod=true`） |
| キャプチャありラムダ | `struct` primary constructor にキャプチャ変数を閉じ込めてインライン代入のみ許可 |
| ラムダ内に LINQ がある | `InlinedScalarFunc` として再帰的に Fragment 化 |
| `delegate` ローカル変数 | 宣言時 initializer がラムダの場合のみインライン化 |
| delegate 変数・メソッドグループ | **フォールバック生成しない（例外）** |
| `AnonymousMethodExpression`（`delegate {}` 形式） | 単一 `return` 文のみ対応 |

---

## 7. コード生成ループ（`文を生成コードへ変換`）

対応する文の種類：
`Block`, `Try`, `Using`, `LocalDeclaration`, `ExpressionStatement`, `For`, `ForEach`, `While`, `Do`, `If`, `Switch`、それ以外

LINQ 呼び出しを含む文に対しては `LINQ呼び出し差し替え文生成` が走り：
1. 文の中の **拡張メソッド** LINQ 呼び出し式を抽出（静的呼び出し・ラムダ内部・ネストした LINQ は除外）
2. 各呼び出しを Fragment で展開し、`__linq0`, `__linq1`... などのローカル変数に一時保存
3. 元の文のテキストを一時変数名で置換して出力

---

## 8. Enumerator の実行方式（`フラグメント実行文追加`）

| 条件 | 出力コード |
|---|---|
| `SupportsFlatLoop == true`（かつ `Tsavorite_ENUMERATOR_MODE` 未定義） | フラットループ（`GetFlatLoopCode`）展開 |
| それ以外 | `using var e = fragment.GetEnumeratorExpression; while(e.TryGetNext(out var c)) {...}` |

---

## 9. プライベートメンバーへのアクセス（UnsafeAccessor）

生成コードは元コードと別 namespace に置かれるため、非 `public` メンバーへのアクセスには `UnsafeAccessor` を自動生成する。

| 対象 | `UnsafeAccessorKind` |
|---|---|
| 非 public インスタンスフィールド | `Field` |
| 非 public 静的フィールド | `StaticField` |
| 非 public インスタンスメソッド | `Method` |
| 非 public 静的メソッド | `StaticMethod` |
| 非 public インスタンスプロパティ getter | `Method` (`get_X`) |

---

## 10. 匿名型の取り扱い（Tsavorite001 警告）

- LINQ 呼び出しに匿名型が含まれる場合、完全なフラグメント最適化は不可。
- 代わりに型パラメータ化した汎用ヘルパーメソッドを生成し `IEnumerable<T>` 経由で System.Linq.Enumerable を再呼び出しする部分最適化に切り替える。
- 同時に `Tsavorite001` 診断警告を発行し、ValueTuple または struct record 使用を促す。

---

## 11. 補助クラスへの退避（衝突回避）

- 生成した `Struct0`, `Struct1`, ... などの補助型は、メソッド単位の補助クラス `{MethodName}_補助0_Struct0` 等へリネームして衝突を避ける。
- 補助クラス内の `Compute`/`Build` で始まるメソッドには `AggressiveInlining` 属性を自動付与。

---

## 12. 失敗時の挙動（フォールバックなし）

インターセプト対象だが展開に失敗した場合、**静かに元実装へ戻す経路を作らず**、全て例外（`GeneratedCodeHelpers.LinqInterceptorFailure`）を throw するコードを生成して、未対応・展開失敗を明示する。

---

## 13. 生成コードの名前空間

元の型の namespace が `Foo.Bar` なら、生成インターセプタは `Intercept.Foo.Bar` に配置される。
    Join_keySelectors_resultSelector_InstanceImplでSGで生成したコードにエラーが出る。
    これはresultSelectorという引数と一致するキャプチャ変数があるからである。this.で参照することで回避できるだろう。
    Fragments
        innkeyKeySelector,resultKeySelector,outerKeySelectorもインライン化して
            Join.GetEnumeratorConstructorの中にデリゲートのまま使っているがインライン展開できる時はそうして。他のメソッドでもやっていればインライン化して
    Fragmentsフォルダ以下のクラスの全メンバーの意味をコメントに書いて
    FirstOrDefault.csには3つのクラスがある。LastOrDefault.csには1つのクラスしかない。System.Linq.Enumerableメソッドは対照的なメソッドがあるはずなので3つ定義するようにして
        コードを別のファイルに分離するのは違うメソッド名のフラグメントである
        同じオーバーロードなら同じファイルに入れて
    EnumerableMethod1.cs
        定義していないlinqメソッドがいくつかある。例えばUnion,Exept,ExceptBy,Intersect,IntersectBy。他にも色々ある。全てのテストコードを書いて
            Fragmentで展開できるかできないかは考えなくていい。エラーになればFragmentを実装していないということだからそれから実装すればいい
        テストが通らないものはFragmentを実装ていないので実装して。
        Fragmentsフォルダ以下の1つのファイルに複数のクラスが定義して居るものがある。同じメソッド名と同じファイル名にする。異なるオーバーロードは同じファイルに残すようにして。
        UnionやSelectManyも別オーバーロードになって居るのをSelectManyメソッド1つでImplメソッドを呼び出すようにして。キャプチャ変数ありとなしの別メソッド呼び出すようにして
    コード生成で完全修飾名刺なくてもいいようにusingしてそれ前提のコードにして
        例えばglobal::System.Colletion.Generic.ListではなくListだけでやれるように
        コード生成は注意して書けばほぼ衝突しないはず
    Fragment継承クラスで変更してもらうこと
        以下はJoinの例である
            global::System.Collections.Generic.IEnumerable<{{outer.ElementType}}> outer,
            global::System.Collections.Generic.IEnumerable<{{inner.ElementType}}> inner,
        このようにIEnumerableを使っているがouterの具体的なIEnumerable継承クラスを指定して
        同じようにIEnumeratorも具象型、値型ならそのまま使うようにして
        型パラメータは匿名型が入ってくることを考えて維持したほうが良い
このファイルをAIに編集してもらうときの指示
	コード生成方法
		Fragmentsフォルダの下のクラスが各メソッドをためのコード片になる。ここにコード生成のルールのほとんどを書く。interceptor.csに特殊処理はできるだけ書かない。
		linqメソッドでIEnumerable<T>を返すメソッドはstruct Enumeratorを実装してコンストラクタ、MoveNext,Resetをコード片から作る
	インターセプターするべきもの
		linqメソッドを使っているメソッドX全体をインターセプトする。インターセプト先メソッドYを生成する。
		Xを呼んでいる部分をYを呼ぶようにする。
		仮想関数呼び出しならそのままにしておく
	インターセプト先メソッドの変更点
		メソッドX内部のlinqメソッドチェーンをループも含めてインライン展開する
		キャプチャ変数もメソッドX内部全てをインライン展開することでラムダ式を非デリゲート化する。
		ラムダ式ではないデリゲートはデリゲート呼び出しのまま
		インライン展開方法はFragments以下のクラスを使いメソッドに対応するコード片から組み立てる
		メソッドチェーンの末尾メソッドで処理するメソッドを生成する。
		末尾以外のメソッドチェーンはキャリアメソッドにより末尾にデータを送る。生成するコードは構文エラーや足りないものはあるが概ね以下のような意図である
			outer.Join(inner,o=>o-1,i=>i+i,(o,i)=>o+i).Where(q=>q%2==0)ならば
			struct Enumerator<TOuter,TInner,TKey,TResult,TOuterEnumerator,TInnerEnumerator>where struct,TOuterEnumerator<TOuter>where struct,TInnerEnumerator<TInner>{
				TOuterEnumerator OuterEnumerator:
				TInnerEnumerator InnerEnumerator
				public Enumerator(TOuterEnumerator OuterEnumerator,TInnerEnumerator InnerEnumerator){
					this.OuterEnumerator=OuterEnumerator;
					this.InnerEnumerator=InnerEnumerator;
					this.State=0;
				}
				public int State;
				public TResult Current{get;private set;}
				private
				public bool MoveNext(Join<TOuter,TInner,TKey,TResult>source,Func<TResult,bool>predicate){
					switch(state){
					case 0:{
						goto Start;
						Head:
                        var i=InnerEnumerator.Current;
						d.Add(i+i);
						Start:
						if(InnerEnumerator.MoveNext())goto Head;
						goto Start;
							goto case 1;
					}
					case 1:{
						if(OuterEnumerator.MoveNext()){
                            var o=OuterEnumerator.Current;
							if(d.TryGetValue(o-1,out var r)){
                                var(o0,i0)=(OuterEnumerator.Current,InnerEnumerator.Current);
                                var Current=o0+i0;
								if(!predicate(Current))goto Skip2;
								this.Current=Current;
								return true;
							}
						}
						return false;
					}
						return false;
				}
			}
			Enumerable<TResult>WhereInline(Join<TOuter,TInner,TKey,TResult>source,Func<TResult,bool>predicate){
				return new Enumerable<TResult>(new Enumerator<TOuter,TInner,TKey,TResult>(source.outer.GetEnumerator(),source.inner.GetEnumerator()));
			}
	インターセプター先コード生成でやるべきこと
        インターセプトメソッド内部にデリゲートがあった場合
            linqメソッドの引数であってもなくてもデリゲート内部にlinqメソッドがあったらそれをインライン化する
		privateメンバーにアクセスする場合があるのでUnsafeAccessorを使ってアクセスする
		全てはFragment継承クラスで定義してあるものに従い、interceptor.csで例外的な処理は入れない。
		EnumeratorベースのFragmentでは、static/capturing lambdaを安易にFunc delegateとしてprimary constructorに渡さない。
		Where/Select/TakeWhile/SkipWhileなどのループ展開で式として安全に展開できるLambdaFuncは、
		TryGetNext内へFuncInvoke/FuncAssignで直接埋め込み、生成g.cs上でもselector(item)ではなくitem * 2のような元ラムダ式が見える形を優先する。
		GeneratedMethodFragments的なSystem.Linq.Enumerable.Xxx(...)再呼び出し経路は追加しない。
		LINQメソッドごとに専用Fragmentを作り、enumeratorの状態遷移として意味を表現する。
        /// <summary>コメントタグで <see cref="MyNamespace.AdvancedProcessor"/> を参照してください。または、特定のメソッド <see cref="ProcessData(int)"/> へジャンプします。</summary>というインターセプト元へのリンクを書いて
    thin orchestrator 不変条件（litmus test）
        interceptor.cs のメソッドが「LINQ 演算子名で分岐して C# ソース文字列を builder.AppendLine する」なら、それはコード片であり Fragment 側の責務。interceptor.cs には置かない。
        逆に「構文/意味モデル → Fragment 構築（PipelineStage/Fragment 選択）」だけを行うものは orchestration として許容（例: PipelineBuilder.TryAppendStage の method 名 switch は new PipelineStage(...) という"データ"を作るだけ）。
        正典例: expr.Consume(consumer) の展開。以前は interceptor 側に Reverse/Where/Select/Index/Append/Prepend ごとの span ループ生成（ReverseConsumeループ文生成… 等）が並んでいたが、
                Enumerable.GetConsumeExecutionStatement（TraversalSupport で span fast path 込み）へ一本化し、直接Consumeループ文生成 は演算子非依存の薄いディスパッチャになった。
        既知の残routing（thin adapter。コード片は Fragment 側 = Any.cs / SelectManyBase / AnonymousExitToList にある）:
            methodName=="Any"（Any.TryBuildWithPredicate）/ "SelectMany"（SelectManyBase.TryBuildInlined + 匿名型出口 Select）/ "ToList"（AnonymousExitToList）。
            いずれも匿名型レジストリ(Pin/Suspend)に強く結合しているため、registry 一本化する場合は匿名型機構ごと慎重に。