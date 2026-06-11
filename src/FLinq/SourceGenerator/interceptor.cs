/*
2026/05/23
# interceptor.cs の仕様まとめ

`TsavoriteIncrementalGenerator`（`IIncrementalGenerator` 実装）の主要な仕様を以下にまとめます。

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

`呼び出し式フラグメント生成1` は **拡張メソッド呼び出し**（`source.Method(...)`）のみを対象とする。
静的呼び出し（`SystemEnumerable.Method(source, ...)`）は `呼び出しソース式取得51` が false を返し、
`列挙フラグメント生成9` が `単独列挙フラグメント生成10` にフォールバックして不透明な Root として扱う。

```
呼び出し式フラグメント生成1()  ※拡張メソッド呼び出しのみ
  ├─ 呼び出しソース式取得51() → source を抽出（ReducedFrom が null なら false を返す）
  ├─ Any → Anyフラグメント生成3()（Where + Any に変換）
  ├─ SelectMany のインライン化可能ならSelectManyインライン化試行50()
  └─ その他 → 登録済みフラグメント生成24()
                 └─ フラグメント候補一覧から MethodName で候補を検索
                    └─ 各候補を試行（登録済みフラグメント生成試行26）

列挙フラグメント生成9()
  ├─ invocation かつ 呼び出し式フラグメント生成1 が成功 → Enumerable fragment
  └─ それ以外（静的LINQ呼び出し含む） → 単独列挙フラグメント生成10()（不透明な Root として扱う）
```

例:
- `source.Where(...).ToList()` → `.Where()` `.ToList()` ともに struct 展開
- `Enumerable.Select(source, ...).Where(...).ToList()` → `Enumerable.Select(...)` は不透明 Root、`.Where()` `.ToList()` を struct 展開
- `Enumerable.Select(source, ...).ToList()` → `Enumerable.Select(...)` は不透明 Root、`.ToList()` を struct 展開

---

## 6. ラムダ式 / FuncFragment の処理（`関数フラグメント生成12`）

| 種類 | 処理 |
|---|---|
| `static` ラムダ / キャプチャなしラムダ | 補助 `static` メソッドとして生成（`CanGenerateMethod=true`） |
| キャプチャありラムダ | `struct` primary constructor にキャプチャ変数を閉じ込めてインライン代入のみ許可 |
| ラムダ内に LINQ がある | `InlinedScalarFunc` として再帰的に Fragment 化 |
| `delegate` ローカル変数 | 宣言時 initializer がラムダの場合のみインライン化 |
| delegate 変数・メソッドグループ | **フォールバック生成しない（例外）** |
| `AnonymousMethodExpression`（`delegate {}` 形式） | 単一 `return` 文のみ対応 |

---

## 7. コード生成ループ（`文生成16`）

対応する文の種類：
`Block`, `Try`, `Using`, `LocalDeclaration`, `ExpressionStatement`, `For`, `ForEach`, `While`, `Do`, `If`, `Switch`、それ以外

LINQ 呼び出しを含む文に対しては `LINQ呼び出し差し替え文生成38` が走り：
1. 文の中の **拡張メソッド** LINQ 呼び出し式を抽出（静的呼び出し・ラムダ内部・ネストした LINQ は除外）
2. 各呼び出しを Fragment で展開し、`__linq0`, `__linq1`... などのローカル変数に一時保存
3. 元の文のテキストを一時変数名で置換して出力

---

## 8. Enumerator の実行方式（`フラグメントEnumerator実行文追加37`）

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
                Enumerable.GetConsumeExecutionStatement（TraversalSupport で span fast path 込み）へ一本化し、直接Consumeループ文生成43 は演算子非依存の薄いディスパッチャになった。
        既知の残routing（thin adapter。コード片は Fragment 側 = Any.cs / SelectManyBase / AnonymousExitToList にある）:
            methodName=="Any"（Any.TryBuildWithPredicate）/ "SelectMany"（SelectManyBase.TryBuildInlined + 匿名型出口 Select）/ "ToList"（AnonymousExitToList）。
            いずれも匿名型レジストリ(Pin/Suspend)に強く結合しているため、registry 一本化する場合は匿名型機構ごと慎重に。
*/
// #define Tsavorite_ENUMERATOR_MODE  // コメントを外すと常に Enumerator struct 方式になる（フラットループ無効化）

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using static Tsavorite.SourceGenerator.GeneratorHelpers;

using Tsavorite.SourceGenerator.Linq.Fragments;
using Enumerable = Tsavorite.SourceGenerator.Linq.Fragments.Enumerable;

namespace Tsavorite.SourceGenerator;

[Generator]
public sealed partial class TsavoriteIncrementalGenerator : IIncrementalGenerator {
    private const string InterceptsLocationAttributeSource = """
        #nullable enable
        namespace System.Runtime.CompilerServices {
            [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]
            internal sealed class InterceptsLocationAttribute : global::System.Attribute {
                public InterceptsLocationAttribute(int version,string data) {
                    this.Version=version;
                    this.Data=data;
                }
                public int Version { get; }
                public string Data { get; }
            }
        }
        """;

    private sealed class フラグメント候補 {
        public required Type FragmentType { get; init; }
        public required ConstructorInfo Constructor { get; init; }
        public required string MethodName { get; init; }
        public required string VariantName { get; init; }
        public required ParameterInfo[] Parameters { get; init; }
        public Func<IMethodSymbol, bool>? IsApplicableOverload { get; init; }
    }

    private static readonly SymbolDisplayFormat 完全修飾形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
    private static readonly SymbolDisplayFormat cref形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
    // Fragments.Fragment を継承した具体型を列挙し、ローカルメソッド再生成にもそのまま使う。
    // これで Aggregate の overload 個別対応を増やすたびに辞書を手で更新しなくてよい。
    private static readonly IReadOnlyDictionary<string,ImmutableArray<フラグメント候補>> フラグメント候補一覧 = フラグメント候補一覧構築23();
    private static readonly IReadOnlySet<string> SystemLinqEnumerableMethodNames = typeof(global::System.Linq.Enumerable)
        .GetMethods(BindingFlags.Public|BindingFlags.Static)
        .Select(static method=>method.Name)
        .ToHashSet(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<Compilation,System.Collections.Concurrent.ConcurrentDictionary<IMethodSymbol,ローカルメソッドLinq解析結果>> ローカルメソッドLinq解析キャッシュ = new();

    // 匿名型を含む LINQ が検出されたとき、インターセプト元のメソッドに対して発行する警告。
    // 匿名型は生成メソッドの署名に出せないため完全なフラグメント最適化ができず、
    // 匿名型を含む LINQ 呼び出しのみ IEnumerable<T> 汎用ヘルパーへ委譲する部分最適化になる。
    // ValueTuple や struct record を使えば完全最適化が可能になることをユーザーに伝える。
    private static readonly DiagnosticDescriptor 匿名型警告Descriptor = new(
        id: "Tsavorite001",
        title: "匿名型が検出されました",
        messageFormat: "'{0}' に匿名型を含む LINQ 式が検出されました。最適化可能な部分だけインターセプトします。ValueTuple または struct record を使用することを検討してください。",
        category: "Tsavorite.Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "匿名型を含む LINQ 式は、生成メソッドの署名に匿名型を記述できないため完全なフラグメント最適化が適用できません。ValueTuple (例: (int x, int y)) または struct record を使うことで最適化が有効になります.");

    // LINQ インターセプト対象メソッドで展開に失敗し、生成コードが例外を throw する形になるとき発行する警告。
    // これにより「インターセプトはされたが展開できなかった」ことをコンパイル時に検出できる。
    // 警告が出ない = 全 LINQ 呼び出しがインライン展開されている ことを確認するために使う。
    private static readonly DiagnosticDescriptor 展開失敗警告Descriptor = new(
        id: "Tsavorite002",
        title: "LINQ インターセプト展開失敗",
        messageFormat: "'{0}' の LINQ 呼び出しをインライン展開できませんでした。実行時に例外が発生します。未対応メソッドまたはデリゲートパターンが含まれている可能性があります。",
        category: "Tsavorite.Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "対象メソッド内の LINQ 呼び出しをインライン展開できなかったため、生成されたインターセプターコードは実行時に例外を発生させます。Fragment 未実装のメソッド、または未対応のデリゲートパターンが含まれている可能性があります.");

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var interceptTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_)=>node is InvocationExpressionSyntax,
                static (syntaxContext,cancellationToken)=>Linq傍受対象生成(syntaxContext,cancellationToken))
            .Where(static target=>target is not null)
            .Select(static (target,_)=>target!);

        context.RegisterPostInitializationOutput(static productionContext=>{
            productionContext.AddSource("InterceptsLocationAttribute.g.cs",SourceText.From(InterceptsLocationAttributeSource,Encoding.UTF8));
        });

        // Tsavorite001 警告: グローバルな重複除去が必要なため Collect で一括処理（軽量）
        context.RegisterSourceOutput(interceptTargets.Collect(),static (productionContext,interceptTargets)=>{
            if(interceptTargets.IsDefaultOrEmpty) {
                return;
            }
            var reportedMethods = new HashSet<string>(StringComparer.Ordinal);
            foreach(var target in interceptTargets.Distinct(LinqInterceptTargetComparer.Instance)) {
                if(!target.LinqAnalysis.HasAnonymousTypeLinq) {
                    continue;
                }
                var methodKey = target.Method.ToDisplayString();
                if(!reportedMethods.Add(methodKey)) {
                    continue;
                }
                var location = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    匿名型警告Descriptor,
                    location,
                    target.Method.Name));
            }
        });

        // コード生成: ファイル単位で独立してキャッシュされる
        var perFileGroups = interceptTargets
            .Collect()
            .SelectMany(static (targets,_)=>targets
                .Distinct(LinqInterceptTargetComparer.Instance)
                .GroupBy(static t=>t.SourcePath,StringComparer.Ordinal)
                .Select(static g=>new PerFileInterceptGroup(g.Key,[..g])));

        context.RegisterSourceOutput(perFileGroups,static (productionContext,group)=>{
            var diagnostics = new List<Diagnostic>();
            var source = インターセプターソース構築(group.Targets,diagnostics);
            if(source.Length>0) {
                productionContext.AddSource(インターセプターHintName取得(group.SourcePath),SourceText.From(source,Encoding.UTF8));
            }
            foreach(var diagnostic in diagnostics) {
                productionContext.ReportDiagnostic(diagnostic);
            }
        });
    }

    private static Linq傍受対象型? Linq傍受対象生成(GeneratorSyntaxContext context,CancellationToken cancellationToken) {
        //呼び出しである必要がある
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }

        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }

        return TryCreateLocalMethodInterceptionTarget(context,invocation,method,cancellationToken,out var localMethodTarget)
            ? localMethodTarget
            : null;
    }

    private static bool TryCreateLocalMethodInterceptionTarget(
        GeneratorSyntaxContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        CancellationToken cancellationToken,
        out Linq傍受対象型? target) {
        target=null;

        // 呼び出し側で型引数が確定済みのジェネリックメソッド呼び出し（例: Foo<int>(...)）も、
        // 未構築の定義（OriginalDefinition）に正規化してから以降の判定・生成を行う。
        // 生成される補助メソッドは元の型パラメーター名（例: TAnonymous）を持つジェネリックメソッドとして
        // 宣言され、呼び出し側の型引数はインターセプター経由でコンパイラが解決する。
        if(method.IsGenericMethod) {
            method=method.OriginalDefinition;
        }

        // インターセプト対象にする主条件は 2 つ:
        // 1. その場所が
        // 1-1.通常の関数呼び出し
        // 1-2.sealed override関数呼び出し
        // 1-3.sealed class のoverride関数であること
        // 2. 呼び出し先メソッドの内部に System.Linq.Enumerable 呼び出しがあること
        // それ以外の条件は、生成したインターセプターがコンパイル不能になったり、
        // 仮想呼び出しの動的ディスパッチを壊したりしないための安全制約として残す。
        //
        // ここで「Fragment に落とせる LINQ だけ」を対象に絞らないのが重要。
        // System.Linq.Enumerable を含むメソッドはまず全部 intercept target にし、
        // その後段で Fragment が見つからない/組み立てられない場合に
        // 生成インターセプターから明示的な例外を返すことで、未対応ケースを隠さない。
        if(method.MethodKind!=MethodKind.Ordinary
            // Partial method/ctor/operator などは現在のローカルメソッド再構成対象外。
            || method.ContainingType is null
            // 所属型がないと interceptor 側で this/型名を再現できない。
            || method.ContainingType.IsGenericType
            // ジェネリック型メンバーは生成メソッド側へ型パラメータを漏らしやすいので除外。
            // （メソッド自身の型パラメーターのみ持つジェネリックメソッドは、冒頭で OriginalDefinition
            // に正規化済みなので通過する。呼び出し側で確定した型引数はインターセプター経由で解決される）
            || (!method.IsStatic && (method.IsAbstract || method.IsVirtual || method.IsOverride))
            // 仮想呼び出しを intercept すると動的ディスパッチを壊すのでそのまま残す。
            || ContainsGeneratedTypeAccessibilityRisk(method.ContainingType)
            // private/protected な入れ子型は生成コードから参照できずコンパイル不能になる。
            || method.ContainingType.IsRefLikeType
            // ref struct を interceptor 引数/receiver として扱う経路は未対応。
            || method.ContainingType.IsStatic
            // static クラスのメンバーは生成コードで型パラメータとして使用できないため除外。
            || (!method.IsStatic && method.ContainingType.IsValueType)
            // 値型 instance method は receiver のコピー/boxing 差異が大きいためまだ除外。
            || method.DeclaringSyntaxReferences.Length==0
            // ソース本体がないメソッドは「内部に LINQ があるか」を判定できない。
            || ContainsAnonymousType(method.ReturnType)
            // 匿名型戻り値は生成メソッドの署名に出せない。
            || ContainsGeneratedTypeAccessibilityRisk(method.ReturnType)
            // 非公開戻り値型も生成コードから見えない。
            || method.ReturnType.IsRefLikeType
            // ref-like 戻り値の forward/inline は寿命制約を満たしにくい。
            || ContainsExternalTypeParameter(method.ReturnType,method.TypeParameters)
            // 戻り値にメソッド外の型パラメータが残ると生成側の静的メソッドで閉じられない。
            // メソッド自身の型パラメーター（TAnonymous 等）は許可する。
            || method.Parameters.Any(static parameter=>parameter.RefKind!=RefKind.None)
            // ref/out/in は aliasing を壊しやすいのでいったん除外。
            || method.Parameters.Any(static parameter=>ContainsAnonymousType(parameter.Type))
            // 匿名型引数も署名へ出せない。
            || method.Parameters.Any(static parameter=>ContainsGeneratedTypeAccessibilityRisk(parameter.Type))
            // 非公開型引数は生成コードから参照できない。
            || method.Parameters.Any(static parameter=>parameter.Type.IsRefLikeType)
            // ref struct 引数は寿命制約を再現しにくい。
            || method.Parameters.Any(static parameter=>ContainsTypeParameter(parameter.Type))) {
            // 引数にメソッド自身のものも含め型パラメータが残ると、生成される補助 struct が
            // その型パラメータを宣言できず再生成対象外になる。引数は非ジェネリックのみ許可する。
            return false;
        }

        if(invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } containingMethodSyntax) {
            return false;
        }

        var containingMethod = context.SemanticModel.GetDeclaredSymbol(containingMethodSyntax,cancellationToken);
        if(containingMethod is null) {
            return false;
        }

        var linqAnalysis = ローカルメソッドLinq解析取得36(method,context.SemanticModel.Compilation);
        if(!linqAnalysis.HasEnumerableCall) {
            return false;
        }

        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return false;
        }

        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return false;
        }

        var invocationLineSpan = invocation.GetLocation().GetLineSpan();
        var line = invocationLineSpan.StartLinePosition.Line+1;
        var character = invocationLineSpan.StartLinePosition.Character+1;
        var identifier = SanitizeIdentifier($"{method.Name}_{System.IO.Path.GetFileNameWithoutExtension(invocationLineSpan.Path)}_{containingMethod.Name}_{line}_{character}");
        var parameterSpecs = method.Parameters
            .Select(static parameter=>new InterceptParameterSpec(parameter.Name,parameter.Type.ToDisplayString(完全修飾形式),parameter.RefKind))
            .ToArray();

        var methodTypeParameterNames = method.TypeParameters.IsDefaultOrEmpty
            ? null
            : method.TypeParameters.Select(static t=>t.Name).ToArray();

        target = new Linq傍受対象型(
            identifier,
            containingMethod.Name,
            interceptsLocationAttributeText,
            method,
            method,
            invocation,
            containingMethod.ContainingType,
            context.SemanticModel,
            parameterSpecs,
            linqAnalysis,
            methodTypeParameterNames: methodTypeParameterNames,
            isLocalMethodInterception: true);
        return true;
    }

    /// <summary>
    /// <see cref="ローカルメソッドLinq解析実行37"/> の結果を <see cref="Compilation"/> をキーにした
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> でキャッシュし、
    /// 同一 method の重複解析を防ぐ。
    /// キャッシュは <see cref="ConditionalWeakTable{TKey,TValue}"/> で Compilation ごとに管理されるため、
    /// インクリメンタルジェネレーターの世代をまたいでキャッシュが汚染されない。
    /// </summary>
    private static ローカルメソッドLinq解析結果 ローカルメソッドLinq解析取得36(IMethodSymbol method,Compilation compilation) {
        var cache = ローカルメソッドLinq解析キャッシュ.GetValue(
            compilation,
            static _=>new System.Collections.Concurrent.ConcurrentDictionary<IMethodSymbol,ローカルメソッドLinq解析結果>(SymbolEqualityComparer.Default));
        return cache.GetOrAdd(method,static (method,compilation)=>ローカルメソッドLinq解析実行37(method,compilation),compilation);
    }

    /// <summary>
    /// メソッドの構文ツリーを走査して System.Linq.Enumerable 呼び出しを解析する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description><see cref="メソッド構文Linq候補含む40"/> で構文レベルの早期フィルタを行い、LINQ 候補がなければ即 <see cref="ローカルメソッドLinq解析結果.Empty"/> を返す。</description></item>
    ///   <item><description>セマンティックモデルで各 <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax"/> のシンボルを解決し、<see cref="SystemLinqEnumerableメソッド判定38"/> が true のものだけを対象にする。</description></item>
    ///   <item><description>拡張メソッド呼び出し（<c>ReducedFrom != null</c>）のみを有効とし、静的呼び出しや out/ref パラメータ持ちは除外する。</description></item>
    ///   <item><description>Fragment 未対応メソッド名（<see cref="Enumerableメソッドフラグメント対応判定32"/> が false）を <c>unsupportedMethods</c> に集積する。</description></item>
    ///   <item><description>戻り値・型引数・パラメータのいずれかに匿名型が含まれる呼び出しがあれば <c>hasAnonymousTypeLinq</c> を true にする。</description></item>
    ///   <item><description>LINQ 呼び出しが見つからなかった場合でも、クエリ式（<c>from...select</c> 等）があれば <c>hasEnumerableCall = true</c> とする。</description></item>
    /// </list>
    /// </summary>
    private static ローカルメソッドLinq解析結果 ローカルメソッドLinq解析実行37(IMethodSymbol method,Compilation compilation) {
        if(method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return ローカルメソッドLinq解析結果.Empty;
        }

        if(!メソッド構文Linq候補含む40(methodSyntax)) {
            return ローカルメソッドLinq解析結果.Empty;
        }

        var semanticModel = compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        // 通常の block-bodied method と expression-bodied method の両方を見る。
        var invocationNodes = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                : [];

        var unsupportedMethods = new HashSet<string>(StringComparer.Ordinal);
        var hasEnumerableCall = false;
        var hasAnonymousTypeLinq = false;
        foreach(var invocation in invocationNodes) {
            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invokedMethod) {
                continue;
            }

            var definition = invokedMethod.ReducedFrom ?? invokedMethod;
            // target 判定では「Fragment 化できる LINQ か」ではなく
            // 「System.Linq.Enumerable を呼んでいるか」だけを見る。
            // これにより Cast/Append など未対応 or 一部未構築のケースも intercept され、
            // 後段で明示的な例外へ落とせる。
            if(!SystemLinqEnumerableメソッド判定38(definition)) {
                continue;
            }

            // 静的呼び出し（SystemEnumerable.Method(source, ...)）は最適化対象外。
            // 拡張メソッド呼び出し（source.Method(...)）のみを intercept 対象とする。
            if(invokedMethod.ReducedFrom is null) {
                continue;
            }

            // out/ref パラメータを持つメソッド（TryGetNonEnumeratedCount 等）は
            // Fragment 化できないため解析対象外とする。
            if(definition.Parameters.Any(static p=>p.RefKind != RefKind.None)) {
                continue;
            }

            hasEnumerableCall=true;
            if(!Enumerableメソッドフラグメント対応判定32(definition.Name)) {
                unsupportedMethods.Add(definition.Name);
            }

            // 匿名型を含む LINQ 呼び出しがあれば警告フラグを立てる。
            // 戻り値型・型引数・パラメータのいずれかに匿名型が含まれていればアウト。
            if(!hasAnonymousTypeLinq) {
                if(ContainsAnonymousType(invokedMethod.ReturnType)
                    || invokedMethod.TypeArguments.Any(ContainsAnonymousType)
                    || invokedMethod.Parameters.Any(static p=>ContainsAnonymousType(p.Type))) {
                    hasAnonymousTypeLinq=true;
                }
            }
        }

        // クエリ式（from ... select/where/orderby 等）も System.Linq.Enumerable 呼び出しとして扱う
        if(!hasEnumerableCall) {
            IEnumerable<SyntaxNode> queryBodyNodes = methodSyntax.Body is not null
                ? methodSyntax.Body.DescendantNodes()
                : methodSyntax.ExpressionBody is not null
                    ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf()
                    : [];
            if(queryBodyNodes.OfType<QueryExpressionSyntax>().Any()) {
                hasEnumerableCall = true;
            }
        }

        return new ローカルメソッドLinq解析結果(
            hasEnumerableCall,
            [..unsupportedMethods.OrderBy(static methodName=>methodName,StringComparer.Ordinal)],
            hasAnonymousTypeLinq);
    }

    /// <summary>
    /// セマンティックモデルを使わず構文だけで「メソッド本体内に System.Linq.Enumerable のメソッド名が出現するか」を判定する。
    /// <br/>セマンティック解析より安価なため、解析対象を絞る前処理として <see cref="ローカルメソッドLinq解析実行37"/> の冒頭で呼ばれる。
    /// <br/>判定対象: メンバーアクセス（<c>source.Where(…)</c>）・識別子（<c>Where(…)</c>）・
    /// ジェネリックメソッド名（<c>Cast&lt;T&gt;(…)</c>）のいずれかが
    /// <see cref="SystemLinqEnumerableMethodNames"/> に含まれるか、またはクエリ式が存在するか。
    /// </summary>
    private static bool メソッド構文Linq候補含む40(MethodDeclarationSyntax methodSyntax) {
        var invocationNodes = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                : [];

        foreach(var invocation in invocationNodes) {
            if(invocation.Expression is MemberAccessExpressionSyntax { Name: { } memberName }
                && SystemLinqEnumerableMethodNames.Contains(memberName.Identifier.ValueText)) {
                return true;
            }
            if(invocation.Expression is IdentifierNameSyntax identifierName
                && SystemLinqEnumerableMethodNames.Contains(identifierName.Identifier.ValueText)) {
                return true;
            }
            if(invocation.Expression is GenericNameSyntax genericName
                && SystemLinqEnumerableMethodNames.Contains(genericName.Identifier.ValueText)) {
                return true;
            }
        }

        // クエリ式（from ... select/where/orderby 等）も System.Linq.Enumerable 呼び出しに相当するため対象とする
        IEnumerable<SyntaxNode> bodyNodesForQuery = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf()
                : [];
        if(bodyNodesForQuery.OfType<QueryExpressionSyntax>().Any()) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// インターセプト対象の選定用。Fragment 化できるかどうかは問わず、
    /// メソッドの所属型が <c>System.Linq.Enumerable</c> であるかだけを判定する。
    /// <br/><c>method.ReducedFrom</c>（拡張メソッド解決前の定義）を優先して参照するため、
    /// 拡張メソッド呼び出し形式・静的呼び出し形式のどちらでも正しく判定できる。
    /// </summary>
    private static bool SystemLinqEnumerableメソッド判定38(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        return definition.ContainingType.ToDisplayString()=="System.Linq.Enumerable";
    }

    /// <summary>
    /// インターセプト対象群を名前空間ごとにグループ化し、1 ソースファイル分の C# テキストを生成する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description><see cref="インターセプト名前空間取得"/> で元の名前空間を <c>Intercept.{originalNs}</c> に変換し、グループ化する。</description></item>
    ///   <item><description>各名前空間ブロック内に <c>using {originalNs};</c> を挿入して、生成コードで短い型名を使えるようにする。</description></item>
    ///   <item><description>同一名前空間内でさらに型ごとにグループ化し、<see cref="インターセプター型ソース構築"/> に委譲する。</description></item>
    /// </list>
    /// </summary>
    /// <summary>
    /// ドット区切り名前空間の 1 つ上の親名前空間を返す。最上位（ドット無し）なら空文字列。
    /// </summary>
    private static string 親名前空間取得(string 名前空間) {
        var i=名前空間.LastIndexOf('.');
        return i<0 ? string.Empty : 名前空間[..i];
    }

    private static string インターセプターソース構築(ImmutableArray<Linq傍受対象型> targets,List<Diagnostic> diagnostics) {
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System.Linq;");

        // このファイルで生成する匿名型→record struct 透過置換のレジストリを用意し、
        // ネストした静的メソッド群（関数フラグメント生成12 等）から ThreadStatic 経由で参照させる。
        var anonRegistry = new AnonymousStructRecordRegistry(
            AnonRecordFileToken(targets.IsDefaultOrEmpty ? string.Empty : targets[0].SourcePath));
        var previousRegistry = _anonRegistry;
        _anonRegistry = anonRegistry;
        try {

        foreach(var namespaceGroup in targets.GroupBy(static target=>インターセプト名前空間取得(target.EnclosingType),StringComparer.Ordinal)) {
            builder.AppendLine();
            builder.AppendLine($"namespace {namespaceGroup.Key} {{");
            var originalNamespace = namespaceGroup.Key.StartsWith("Intercept.",StringComparison.Ordinal)
                ? namespaceGroup.Key["Intercept.".Length..]
                : string.Empty;
            if(!string.IsNullOrWhiteSpace(originalNamespace)) {
                // 生成コードは Intercept.{originalNamespace} という別名前空間に置かれるため、
                // 元のネスト名前空間スコープで暗黙に見えていた親名前空間のメンバー（拡張メソッド等）を
                // 解決できるよう、元の名前空間とその全祖先を using として出力する。
                // 生成側の名前空間が Intercept.Test... のように元の先頭セグメントを内包するため、
                // 素の using だと相対解決で Intercept.Test... を指してしまう。global:: で明示する。
                for(var ns=originalNamespace;!string.IsNullOrEmpty(ns);ns=親名前空間取得(ns)) {
                    builder.AppendLine($"    using global::{ns};");
                }
                builder.AppendLine();
            }
            foreach(var typeGroup in namespaceGroup.GroupBy(static target=>target.EnclosingType.ToDisplayString(完全修飾形式),StringComparer.Ordinal)) {
                var typeTargets = typeGroup.ToImmutableArray();
                var typeSource = インターセプター型ソース構築(typeTargets,diagnostics);
                インデント付き追記(builder,typeSource,"    ");
            }
            builder.AppendLine("}");
        }

        // 匿名型置換で割り当てた record struct 定義をグローバル名前空間のホルダークラスとして出力する。
        var holder = anonRegistry.RenderHolder();
        if(!string.IsNullOrEmpty(holder)) {
            builder.AppendLine();
            builder.Append(holder);
        }
        return builder.ToString();

        } finally {
            _anonRegistry = previousRegistry;
        }
    }

    /// <summary>
    /// 1 つの型に属するインターセプト対象群をまとめて <c>internal static class</c> ブロックへ出力する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description><see cref="型スタック取得"/> でネスト型の階層スタックを取得し、外側から順に <c>internal static class</c> を開く。</description></item>
    ///   <item><description>各ターゲットについて <see cref="生成ローカルメソッド本体構築0"/> を試みる。成功すれば <c>[InterceptsLocation]</c> 付きメソッドを出力する。</description></item>
    ///   <item><description>本体構築に失敗した場合は Fragment 未対応か展開不能かを判別し、いずれも例外 throw を生成して Tsavorite002 警告を発行する（静かなフォールバックは作らない）。</description></item>
    ///   <item><description>例外が発生した場合も同様に例外 throw コードを生成し、同一メソッドへの二重出力は <c>emittedMethodNames</c> で抑制する。</description></item>
    ///   <item><description>UnsafeAccessor 宣言はクラス直下に出力し、型スタックを逆順に閉じる。</description></item>
    /// </list>
    /// </summary>
    private static string インターセプター型ソース構築(ImmutableArray<Linq傍受対象型> targets,List<Diagnostic> diagnostics) {
        var builder = new StringBuilder();
        var typeStack = 型スタック取得(targets[0].EnclosingType);
        foreach (var t in typeStack)
        {
            builder.AppendLine($"internal static class {SanitizeIdentifier(t.Name)} {{");
        }

        var emittedMethodNames = new HashSet<string>(StringComparer.Ordinal);
        var emittedUnsafeAccessors = new HashSet<string>(StringComparer.Ordinal);
        foreach(var target in targets) {
            try {
                if(target.IsLocalMethodInterception) {
                    var ローカル静的メソッド群 = string.Empty;
                    var ローカル本体 = string.Empty;
                    if(生成ローカルメソッド本体構築0(target,out ローカル静的メソッド群,out ローカル本体,emittedUnsafeAccessors)) {
                        if(!string.IsNullOrWhiteSpace(ローカル静的メソッド群)) {
                            インデント付き追記(builder,ローカル静的メソッド群,"    ");
                        }
                        インターセプターXmlコメント追加(builder,target);
                        builder.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                        builder.AppendLine($"    {target.InterceptsLocationAttributeText}");
                        builder.AppendLine($"    public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}) {{");
                        インデント付き追記(builder,ローカル本体,"        ");
                        builder.AppendLine("    }");
                        builder.AppendLine();
                        continue;
                    }

                    インターセプターXmlコメント追加(builder,target);
                    builder.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                    builder.AppendLine($"    {target.InterceptsLocationAttributeText}");
                    builder.AppendLine($"    public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}) {{");
                    if(ローカルメソッド未対応Enumerable取得31(target) is { Count: > 0 } unsupportedMethods) {
                        var loc1 = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                        diagnostics.Add(Diagnostic.Create(展開失敗警告Descriptor,loc1,target.Method.Name));
                        インデント付き追記(builder,RenderUnsupportedLocalMethodBody(unsupportedMethods),"        ");
                    } else {
                        var loc2 = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                        diagnostics.Add(Diagnostic.Create(展開失敗警告Descriptor,loc2,target.Method.Name));
                        インデント付き追記(builder,RenderUninlineableLocalMethodBody(target),"        ");
                    }
                    builder.AppendLine("    }");
                    builder.AppendLine();
                }
            } catch(Exception ex) {
                if(emittedMethodNames.Contains(target.MethodName)) {
                    continue;
                }

                emittedMethodNames.Add(target.MethodName);
                var locEx = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                diagnostics.Add(Diagnostic.Create(展開失敗警告Descriptor,locEx,target.Method.Name));
                インターセプターXmlコメント追加(builder,target);
                builder.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine($"    {target.InterceptsLocationAttributeText}");
                builder.AppendLine($"    public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}) {{");
                インデント付き追記(builder,RenderUnsupportedDirectMethodBody(target,ex.Message),"        ");
                builder.AppendLine("    }");
                builder.AppendLine();
            }
        }

        for(var i = typeStack.Count-1; i>=0; i--) {
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    /// <summary>
    /// ネスト型の外側から内側への順序で型シンボルのリストを返す。
    /// <br/>例: <c>Outer.Inner</c> なら <c>[Outer, Inner]</c>。
    /// <br/><c>ContainingType</c> を辿りながらスタックに積み、最後に Reverse することで外→内の順にする。
    /// </summary>
    private static List<INamedTypeSymbol> 型スタック取得(INamedTypeSymbol type) {
        var stack = new List<INamedTypeSymbol>();
        for(INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) {
            stack.Add(current);
        }
        stack.Reverse();
        return stack;
    }

    /// <summary>
    /// 元の型の名前空間を <c>Intercept.{originalNs}</c> へ変換する。
    /// グローバル名前空間の型は <c>Intercept</c> のみ。
    /// インターセプターを別名前空間に置くことで、元コードと名前が衝突しない。
    /// </summary>
    private static string インターセプト名前空間取得(INamedTypeSymbol type) {
        var originalNamespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : string.Empty;
        return string.IsNullOrWhiteSpace(originalNamespace)
            ? "Intercept"
            : $"Intercept.{originalNamespace}";
    }

    /// <summary>
    /// ソースファイルのパスからインターセプター生成ファイルの HintName（<c>{FileName}.g.cs</c>）を返す。
    /// ファイル名が空の場合は <c>Unknown.g.cs</c> にフォールバックする。
    /// <see cref="SanitizeIdentifier"/> で C# 識別子として使えない文字を <c>_</c> に置換する。
    /// </summary>
    private static string インターセプターHintName取得(string sourcePath) {
        if(string.IsNullOrWhiteSpace(sourcePath)) {
            return "Unknown.g.cs";
        }
        //同名ファイルが別ディレクトリに存在しても hintName が衝突しないよう、フルパスから生成する
        return $"{SanitizeIdentifier(sourcePath)}.g.cs";
    }

    /// <summary>
    /// インターセプターメソッドの直前に XML doc コメントを追加する。
    /// 型が公開アクセス可能な場合は <c>&lt;see cref="Type"/&gt;</c> でクラスへのリンクを生成し、
    /// そうでない場合は <c>&lt;c&gt;</c> タグのみで完全修飾名を表示する。
    /// private メソッドへの <c>&lt;see cref&gt;</c> は Roslyn が解決できず CS1574 になるため、
    /// メソッド名は常に <c>&lt;c&gt;</c> タグで出力する。
    /// </summary>
    private static void インターセプターXmlコメント追加(StringBuilder builder,Linq傍受対象型 target) {
        var method = target.Method;
        var methodName = Xmlテキストエスケープ(method.Name);
        string content;
        if(型が公開アクセス可能か(method.ContainingType)){
            var typeCref = Xml属性値エスケープ(method.ContainingType.ToDisplayString(cref形式));
            content = $"<c>{methodName}()</c> in <see cref=\"{typeCref}\"/>";
        }else{
            var fqn = Xmlテキストエスケープ(メソッドCref構築(method));
            content = $"<c>{fqn}</c>";
        }
        builder.AppendLine($"    /// <summary>Intercepts {content}.</summary>");
    }

    private static bool 型が公開アクセス可能か(INamedTypeSymbol type) {
        for(var t = type; t is not null; t = t.ContainingType){
            if(t.DeclaredAccessibility is not(Accessibility.Public or Accessibility.Internal)){
                return false;
            }
        }
        return true;
    }

    private static string メソッドCref構築(IMethodSymbol method) {
        var containingType = method.ContainingType.ToDisplayString(cref形式);
        var parameters = string.Join(",",method.Parameters.Select(static parameter=>parameter.Type.ToDisplayString(cref形式)));
        return $"{containingType}.{method.Name}({parameters})";
    }

    private static string Xml属性値エスケープ(string text) =>
        text
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal);

    private static string Xmlテキストエスケープ(string text) =>
        text
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal);

    /// <summary>
    /// インターセプト対象メソッドの本体を丸ごと再構成し、文中の LINQ 呼び出しを Fragment 実行へ差し替える。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>メソッドの構文参照から <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax"/> を取得する。式本体（<c>=&gt;</c>）の場合は <c>式本体式</c> として保持し、後段で専用処理に渡す。</description></item>
    ///   <item><description><see cref="CollectMethodBodyIdentifiers"/> でメソッドスコープの識別子をすべて収集し、生成変数名の衝突防止に使う。</description></item>
    ///   <item><description>各文を <see cref="文生成16"/> に渡して再構成する。1 文でも失敗したら false を返す。</description></item>
    ///   <item><description>式本体の場合は <see cref="LINQ呼び出し差し替え文生成38_式本体"/> で処理する（ラムダ内に this. がある場合は対象外）。</description></item>
    ///   <item><description>LINQ 呼び出しが 1 件も置換されなかった場合は false を返す（再構成不要のメソッドを無駄にインターセプトしない）。</description></item>
    ///   <item><description>生成済み本体テキストの <c>this.</c> を <c>@this.</c> に置換して、インスタンスメソッドのレシーバー引数と整合させる。</description></item>
    ///   <item><description><see cref="静的メソッド群と本体を補助クラスへ退避21"/> で補助 struct を専用クラスへ退避し名前衝突を回避する。</description></item>
    ///   <item><description>Fragment ツリーおよびソース式から UnsafeAccessor 宣言を収集し、クラス直下用の <c>静的メソッド群</c> に追記する。</description></item>
    /// </list>
    /// </summary>
    private static bool 生成ローカルメソッド本体構築0(Linq傍受対象型 対象,out string 静的メソッド群,out string 本体,HashSet<string>? emittedUnsafeAccessors = null) {
        静的メソッド群=string.Empty;
        本体=string.Empty;

        // 以前は識別子ハッシュから開始番号を作っていたため Struct1144748 のような読みにくい名前になっていた。
        // いまはメソッドごとに 0 から連番を振り、必要なら外側で補助クラスに包んで衝突を避ける。
        var フラグメント番号 = フラグメント番号初期値取得18();
        if(対象.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax メソッド構文) {
            return false;
        }

        ExpressionSyntax? 式本体式 = null;
        IEnumerable<StatementSyntax> statements;
        if(メソッド構文.Body is { } ブロック本体) {
            statements = ブロック本体.Statements;
        } else if(メソッド構文.ExpressionBody is { Expression: var アロー式 }) {
            // 合成ノード SyntaxFactory.ReturnStatement(アロー式) を作ると SyntaxTree が null になり
            // semanticModel.GetSymbolInfo() が「構文ノードが構文ツリー内にありません」を投げる。
            // 元ツリーのノードを保持して後で専用関数に渡す。
            // インスタンスメソッドでも LINQ呼び出し差し替え文生成38_式本体 が PropagateContainingTypeName と
            // this.→@this. 変換でソース式レベルの this. を処理できる。
            // lambda 内の this. は 関数フラグメント生成12 が capturesThis を検出して @this を struct closure の
            // キャプチャ変数として持ち回るため、fragment struct 内でも @this は定義される。
            式本体式 = アロー式;
            statements = [];
        } else {
            return false;
        }

        var セマンティックモデル = 対象.SemanticModel.Compilation.GetSemanticModel(メソッド構文.SyntaxTree);
        var usedNames = CollectMethodBodyIdentifiers(メソッド構文, 対象.Method);
        var sourceUnsafeAccessorCollector = new List<string>();
        var generatedFragments = new List<Fragment>();
        var 本体ビルダー = new StringBuilder();
        var linq呼び出し数 = 0;
        foreach(var 文 in statements) {
            if(!文生成16(文,セマンティックモデル,対象.EnclosingType,ref フラグメント番号,対象.Method.IsStatic,ref 静的メソッド群,sourceUnsafeAccessorCollector,generatedFragments,本体ビルダー,ref linq呼び出し数,usedNames)) {
                return false;
            }
        }

        if(式本体式 is not null) {
            if(!LINQ呼び出し差し替え文生成38_式本体(式本体式,セマンティックモデル,対象.EnclosingType,ref フラグメント番号,対象.Method.IsStatic,ref 静的メソッド群,sourceUnsafeAccessorCollector,generatedFragments,本体ビルダー,ref linq呼び出し数,usedNames)) {
                return false;
            }
        }

        if(linq呼び出し数==0) {
            return false;
        }

        本体=本体ビルダー
            .ToString()
            .TrimEnd()
            .Replace("throw NoElements();","throw global::Tsavorite.GeneratedCodeHelpers.NoElements();",StringComparison.Ordinal);

        // 安全網: 匿名型→record struct 置換で取りこぼした生の匿名型表示が残ると不正な C# になるため、
        // その場合はインライン展開を諦めて呼び出し元の例外スタブ生成へフォールバックする。
        if(本体.IndexOf("<anonymous type",StringComparison.Ordinal)>=0
            || 静的メソッド群.IndexOf("<anonymous type",StringComparison.Ordinal)>=0) {
            return false;
        }

        if(!対象.Method.IsStatic) {
            本体=System.Text.RegularExpressions.Regex.Replace(
                本体,
                @"(?<!@)\bthis\.",
                "@this.",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        静的メソッド群と本体を補助クラスへ退避21(対象.MethodName,ref 静的メソッド群,ref 本体);

        // フラグメントツリーおよびソース式から UnsafeAccessor 宣言を収集し、補助クラスの外（クラス直下）に追記する。
        // これにより、インターセプタメソッド本体から短い名前で直接参照できる。
        // emittedUnsafeAccessors が渡された場合はクラスレベルで重複しないようフィルタする。
        var unsafeAccessorCodes = generatedFragments
            .SelectMany(static fragment=>fragment.CollectAllUnsafeAccessorCodes())
            .Concat(sourceUnsafeAccessorCollector)
            .Where(code=>emittedUnsafeAccessors?.Add(code) ?? true)
            .ToList();
        if(unsafeAccessorCodes.Count > 0) {
            静的メソッド群 = 静的メソッド群 + "\n" + string.Join("\n",unsafeAccessorCodes);
        }

        return true;
    }

    // 呼び出し式から対応する Fragment を選び、再生成に必要な静的補助コードも回収する。
    /// <summary>
    /// source.Method(...)
    /// ↓
    /// Method に応じた Fragment 連結へ振り分ける
    /// 例:
    /// source.Any(x=>x > 0) → Where(x=>x > 0) → Any()
    /// source.Join(inner, outerKey, innerKey, result) → Join(...)
    /// </summary>
    private static bool 呼び出し式フラグメント生成1(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        var methodName = definition.Name;

        if(invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            if(methodName=="Any" && method.Parameters.Length==0) {
                return false;
            }
        }

        if(PipelineBuilder.TryBuild(
            semanticModel,
            invocation,
            ref uniqueNumber,
            out fragment,
            out staticMethods,
            new PipelineBuildServices(
                完全修飾形式,
                (ExpressionSyntax expression,ref int number,out Enumerable enumerable,out string methods) =>
                    列挙フラグメント生成9(semanticModel,expression,containingType,ref number,out enumerable,out methods,unsafeAccessorCollector),
                (ExpressionSyntax expression,ITypeSymbol delegateType,ref int number,out FuncFragment func) =>
                    関数フラグメント生成12(expression,delegateType,ref number,out func,semanticModel,containingType),
                関数フラグメント利用可能28,
                expression=>RenderExpressionForGeneratedMethod(expression,semanticModel,containingType),
                (ITypeSymbol type,out ITypeSymbol elementType)=>{
                    if(列挙要素型取得11(type,out elementType,out _)) {
                        return true;
                    }
                    elementType=null!;
                    return false;
                },
                ContainsAnonymousType,
                静的メソッド連結14),
            unsafeAccessorCollector)) {
            return true;
        }

        // SelectMany selector の内部チェーンを struct にインライン化できる場合は専用パスへ。
        // レジストリ有効のまま試行することで内側チェーンの匿名型を record struct 化し、
        // 結果要素が匿名型なら SelectManyインライン化試行50 内の出口 Select で元の匿名型へ復元する。
        if(methodName=="SelectMany"
            && SelectManyインライン化試行50(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector)) {
            return true;
        }

        // 匿名型要素チェーン（TResult ファクトリ形）の ToList は要素型名を生成コードに書けないため、
        // Carrier の汎用 ToList へ型推論で委譲する直接実行文として生成する。
        // ソース構築はレジストリ有効のまま行い、内部の匿名型を record struct 置換できるようにする。
        if(methodName=="ToList"
            && invocation.ArgumentList.Arguments.Count==0
            && invocation.Expression is MemberAccessExpressionSyntax { Expression: var anonListSourceExpr }
            && ContainsAnonymousType(method.ReturnType)) {
            var anonListSnapshot = uniqueNumber;
            if(列挙フラグメント生成9(semanticModel,anonListSourceExpr,containingType,ref uniqueNumber,out var anonListSource,out var anonListStatics,unsafeAccessorCollector)
                && anonListSource.ElementType=="TResult") {
                fragment=new AnonymousExitToList(uniqueNumber++,anonListSource);
                staticMethods=anonListStatics;
                return true;
            }
            uniqueNumber=anonListSnapshot;
        }

        // ここから先は単発オペレーション等で「結果の匿名型がそのまま外へ逃げる」ケースが多い。
        // それらを record struct へ置換すると呼び出し元が期待する匿名型と型が合わなくなるため、
        // フォールバック経路では匿名型置換を抑止し、従来どおり匿名型を維持する。
        var anonSuspension = SuspendAnonRegistry();
        try {
            if(methodName=="Any") {
                return Anyフラグメント生成3(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
            }

            return 登録済みフラグメント生成24(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
        } finally {
            RestoreAnonRegistry(anonSuspension);
        }
    }

    /// <summary>
    /// source.Any(x=>x > 0)
    /// ↓
    /// Where(x=>x > 0) → Any()
    /// </summary>
    private static bool Anyフラグメント生成3(SemanticModel semanticModel,InvocationExpressionSyntax invocation,INamedTypeSymbol containingType,ref int uniqueNumber,out Fragment fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null)
       =>Any.TryBuildWithPredicate(
            semanticModel,invocation,containingType,ref uniqueNumber,
            out fragment,out staticMethods,unsafeAccessorCollector,
            列挙フラグメント生成9,
            関数フラグメント生成12,
            (name,variant,n,extraArgs)=>{
                if(!フラグメント候補選択25(name,variant,out var c)) return null;
                return (Fragment?)c.Constructor.Invoke([n,..extraArgs]);
            });

    /// <summary>
    /// アセンブリ内の Fragment 継承クラスをリフレクションで全列挙し、
    /// 型名の命名規則に基づいて「LINQ メソッド名 → その overload/variant 群」の
    /// 逆引き辞書を構築する。
    ///
    /// 【命名規則】
    ///   Fragment 継承クラスの型名は "{MethodName}_{VariantName}" の形式。
    ///   例:
    ///     Aggregate_func          → MethodName="Aggregate", VariantName="func"
    ///     Aggregate_seed_func     → MethodName="Aggregate", VariantName="seed_func"
    ///     Where_indexPredicate    → MethodName="Where",     VariantName="indexPredicate"
    ///     Join                    → MethodName="Join",      VariantName="" (空文字)
    ///
    /// 【除外する型 (helperTypes)】
    ///   抽象基底クラス Fragment, Scalar, Enumerable, Root は
    ///   具体 Fragment ではないため候補から除外する。
    ///
    /// 【コンストラクタの制約】
    ///   第1引数は必ず int (一意な識別番号) であること。
    ///   それ以降の引数が Fragment チェーン構築に使われるパラメータとなる。
    ///
    /// 【ソート順】
    ///   同一メソッド名の候補はパラメータ数の降順で並ぶ。
    ///   これにより 登録済みフラグメント生成試行26() が
    ///   より特化した overload (引数が多い) から先に試行できる。
    ///
    /// 【利用シーン】
    ///   この辞書は フラグメント候補一覧 静的フィールドとして保持され、
    ///   登録済みフラグメント生成24() や フラグメント候補選択25() から
    ///   「LINQ メソッド名 → 対応 Fragment 型」の解決に使われる。
    ///   新しい Fragment 継承クラスを追加するだけで自動的に認識されるため、
    ///   辞書の手動更新は不要。
    /// </summary>
    private static IReadOnlyDictionary<string,ImmutableArray<フラグメント候補>> フラグメント候補一覧構築23() {
        // Fragment 抽象基底クラスの型情報を取得
        var fragmentType = typeof(Fragment);
        // 具体 Fragment ではない基底型/補助型を除外リストに追加
        var helperTypes = new HashSet<Type> {
            typeof(Fragment),
            typeof(Scalar),
            typeof(Enumerable),
            typeof(Root),
        };

        // アセンブリ内の全型から Fragment 継承クラスを抽出
        var candidates = fragmentType.Assembly.GetTypes()
            .Where(type=>!type.IsAbstract                          // 抽象クラスは除外
                && fragmentType.IsAssignableFrom(type)               // Fragment を継承している
                && !helperTypes.Contains(type))                      // 基底型/補助型を除外
            .Select(type=>{
                // コンストラクタは1つだけ想定
                var constructor = type.GetConstructors().SingleOrDefault();
                if(constructor is null) {
                    return null;
                }

                var parameters = constructor.GetParameters();
                // 第1引数は int (一意識別番号) である必要がある
                if(parameters.Length==0 || parameters[0].ParameterType!=typeof(int)) {
                    return null;
                }

                // 型名を '_' で分割して MethodName と VariantName を抽出
                // 例: "Aggregate_seed_func" → ["Aggregate", "seed", "func"]
                var parts = type.Name.Split('_');
                var isApplicableMethod = type.GetMethod(
                    "IsApplicableOverload",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    [typeof(IMethodSymbol)],
                    null);
                Func<IMethodSymbol, bool>? isApplicableDelegate = isApplicableMethod is not null
                    ? method=>(bool)isApplicableMethod.Invoke(null,[method])!
                    : null;
                return new フラグメント候補 {
                    FragmentType=type,
                    Constructor=constructor,
                    MethodName=parts[0],                              // 例: "Aggregate"
                    VariantName=parts.Length>1                         // 例: "seed_func"
                        ? string.Join("_", parts.Skip(1))
                        : string.Empty,
                    Parameters=parameters[1..],                       // int 識別番号を除いた実パラメータ
                    IsApplicableOverload=isApplicableDelegate,
                };
            })
            .Where(static candidate=>candidate is not null)
            .Cast<フラグメント候補>()
            // パラメータ数の降順 → 型名の昇順でソート
            // パラメータ数が多い = より特化した overload を先に試行するため
            .OrderByDescending(static candidate=>candidate.Parameters.Length)
            .ThenBy(static candidate=>candidate.FragmentType.Name, StringComparer.Ordinal)
            // MethodName でグループ化して辞書化
            .GroupBy(static candidate=>candidate.MethodName, StringComparer.Ordinal)
            .ToDictionary(
                static group=>group.Key,
                static group=>group.ToImmutableArray(),
                StringComparer.Ordinal);

        return candidates;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// Fragment 候補一覧から Method 名に対応する型
    /// 例:
    /// Aggregate_func / Aggregate_seed_func / Aggregate_seed_func_resultSelector
    /// Where_predicate / Where_indexPredicate
    /// Join / GroupJoin
    /// を順に試し、成立したチェーンへ正規化する。
    /// </summary>
    private static bool 登録済みフラグメント生成24(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        if(!呼び出しソース式取得51(semanticModel,invocation,method,out var sourceExpression,out var argumentStartIndex,out var fragmentMethod)) {
            return false;
        }

        var methodName = (fragmentMethod.ReducedFrom ?? fragmentMethod).Name;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var candidate in candidates) {
            var snapshot = uniqueNumber;
            if(登録済みフラグメント生成試行26(candidate,semanticModel,invocation,sourceExpression,argumentStartIndex,fragmentMethod,containingType,ref snapshot,out fragment,out staticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                return true;
            }
        }

        return false;
    }

    private static bool 呼び出しソース式取得51(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out ExpressionSyntax sourceExpression,
        out int argumentStartIndex,
        out IMethodSymbol fragmentMethod) {
        sourceExpression=null!;
        argumentStartIndex=0;
        fragmentMethod=method;

        // 拡張メソッド呼び出しのみを対象とする。
        // 静的呼び出し（SystemEnumerable.Method(source, ...)）は展開せず、
        // 列挙フラグメント生成9 が単独列挙フラグメント生成10 にフォールバックして不透明な Root として扱う。
        if(method.ReducedFrom is not null
            && invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            sourceExpression=括弧除去53(receiverExpression);
            return true;
        }

        return false;
    }

    private static bool IsEnumerableSourceParameter(ITypeSymbol type) =>
        type is INamedTypeSymbol namedType
        && (IsIEnumerableType(namedType) || IsNonGenericIEnumerableType(namedType));

    /// <summary>
    /// source.SelectMany(p =&gt; innerChain) の内部チェーンを struct にインライン化する。
    /// <br/>selector が単一引数ラムダで本体が LINQ チェーンにパース可能な場合のみ成功する。
    /// 外側パラメータ名（例: p）を内側 Fragment の GetEnumeratorExpression 内で
    /// outerValue フィールド名に置換し、SelectMany_WithInlinedInner を生成する。
    /// </summary>
    private static bool SelectManyインライン化試行50(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        // 内側チェーンの構築は再帰的に呼び出し式フラグメント生成1 のフォールバック経路
        // （SuspendAnonRegistry 付き）を通るため、ピン留めして匿名型の record struct 置換を維持する。
        PinAnonRegistry();
        try {
            if(!SelectManyBase.TryBuildInlined(
                semanticModel,invocation,containingType,ref uniqueNumber,
                out fragment,out staticMethods,unsafeAccessorCollector,
                列挙フラグメント生成9,
                TryGetLambdaExpressionBody,
                GetLambdaParameters)) {
                return false;
            }
        } finally {
            UnpinAnonRegistry();
        }
        // 結果要素が匿名型でなければ struct インライン化結果をそのまま使う。
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.TypeArguments.Length==0) {
            return true;
        }
        var resultElementType=method.TypeArguments[^1];
        if(!ContainsAnonymousType(resultElementType)) {
            return true;
        }
        // 内側チェーンは record struct 置換で構築済みのため、要素を元の匿名型へ復元する
        // 出口 Select を被せる（トップレベル匿名型 Select と同じ TResult 推論形）。
        var registry=_anonRegistry;
        if(registry is null || fragment is not Enumerable selectManyFragment) {
            // レジストリ無効時は record struct 置換が行われず TryBuildInlined がここまで
            // 来ないはずだが、保険としてフォールバック経路へ委ねる。
            return false;
        }
        var recordTypeName=registry.ResolveClosedName(resultElementType);
        var exitParamName=$"__anonExit{uniqueNumber}";
        var exitBody=registry.ReconstructAnonymousCreation(resultElementType,exitParamName);
        var exitFragment=new AnonymousDelegateFuncFragment(
            $"global::System.Func<{recordTypeName},TResult>",
            $"_anonExitDel{uniqueNumber++}",
            $"({recordTypeName} {exitParamName})=>{exitBody}") {
            ReturnTypeName="TResult",
            ParameterTypeNames=[recordTypeName],
            ParameterNames=[exitParamName],
            CanGenerateMethod=false,
        };
        var exitSelect=new Select_selector(uniqueNumber++,selectManyFragment,exitFragment);
        fragment=exitSelect;
        staticMethods=静的メソッド連結14(staticMethods,exitSelect.StructMethod);
        return true;
    }

    private static bool フラグメント候補選択25(string methodName,string variantName,out フラグメント候補 candidate) {
        candidate=null!;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var current in candidates) {
            if(current.VariantName!=variantName) {
                continue;
            }
            candidate=current;
            return true;
        }

        return false;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// candidate(番号, sourceFragment, ...)
    /// のコンストラクター形へ引数を並べ替えて、1 本の Fragment チェーンへ正規化する。
    /// 例:
    /// source.Aggregate(seed, func) → Aggregate_seed_func(sourceFragment, seed, funcFragment)
    /// source.Select(selector) → Select(sourceFragment, selectorFragment)
    /// source.Join(inner, outerKey, innerKey, result, comparer?)
    ///   → Join(outerFragment, innerFragment, outerKeyFragment, innerKeyFragment, resultFragment, comparer)
    /// </summary>
    private static bool 登録済みフラグメント生成試行26(
        フラグメント候補 candidate,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax sourceExpression,
        int argumentStartIndex,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(!候補使用可能27(candidate,method)) {
            return false;
        }

        var constructorArguments = new List<object?>(candidate.Parameters.Length+1) { uniqueNumber++ };
        if(!列挙フラグメント生成9(semanticModel,sourceExpression,containingType,ref uniqueNumber,out var sourceFragment,out var sourceStaticMethods,unsafeAccessorCollector)) {
            return false;
        }

        constructorArguments.Add(sourceFragment);
        staticMethods=sourceStaticMethods;

        var argumentIndex = argumentStartIndex;
        for(var parameterIndex = 1; parameterIndex<candidate.Parameters.Length; parameterIndex++) {
            var parameter = candidate.Parameters[parameterIndex];
            if(parameter.ParameterType==typeof(string)
                && TryResolveSyntheticStringArgument(parameter,method,out var syntheticArgument)) {
                constructorArguments.Add(syntheticArgument);
                continue;
            }

            if(argumentIndex>=invocation.ArgumentList.Arguments.Count) {
                // "comparer" 文字列パラメータは省略可能（= null）。他の string は合成引数か必須引数なので return false。
                if(parameter.ParameterType==typeof(string) && parameter.Name=="comparer") {
                    constructorArguments.Add("null");
                    continue;
                }
                return false;
            }

            var argumentExpression = invocation.ArgumentList.Arguments[argumentIndex].Expression;
            if(parameter.ParameterType==typeof(string)) {
                constructorArguments.Add(RenderExpressionForGeneratedMethod(argumentExpression,semanticModel,containingType));
                argumentIndex++;
                continue;
            }

            if(typeof(FuncFragment).IsAssignableFrom(parameter.ParameterType)) {
                var methodParameterIndex = argumentIndex - argumentStartIndex;
                if(methodParameterIndex < 0
                    || methodParameterIndex >= method.Parameters.Length
                    || !関数フラグメント生成12(argumentExpression,method.Parameters[methodParameterIndex].Type,ref uniqueNumber,out var funcFragment,semanticModel,containingType)
                    || !関数フラグメント利用可能28(funcFragment)) {
                    return false;
                }

                constructorArguments.Add(funcFragment);
                argumentIndex++;
                continue;
            }

            if(typeof(Enumerable).IsAssignableFrom(parameter.ParameterType)) {
                if(!単独列挙フラグメント生成10(semanticModel,argumentExpression,containingType,ref uniqueNumber,out var enumerableFragment,out var enumerableStaticMethods,unsafeAccessorCollector)) {
                    return false;
                }

                constructorArguments.Add(enumerableFragment);
                staticMethods=静的メソッド連結14(staticMethods,enumerableStaticMethods);
                argumentIndex++;
                continue;
            }

            return false;
        }

        if(argumentIndex!=invocation.ArgumentList.Arguments.Count) {
            return false;
        }

        if(candidate.Constructor.Invoke(constructorArguments.ToArray()) is not Fragment generatedFragment) {
            return false;
        }

        fragment=generatedFragment;
        staticMethods=静的メソッド連結14(staticMethods,fragment.StructMethod);
        return true;
    }

    private static bool TryResolveSyntheticStringArgument(ParameterInfo parameter,IMethodSymbol method,out string value) {
        value=string.Empty;
        if(parameter.Name is not ("resultElementType" or "resultType")) {
            return false;
        }

        if(parameter.Name=="resultType") {
            value=method.ReturnType.ToDisplayString(完全修飾形式);
            return true;
        }

        if(!列挙要素型取得11(method.ReturnType,out var elementType,out _)) {
            return false;
        }

        value=elementType.ToDisplayString(完全修飾形式);
        return true;
    }

    private static bool 候補使用可能27(フラグメント候補 candidate,IMethodSymbol method)
       =>candidate.IsApplicableOverload?.Invoke(method) ?? true;

    private static bool 関数フラグメント利用可能28(FuncFragment funcFragment) {
        if(funcFragment.CanGenerateMethod) {
            return true;
        }

        // キャプチャ付きラムダ/delegate 式は struct closure として primary constructor に閉じ込め、
        // ループ内では式へ展開する。delegate 変数・メソッドグループは受け付けない。
        if(funcFragment is LambdaFunc) {
            return true;
        }

        // try-catch を含む複数文ブロック本体は BlockFunc で表現し、FuncAssign で文として展開する。
        // BlockFunc.RequiresStatementInlining = true のため FuncAssign 側が RenderInlineAssign を使う。
        if(funcFragment is BlockFunc) {
            return true;
        }

        if(funcFragment.CanInvokeAsDelegate) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// source.Select(...).Where(...)
    /// ↓
    /// Select(...) → Where(...)
    /// のように、入れ子の LINQ 呼び出しなら再帰的に Fragment チェーンへほどく。
    /// LINQ 呼び出しでなければ Root(sourceExpression) として扱う。
    /// </summary>
    private static bool 列挙フラグメント生成9(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null) {
        expression=括弧除去53(expression);
        if(expression is InvocationExpressionSyntax invocation
            && 呼び出し式フラグメント生成1(semanticModel,invocation,containingType,ref uniqueNumber,out var nestedFragment,out staticMethods,unsafeAccessorCollector)
            && nestedFragment is Enumerable enumerableFragment) {
            fragment=enumerableFragment;
            return true;
        }

        return 単独列挙フラグメント生成10(semanticModel,expression,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
    }

    /// <summary>
    /// 式を包む <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.ParenthesizedExpressionSyntax"/> を再帰的に剥がし、内側の式を返す。
    /// Fragment 生成の前処理として呼ぶことで、<c>(source.Where(…))</c> のような余分な括弧を無視できる。
    /// </summary>
    private static ExpressionSyntax 括弧除去53(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>
    /// Source() / Second() / rows
    /// ↓
    /// Root(sourceExpression)
    /// として、メソッドチェーンの起点になる列挙ソースへ正規化する。
    /// </summary>
    private static bool 単独列挙フラグメント生成10(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null) {
        staticMethods=string.Empty;
        var typeInfo = semanticModel.GetTypeInfo(expression);
        var expressionType = typeInfo.Type ?? typeInfo.ConvertedType;
        if(!列挙要素型取得11(expressionType,out var elementType,out var useEnumerableInterface)) {
            if(IsNonGenericIEnumerableType(expressionType)) {
                fragment=new NonGenericRoot(
                    uniqueNumber++,
                    RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector));
                return true;
            }

            fragment=null!;
            return false;
        }

        // 具象型エニュメレータ名を取得する。具象 GetEnumerator() が値型を返す場合はその型名を使い、
        // インターフェース経由の場合は IEnumerator<T> 扱いに切り替える。
        var elementTypeName = elementType.ToDisplayString(完全修飾形式);
        var (concreteEnumeratorTypeName, isDuckTyping) = 具象エニュメレータ型名取得(expressionType,elementType,useEnumerableInterface);
        var enumeratorTypeName = concreteEnumeratorTypeName
            ?? $"global::System.Collections.Generic.IEnumerator<{elementTypeName}>";
        // 具象型が取れなかった場合はインターフェース IEnumerator<T> になるため struct 制約なし扱いにする。
        var effectiveUseEnumerableInterface = useEnumerableInterface || concreteEnumeratorTypeName is null;

        var _srcExpr=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);
        var _num=uniqueNumber++;
        fragment = expressionType is IArrayTypeSymbol
            ? new ArrayRoot(_num, _srcExpr, elementTypeName)
            : IsListType(expressionType)
                ? new ListRoot(_num, _srcExpr, elementTypeName)
                : isDuckTyping && concreteEnumeratorTypeName is not null
                    ? new DuckTypingRoot(_num, _srcExpr, elementTypeName, concreteEnumeratorTypeName)
                    : CreateRootEnumerableFragment(_srcExpr, elementTypeName, enumeratorTypeName, effectiveUseEnumerableInterface, _num);
        return true;
    }

    /// <summary>
    /// ソース型の public な非明示的実装 <c>GetEnumerator()</c> を探し、具象エニュメレータ型名と
    /// duck-typing フラグを返す。
    /// <br/>インターフェース型・配列・戻り値がインターフェースや抽象型の場合は <c>(null, false)</c>。
    /// <br/><c>isDuckTyping=true</c>: エニュメレータが <c>IEnumerator&lt;T&gt;</c> を実装しない
    /// duck-typing struct（例: <c>ImmutableArray&lt;T&gt;.Enumerator</c>）。
    /// この場合 struct 制約 (<c>where TEnum : struct, IEnumerator&lt;T&gt;</c>) に渡せず
    /// <see cref="Linq.Fragments.DuckTypingRoot"/> で直接展開する必要がある。
    /// </summary>
    private static (string? typeName, bool isDuckTyping) 具象エニュメレータ型名取得(ITypeSymbol? sourceType,ITypeSymbol elementType,bool useEnumerableInterface) {
        if(useEnumerableInterface || sourceType is not INamedTypeSymbol namedSource) {
            return (null, false);
        }
        // 明示的インターフェース実装を除いた public GetEnumerator() を探す。
        var getEnumerator = namedSource.GetMembers("GetEnumerator")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Parameters.IsEmpty
                && m.DeclaredAccessibility==Accessibility.Public
                && m.MethodKind!=MethodKind.ExplicitInterfaceImplementation
                && !m.IsStatic);
        if(getEnumerator is null) {
            return (null, false);
        }
        // 戻り値が具象値型（インターフェースでも抽象型でもない）であれば使う。
        if(getEnumerator.ReturnType is not INamedTypeSymbol returnType
            || returnType.IsAbstract
            || returnType.TypeKind==TypeKind.Interface) {
            return (null, false);
        }
        var typeName = returnType.ToDisplayString(完全修飾形式);
        // IEnumerator<elementType> を実装しているかチェック。
        // ImmutableArray<T>.Enumerator などは duck-typing パターンのみで IEnumerator<T> を実装しない。
        // そのような型を struct 制約 (where TEnum : struct, IEnumerator<T>) に渡すと CS0315 が発生する。
        var implementsIEnumerator = returnType.AllInterfaces.Any(i =>
            i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
            && i.MetadataName == "IEnumerator`1"
            && i.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0],elementType));
        return (typeName, isDuckTyping: !implementsIEnumerator);
    }

    /// <summary>
    /// 型がちょうど <c>System.Collections.Generic.List&lt;T&gt;</c> かを判定する。
    /// 継承クラスは内部配列の意味が同じでも別物として扱い、従来の Root パスへ流す。
    /// <c>List&lt;T&gt;</c> と判定された場合は <see cref="Linq.Fragments.ListRoot"/> で
    /// <c>CollectionsMarshal.AsSpan</c> ベースの直走査を使える。
    /// </summary>
    private static bool IsListType(ITypeSymbol? type) =>
        type is INamedTypeSymbol named
        && named.OriginalDefinition.ToDisplayString()=="System.Collections.Generic.List<T>";

    /// <summary>
    /// 型シンボルから列挙要素型（<c>T</c>）を解決する。
    /// <br/>解決順序:
    /// <list type="number">
    ///   <item><description>配列型 (<c>T[]</c>) → 要素型を直接取得。<c>useEnumerableInterface=true</c>（配列は <c>GetEnumerator()</c> が interface 実装）。</description></item>
    ///   <item><description>型自体が <c>IEnumerable&lt;T&gt;</c> → 型引数を取得。<c>useEnumerableInterface=true</c>。</description></item>
    ///   <item><description>上記以外の名前付き型 → <c>AllInterfaces</c> から <c>IEnumerable&lt;T&gt;</c> を探す。見つかれば <c>useEnumerableInterface=false</c>（具象 <c>GetEnumerator()</c> が使える可能性あり）。</description></item>
    /// </list>
    /// <c>useEnumerableInterface=false</c> の場合、<see cref="具象エニュメレータ型名取得"/> でさらに具象エニュメレータ型を探す。
    /// </summary>
    private static bool 列挙要素型取得11(ITypeSymbol? type,out ITypeSymbol elementType,out bool useEnumerableInterface) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType) && namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(static candidate=>IsIEnumerableType(candidate) && candidate.TypeArguments.Length==1);
                if(enumerableInterface is not null) {
                    elementType=enumerableInterface.TypeArguments[0];
                    useEnumerableInterface=false;
                    return true;
                }
                break;
        }

        elementType=null!;
        useEnumerableInterface=false;
        return false;
    }

    private static bool TryCreateDelegateFuncFragment(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        ref int uniqueNumber,
        out FuncFragment fragment,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType) {
        fragment=null!;
        if(delegateType is not INamedTypeSymbol namedDelegate) {
            return false;
        }

        var invoke=namedDelegate.DelegateInvokeMethod
            ?? namedDelegate.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();
        if(invoke is null || invoke.ReturnsVoid) {
            return false;
        }

        var delegateTypeName=delegateType.ToDisplayString(完全修飾形式);
        var delegateFieldName=$"_del{uniqueNumber++}";
        string delegateConstructorArg;
        if(semanticModel is not null && containingType is not null) {
            delegateConstructorArg=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType);
        }else{
            delegateConstructorArg=NormalizeExpressionText(expression.ToString());
        }
        fragment=new DelegateFuncFragment(delegateTypeName,delegateFieldName,delegateConstructorArg) {
            ReturnTypeName=invoke.ReturnType.ToDisplayString(完全修飾形式),
            ParameterTypeNames=invoke.Parameters.Select(static p=>p.Type.ToDisplayString(完全修飾形式)).ToArray(),
            ParameterNames=invoke.Parameters.Select(static p=>p.Name).ToArray(),
            CanGenerateMethod=false,
            CanInlineAssign=false,
        };
        return true;
    }

    /// <summary>
    /// x=>x * 2 / (left, right)=>left + right
    /// ↓
    /// Select や Aggregate に渡せる FuncFragment へ正規化する。
    /// 生成可能な static ラムダなら補助メソッド化し、
    /// 閉包付きラムダならインライン代入だけ許可する。
    /// delegate 変数がラムダ / anonymous delegate 初期化なら、その初期化式へ戻して同じ規則で処理する。
    /// </summary>
    private static bool 関数フラグメント生成12(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        ref int uniqueNumber,
        out FuncFragment fragment,
        SemanticModel? semanticModel = null,
        INamedTypeSymbol? containingType = null) {
        fragment=null!;
        // 方針: 直接式へインライン化できるならそれを最優先し、クロージャが必要なら struct primary constructor に閉じ込める。
        // delegate Invoke へ逃がす経路は作らない。
        // 仕様: Func<T> f = x=>...; のようなローカル delegate 変数は initializer のラムダへ戻してインライン化する。
        // 再代入された delegate 変数や method group は inline 対象外。delegate Invoke fallback は生成しない。
        if(TryGetInlineableDelegateVariableInitializer(expression,semanticModel,out var initializerExpression)) {
            return 関数フラグメント生成12(
                initializerExpression,
                delegateType,
                ref uniqueNumber,
                out fragment,
                semanticModel,
                containingType);
        }

        if(expression is LambdaExpressionSyntax lambda) {
            // 複数文ブロック本体（=>{ ...; return x; ... }、try-catch 含む）は BlockFunc で文インライン化する。
            // 式本体・単一 return 文は従来どおり下の式インライン経路へ流す。
            if(lambda.Body is BlockSyntax lambdaBlock
                && !IsSingleReturnBlock(lambdaBlock)
                && TryBuildBlockFunc(lambdaBlock,GetLambdaParameters(lambda),delegateType,semanticModel,containingType,out var lambdaBlockFunc)) {
                fragment = lambdaBlockFunc;
                return true;
            }

            // 匿名型を含む delegate 型は、匿名型→ジェネリック record struct 透過置換で「名指し可能な型」へ写像して
            // 通常の LambdaFunc としてインライン展開する（delegate 保持に頼らない）。
            // これによりメソッドチェーン中間の匿名型が後続ステージへ展開できるようになる。
            if(TryBuildRegistryMappedLambdaFunc(lambda,delegateType,semanticModel,containingType,out var registryFragment)) {
                fragment = registryFragment;
                return true;
            }

            // 出口パス: 引数・戻り値の両方に匿名型がある場合（struct record → 匿名型への変換が必要）。
            // TryBuildRegistryMappedLambdaFunc がこのケースを除外するため、ここで専用フラグメントを生成する。
            if(TryBuildAnonExitDelegateFuncFragment(lambda,delegateType,ref uniqueNumber,out var anonExitFragment,semanticModel,containingType)) {
                fragment = anonExitFragment;
                return true;
            }

            // ラムダが匿名型（または匿名型を含む型）を「返す」場合 → AnonymousDelegateFuncFragment を生成。
            // 引数型のみ匿名型の場合（ソースが匿名型）は対象外：TryCreateLambdaFuncFragment 失敗で return false。
            // TryCreateLambdaFuncFragment より先にチェック（匿名型は delegate 型解決に失敗するため）。
            if(semanticModel is not null
                && containingType is not null
                && delegateType is INamedTypeSymbol { TypeArguments.Length: > 0 } funcTypeSym
                && ContainsAnonymousType(funcTypeSym.TypeArguments[^1])) {
                var returnTypeSym = funcTypeSym.TypeArguments[^1];
                var returnTypeName = ReplaceAnonymousTypesWithTResult(returnTypeSym);
                var paramTypeNames = funcTypeSym.TypeArguments.Take(funcTypeSym.TypeArguments.Length - 1)
                    .Select(static t=>t.ToDisplayString(完全修飾形式)).ToArray();
                var lambdaParamNames = GetLambdaParameters(lambda).ToArray();
                var anonCapturedLocals = Array.Empty<(string Name, string TypeName)>();
                var anonBodyAccessorCodes = Array.Empty<string>();
                if(TryGetLambdaExpressionBody(lambda,out var anonBodyExpr)) {
                    {
                        var anonBodyCollector = new List<string>();
                        RenderExpressionForGeneratedMethod(anonBodyExpr,semanticModel,containingType!,anonBodyCollector);
                        anonBodyAccessorCodes = anonBodyCollector.ToArray();
                    }
                    var anonDataFlow = semanticModel.AnalyzeDataFlow(anonBodyExpr);
                    if(anonDataFlow?.Succeeded == true) {
                        var anonParamSet = new HashSet<string>(lambdaParamNames,StringComparer.Ordinal);
                        anonCapturedLocals = anonDataFlow.DataFlowsIn
                            .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !anonParamSet.Contains(sym.Name) && sym.Name != "this")
                            .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                                ? loc.Type.ToDisplayString(完全修飾形式)
                                : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                            .ToArray();
                    }
                }
                var allTypeNames = paramTypeNames.Append(returnTypeName);
                var delegateTypeName = $"global::System.Func<{string.Join(", ", allTypeNames)}>";
                var delegateFieldName = $"_anonDel{uniqueNumber++}";
                var delegateConstructorArg = RenderExpressionForGeneratedMethod(lambda, semanticModel, containingType);
                fragment = new AnonymousDelegateFuncFragment(delegateTypeName, delegateFieldName, delegateConstructorArg) {
                    ReturnTypeName = returnTypeName,
                    ParameterTypeNames = paramTypeNames,
                    ParameterNames = lambdaParamNames,
                    CanGenerateMethod = false,
                    CapturedLocals = anonCapturedLocals,
                    BodyUnsafeAccessorCodes = anonBodyAccessorCodes
                };
                return true;
            }

            if(!TryCreateLambdaFuncFragment(lambda,delegateType,out var lambdaSpec)) {
                // delegate 型の引数に匿名型が含まれる場合（ソースが匿名型など）は投げずに return false。
                // 呼び出し元が元の System.Linq 式を維持する処理を行う。
                if(delegateType is INamedTypeSymbol { TypeArguments.Length: > 0 } ft
                    && ft.TypeArguments.Any(ContainsAnonymousType)) {
                    fragment = null!;
                    return false;
                }
                throw LambdaInlineException(lambda,semanticModel,"ラムダ本体が式または単一 return 文ではないか、delegate 型を解決できませんでした。");
            }

            var body = lambdaSpec.Body;
            var capturedLocals = Array.Empty<(string Name, string TypeName)>();
            var bodyAccessorCodes = Array.Empty<string>();
            var capturesThis = false;
            ExpressionSyntax? lambdaBodyExpr = null;
            if(semanticModel is not null && TryGetLambdaExpressionBody(lambda,out lambdaBodyExpr)) {
                if(containingType is not null) {
                    var bodyCollector = new List<string>();
                    body=RenderExpressionForGeneratedMethod(lambdaBodyExpr,semanticModel,containingType,bodyCollector);
                    bodyAccessorCodes = bodyCollector.ToArray();
                }
                // ラムダがキャプチャしているローカル変数・外側パラメータを検出する。
                // lambda 自身のパラメータと暗黙の this は除外する。
                var dataFlow = semanticModel.AnalyzeDataFlow(lambdaBodyExpr);
                if(dataFlow?.Succeeded == true) {
                    var paramSet = new HashSet<string>(lambdaSpec.ParameterNames,StringComparer.Ordinal);
                    var thisSymbol = dataFlow.DataFlowsIn.FirstOrDefault(sym=>sym.Name == "this") as IParameterSymbol;
                    capturesThis = thisSymbol is not null;
                    capturedLocals = dataFlow.DataFlowsIn
                        .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name != "this")
                        .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                            ? loc.Type.ToDisplayString(完全修飾形式)
                            : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                        .ToArray();
                    // ラムダ内の this. は Fragment struct 内では @this として参照されるため、
                    // struct closure のキャプチャ変数として明示的に持ち回る。
                    // 型は containingType（呼び出し元の囲み型。Full Fusion 時は呼び出し先と異なりうる）ではなく
                    // 実際の暗黙 this パラメーターの型（ラムダの宣言元インスタンスの型）を用いる。
                    if(thisSymbol is not null) {
                        capturedLocals = capturedLocals
                            .Append(("@this", thisSymbol.Type.ToDisplayString(完全修飾形式)))
                            .ToArray();
                    }
                }
            }

            if(semanticModel is not null
                && containingType is not null
                && lambdaBodyExpr is not null
                && TryFindSystemLinqInvocation(lambdaBodyExpr,semanticModel,out var scalarInvocation,out var scalarMethod)) {
                var snapshot = uniqueNumber;
                if(呼び出し式フラグメント生成1(
                        semanticModel,
                        scalarInvocation,
                        containingType,
                        ref snapshot,
                        out var scalarFragment,
                        out var scalarStaticMethods,
                        null)
                    && scalarFragment is Scalar
                    && ScalarFragmentSupportsInlineEnumeration(scalarFragment)) {
                    uniqueNumber = snapshot;
                    fragment = new InlinedScalarFunc(scalarFragment) {
                        ReturnTypeName = lambdaSpec.ReturnTypeName,
                        ParameterTypeNames = lambdaSpec.ParameterTypeNames,
                        ParameterNames = lambdaSpec.ParameterNames,
                        CanGenerateMethod = false,
                        CanInlineAssign = true,
                        RequiresStatementInlining = true,
                        InlineBody = string.Empty,
                        BodyExpression = RenderExpressionForGeneratedMethod(lambdaBodyExpr,semanticModel,containingType),
                        ScalarExpression = RenderExpressionForGeneratedMethod(scalarInvocation,semanticModel,containingType),
                        ScalarReturnTypeName = semanticModel.GetTypeInfo(scalarInvocation).Type?.ToDisplayString(完全修飾形式) ?? scalarFragment.GetType().Name,
                        CapturedLocals = capturedLocals,
                        BodyUnsafeAccessorCodes = bodyAccessorCodes
                            .Concat(scalarFragment.CollectAllUnsafeAccessorCodes())
                            .ToArray(),
                        ScalarStaticMethods = scalarStaticMethods,
                        OuterParameterNames = lambdaSpec.ParameterNames,
                    };
                    return true;
                }

                // シーケンスを返す LINQ は SelectMany などの collection selector として有効なので、
                // スカラー Fragment 化できない場合は通常ラムダとして残す。
            }

            // キャプチャなし（this 含む）なら実質 static → struct ゼロコピーでメソッド生成可能。
            var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
                || (!capturesThis && capturedLocals.Length == 0);
            fragment = new LambdaFunc {
                ReturnTypeName = lambdaSpec.ReturnTypeName,
                ParameterTypeNames = lambdaSpec.ParameterTypeNames,
                ParameterNames = lambdaSpec.ParameterNames,
                CanGenerateMethod = canGenerateMethod,
                CanInlineAssign = true,
                InlineBody = body,
                CapturedLocals = capturedLocals,
                BodyUnsafeAccessorCodes = bodyAccessorCodes,
            };
            return true;
        }

        if(expression is AnonymousMethodExpressionSyntax anonymousMethod) {
            // delegate(){ ...; return x; ... }（try-catch 含む複数文）も BlockFunc で文インライン化する。
            if(anonymousMethod.Block is BlockSyntax anonymousBlock
                && !IsSingleReturnBlock(anonymousBlock)
                && TryBuildBlockFunc(anonymousBlock,GetAnonymousMethodParameters(anonymousMethod),delegateType,semanticModel,containingType,out var anonymousBlockFunc)) {
                fragment = anonymousBlockFunc;
                return true;
            }

            if(!TryCreateAnonymousMethodFuncFragment(anonymousMethod,delegateType,out var anonymousSpec,out var bodyExpression)) {
                // delegate 式が匿名型を返す場合も AnonymousDelegateFuncFragment を生成する。
                if(delegateType is INamedTypeSymbol { TypeArguments.Length: > 0 } anonFuncType
                    && anonFuncType.TypeArguments.Any(ContainsAnonymousType)
                    && semanticModel is not null && containingType is not null) {
                    var anonReturnTypeName = ReplaceAnonymousTypesWithTResult(anonFuncType.TypeArguments[^1]);
                    var anonParamTypeNames = anonFuncType.TypeArguments.Take(anonFuncType.TypeArguments.Length - 1)
                        .Select(static t=>t.ToDisplayString(完全修飾形式)).ToArray();
                    var anonParamNames = GetAnonymousMethodParameters(anonymousMethod).ToArray();
                    var anonDelegateTypeName = $"global::System.Func<{string.Join(", ", anonParamTypeNames.Append(anonReturnTypeName))}>";
                    var anonFieldName = $"_anonDel{uniqueNumber++}";
                    var anonCtorArg = RenderExpressionForGeneratedMethod(anonymousMethod, semanticModel, containingType);
                    fragment = new AnonymousDelegateFuncFragment(anonDelegateTypeName, anonFieldName, anonCtorArg) {
                        ReturnTypeName = anonReturnTypeName,
                        ParameterTypeNames = anonParamTypeNames,
                        ParameterNames = anonParamNames,
                        CanGenerateMethod = false
                    };
                    return true;
                }
                throw new InvalidOperationException($"delegate 式をインライン展開できませんでした。LINQ delegate: '{NormalizeExpressionText(anonymousMethod.ToString())}'。理由: 本体が単一 return 文ではないか、delegate 型を解決できませんでした。");
            }

            var body = anonymousSpec.Body;
            var capturedLocals = Array.Empty<(string Name, string TypeName)>();
            var bodyAccessorCodes = Array.Empty<string>();
            var capturesThis = false;
            if(semanticModel is not null && containingType is not null) {
                var bodyCollector = new List<string>();
                body=RenderExpressionForGeneratedMethod(bodyExpression,semanticModel,containingType,bodyCollector);
                bodyAccessorCodes = bodyCollector.ToArray();

                var dataFlow = semanticModel.AnalyzeDataFlow(bodyExpression);
                if(dataFlow?.Succeeded == true) {
                    var paramSet = new HashSet<string>(anonymousSpec.ParameterNames,StringComparer.Ordinal);
                    capturesThis = dataFlow.DataFlowsIn.Any(sym=>sym.Name == "this");
                    capturedLocals = dataFlow.DataFlowsIn
                        .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name != "this")
                        .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                            ? loc.Type.ToDisplayString(完全修飾形式)
                            : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                        .ToArray();
                }
            }

            fragment = new LambdaFunc {
                ReturnTypeName = anonymousSpec.ReturnTypeName,
                ParameterTypeNames = anonymousSpec.ParameterTypeNames,
                ParameterNames = anonymousSpec.ParameterNames,
                CanGenerateMethod = !capturesThis && capturedLocals.Length == 0,
                CanInlineAssign = true,
                InlineBody = body,
                CapturedLocals = capturedLocals,
                BodyUnsafeAccessorCodes = bodyAccessorCodes,
            };
            return true;
        }

        if(TryCreateDelegateFuncFragment(expression,delegateType,ref uniqueNumber,out fragment,semanticModel,containingType)) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// delegate 型に匿名型が含まれるラムダを、匿名型→ジェネリック record struct 透過置換で
    /// 名指し可能な型へ写像し、通常の <see cref="LambdaFunc"/>（インライン展開）として構築する。
    /// <br/>・引数型の匿名型 → record struct（メンバーアクセスはプロパティ名一致につき本体は書き換え不要）
    /// <br/>・戻り値の匿名型 → record struct（本体ルートの <c>new {...}</c> を <c>new record(...)</c> へ再帰的に書き換え）
    /// <br/>レジストリ未設定・匿名型なし・本体が匿名生成式でない戻り匿名型などは false を返し、従来経路へ委ねる。
    /// </summary>
    private static bool TryBuildRegistryMappedLambdaFunc(
        LambdaExpressionSyntax lambda,
        ITypeSymbol delegateType,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType,
        out FuncFragment fragment) {
        fragment = null!;
        var registry = _anonRegistry;
        if(registry is null || semanticModel is null || containingType is null) {
            return false;
        }
        if(delegateType is not INamedTypeSymbol funcType
            || funcType.TypeArguments.Length==0
            || !funcType.TypeArguments.Any(ContainsAnonymousType)
            || !IsInlineDelegateType(funcType)) {
            return false;
        }
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }

        var parameters = GetLambdaParameters(lambda);
        var paramTypeSyms = funcType.TypeArguments.Take(funcType.TypeArguments.Length-1).ToArray();
        if(parameters.Count!=paramTypeSyms.Length) {
            return false;
        }
        var returnTypeSym = funcType.TypeArguments[^1];

        var strippedBody = StripParentheses(body);
        if(ContainsAnonymousType(returnTypeSym) && strippedBody is not AnonymousObjectCreationExpressionSyntax) {
            // identity lambda (p=>p) は引数の struct record をそのまま返せるため通過させる。
            // それ以外（本体ルートが匿名生成式でも identity でもない）は struct record
            // ファクトリへ変換できないため、呼び出し元の TryBuildAnonExitDelegateFuncFragment へ委ねる。
            if(strippedBody is not IdentifierNameSyntax identityId || !parameters.Contains(identityId.Identifier.ValueText)) {
                return false;
            }
        }

        var parameterTypeNames = paramTypeSyms.Select(registry.ResolveClosedName).ToArray();
        var returnTypeName = registry.ResolveClosedName(returnTypeSym);

        var bodyCollector = new List<string>();
        var bodyText = strippedBody is AnonymousObjectCreationExpressionSyntax anonCreate
            ? registry.RenderAnonymousCreation(anonCreate,semanticModel,
                expr=>RenderExpressionForGeneratedMethod(expr,semanticModel,containingType,bodyCollector))
            : RenderExpressionForGeneratedMethod(body,semanticModel,containingType,bodyCollector);

        var capturedLocals = Array.Empty<(string Name, string TypeName)>();
        var capturesThis = false;
        var dataFlow = semanticModel.AnalyzeDataFlow(body);
        if(dataFlow?.Succeeded==true) {
            var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
            var thisSymbol = dataFlow.DataFlowsIn.FirstOrDefault(sym=>sym.Name=="this") as IParameterSymbol;
            capturesThis = thisSymbol is not null;
            capturedLocals = dataFlow.DataFlowsIn
                .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name!="this")
                .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                    ? loc.Type.ToDisplayString(完全修飾形式)
                    : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                .ToArray();
            // ラムダ内の this. は Fragment struct 内では @this として参照されるため、
            // 実際の暗黙 this パラメーターの型（ラムダの宣言元インスタンスの型）でキャプチャする。
            if(thisSymbol is not null) {
                capturedLocals = capturedLocals
                    .Append(("@this", thisSymbol.Type.ToDisplayString(完全修飾形式)))
                    .ToArray();
            }
        }

        var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
            || (!capturesThis && capturedLocals.Length==0);

        fragment = new LambdaFunc {
            ReturnTypeName = returnTypeName,
            ParameterTypeNames = parameterTypeNames,
            ParameterNames = parameters.ToArray(),
            CanGenerateMethod = canGenerateMethod,
            CanInlineAssign = true,
            InlineBody = bodyText,
            CapturedLocals = capturedLocals,
            BodyUnsafeAccessorCodes = bodyCollector.ToArray(),
        };
        return true;
    }

    /// <summary>
    /// 引数型・戻り値型の両方に匿名型が含まれるラムダを、struct record → 元の匿名型への再構成で
    /// <see cref="AnonymousDelegateFuncFragment"/> として構築する。
    /// <br/>対象: 引数が struct record に置換済みで、戻り値は実際の匿名型を返す必要がある「出口ステップ」。
    /// <br/>例1: GroupBy key selector <c>p=&gt;p</c> (identity) → <c>(struct_rec p)=&gt;new{p.field,...}</c>
    /// <br/>例2: GroupBy element selector <c>p=&gt;new{{x=p}}</c> → <c>(struct_rec p)=&gt;new{{x=new{{p.field,...}}}}</c>
    /// </summary>
    private static bool TryBuildAnonExitDelegateFuncFragment(
        LambdaExpressionSyntax lambda,
        ITypeSymbol delegateType,
        ref int uniqueNumber,
        out FuncFragment fragment,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType) {
        fragment=null!;
        var registry=_anonRegistry;
        if(registry is null || semanticModel is null || containingType is null) {
            return false;
        }
        if(delegateType is not INamedTypeSymbol funcType
            || funcType.TypeArguments.Length==0
            || !IsInlineDelegateType(funcType)) {
            return false;
        }
        var paramTypeSyms=funcType.TypeArguments.Take(funcType.TypeArguments.Length-1).ToArray();
        var returnTypeSym=funcType.TypeArguments[^1];
        // このパスは: 戻り値が匿名型の identity lambda (p=>p) など、
        // TryBuildRegistryMappedLambdaFunc が struct record ファクトリへ変換できなかった場合に限定する。
        if(!ContainsAnonymousType(returnTypeSym)) {
            return false;
        }
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }
        var parameters=GetLambdaParameters(lambda);
        if(parameters.Count!=paramTypeSyms.Length) {
            return false;
        }
        var strippedBody=StripParentheses(body);
        // identity: p=>p → 戻り値の匿名型全体を struct record のメンバーから再構成する。
        if(strippedBody is not IdentifierNameSyntax identId || !parameters.Contains(identId.Identifier.ValueText)) {
            return false;
        }
        var bodyCollector=new List<string>();
        var bodyText=registry.ReconstructAnonymousCreation(returnTypeSym,identId.Identifier.ValueText);
        var paramTypeNames=paramTypeSyms.Select(registry.ResolveClosedName).ToArray();
        // キャプチャローカルを収集する。
        var capturedLocals=Array.Empty<(string Name,string TypeName)>();
        var dataFlow=semanticModel.AnalyzeDataFlow(body);
        if(dataFlow?.Succeeded==true) {
            var paramSet=new HashSet<string>(parameters,StringComparer.Ordinal);
            capturedLocals=dataFlow.DataFlowsIn
                .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name!="this")
                .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                    ? loc.Type.ToDisplayString(完全修飾形式)
                    : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                .ToArray();
        }
        // 引数を struct record 型で明示したラムダ式を組み立てて、デリゲートとして格納する。
        var paramDecls=parameters.Zip(paramTypeNames,static (n,t) => $"{t} {n}");
        var delegateLambda=$"({string.Join(",",paramDecls)})=>{bodyText}";
        var allTypeNames=paramTypeNames.Append("TResult");
        var delegateTypeName2=$"global::System.Func<{string.Join(",",allTypeNames)}>";
        var delegateFieldName=$"_anonExitDel{uniqueNumber++}";
        fragment=new AnonymousDelegateFuncFragment(delegateTypeName2,delegateFieldName,delegateLambda) {
            ReturnTypeName="TResult",
            ParameterTypeNames=paramTypeNames,
            ParameterNames=parameters.ToArray(),
            CanGenerateMethod=false,
            CapturedLocals=capturedLocals,
            BodyUnsafeAccessorCodes=bodyCollector.ToArray(),
        };
        return true;
    }

    /// <summary>
    /// LINQ に渡された識別子またはメンバーアクセス式が、宣言時にラムダ／<c>delegate</c> で初期化された
    /// ローカル変数またはフィールドであるかを判定し、そのイニシャライザ式を返す。
    /// <br/>成功条件: シンボルが <c>ILocalSymbol</c> または <c>IFieldSymbol</c> であり、
    /// 宣言の <c>VariableDeclarator.Initializer.Value</c> が <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.LambdaExpressionSyntax"/>
    /// または <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.AnonymousMethodExpressionSyntax"/> であること。
    /// <br/><c>field</c> / <c>this.field</c> / <c>Type.field</c> のいずれの参照形でも対象にする。
    /// <br/>delegate 変数呼び出しを delegate Invoke として残す経路は作らない。
    /// </summary>
    private static bool TryGetInlineableDelegateVariableInitializer(
        ExpressionSyntax expression,
        SemanticModel? semanticModel,
        out ExpressionSyntax initializerExpression) {
        initializerExpression=null!;
        if(semanticModel is null
            || expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax)) {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if(symbol is not (ILocalSymbol or IFieldSymbol)
            || symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax {
                Initializer.Value: LambdaExpressionSyntax or AnonymousMethodExpressionSyntax
            } variableDeclarator) {
            return false;
        }

        initializerExpression=variableDeclarator.Initializer.Value;
        return true;
    }

    private static InvalidOperationException LambdaInlineException(
        LambdaExpressionSyntax lambda,
        SemanticModel? semanticModel,
        string reason,
        InvocationExpressionSyntax? linqInvocation = null) {
        var linqText = linqInvocation is not null
            ? NormalizeExpressionText(linqInvocation.ToString())
            : semanticModel is not null && TryFindSystemLinqInvocation(lambda,semanticModel,out var foundInvocation,out _)
                ? NormalizeExpressionText(foundInvocation.ToString())
                : NormalizeExpressionText(lambda.ToString());
        return new InvalidOperationException($"ラムダ式をインライン展開できませんでした。LINQ: '{linqText}'。理由: {reason}");
    }

    // スカラー subquery を InlinedScalarFunc（using-enumerator 文インライン）で展開できるかを判定する。
    // Sum/ToList/ToArray/ToDictionary など直接実行のみの PipelineTerminal は enumerator 式を作れず
    // GetEnumeratorExpression が NotSupportedException を投げるため、その場合は InlinedScalarFunc 化を諦め、
    // 呼び出し元で生のラムダ本体テキスト（実行時 LINQ-to-objects）として残す（Method 版と同じ挙動）。
    private static bool ScalarFragmentSupportsInlineEnumeration(Fragment scalarFragment) {
        try {
            _=scalarFragment.GetEnumeratorExpression;
            return true;
        } catch(NotSupportedException) {
            return false;
        }
    }
    private static bool TryFindSystemLinqInvocation(
        SyntaxNode node,
        SemanticModel semanticModel,
        out InvocationExpressionSyntax invocation,
        out IMethodSymbol method) {
        foreach(var candidate in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()) {
            if(semanticModel.GetSymbolInfo(candidate).Symbol is IMethodSymbol candidateMethod
                && SystemLinqEnumerableメソッド判定38(candidateMethod)) {
                invocation=candidate;
                method=candidateMethod;
                return true;
            }
        }

        invocation=null!;
        method=null!;
        return false;
    }

    /// <summary>
    /// 2 つの補助静的メソッド文字列を改行で連結する。空文字列・空白のみの文字列は無視する。
    /// Fragment 構築中に積み上がる <c>staticMethods</c> を逐次マージするために使う。
    /// </summary>
    private static string 静的メソッド連結14(string left,string right) {
        return string.Join("\n",new[] { left,right }.Where(static text=>!string.IsNullOrWhiteSpace(text)));
    }

    /// <summary>
    /// Fragment を実行するコードを <paramref name="builder"/> に追記する。
    /// <br/>Fragment の種類に応じて 3 通りの出力を使い分ける:
    /// <list type="bullet">
    ///   <item><description><see cref="DirectExecutionFragment"/>: <c>GetDirectExecutionStatement</c> の結果をそのまま出力（例: <c>ToList()</c> など副作用付き終端操作）。</description></item>
    ///   <item><description><see cref="Enumerable"/>: <c>var {name} = {GetEnumerableExpression};</c> を出力（遅延評価の列挙結果を変数に束縛）。</description></item>
    ///   <item><description>その他の <see cref="Fragment"/>（スカラー等）: <c>GetEnumeratorExpression</c> で enumerator を取得し <c>TryGetNext</c> で 1 要素を取り出すパターンを出力。</description></item>
    /// </list>
    /// <see cref="GetUniqueIdentifier"/> で enumerator 変数名・current 変数名の衝突を避ける。
    /// </summary>
    private static bool フラグメントEnumerator実行文追加37(StringBuilder builder,Fragment fragment,string actualVariableName,string actualTypeName,HashSet<string> usedNames) {
        var suffix = SanitizeIdentifier(actualVariableName);
        var enumeratorName = GetUniqueIdentifier($"e_{suffix}", usedNames);
        var currentName = GetUniqueIdentifier($"c_{suffix}", usedNames);

        if(fragment is DirectExecutionFragment directExecutionFragment) {
            builder.AppendLine(directExecutionFragment.GetDirectExecutionStatement(actualVariableName,actualTypeName));
            return true;
        }

        if(fragment is Enumerable enumerableFragment) {
            try {
                builder.AppendLine($"var {actualVariableName}={enumerableFragment.GetEnumerableExpression};");
            } catch(NotSupportedException) {
                return false;
            }
            return true;
        }

        string scalarEnumeratorExpression;
        try {
            scalarEnumeratorExpression=fragment.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return false;
        }
        builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        builder.AppendLine($"using var {enumeratorName}={scalarEnumeratorExpression};");
        builder.AppendLine($"{enumeratorName}.TryGetNext(out {actualVariableName});");
        return true;
    }

    /// <summary>
    /// <c>expr.Consume(consumer)</c>（引数1個・文形）を直接消費ループへ展開する。
    /// <br/>演算子別の特殊処理は持たない（thin orchestrator）。<see cref="呼び出し式フラグメント生成1"/> で
    /// Fragment を構築し、消費ループの組み立ては <see cref="Enumerable.GetConsumeExecutionStatement"/> に委譲する。
    /// span 直走査などの最適化は各 Fragment 側に存在する（コード片は Fragment が持つ原則）。
    /// </summary>
    private static bool 直接Consumeループ文生成43(
        ExpressionStatementSyntax statement,
        InvocationExpressionSyntax linqInvocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        if(statement.Expression is not InvocationExpressionSyntax consumeInvocation) {
            return false;
        }
        if(consumeInvocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.ValueText!="Consume"
            || consumeInvocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }

        ExpressionSyntax? consumerExpression = null;
        var normalizedLinqInvocation = NormalizeExpressionText(linqInvocation.ToString());
        if(NormalizeExpressionText(memberAccess.Expression.ToString())==normalizedLinqInvocation) {
            consumerExpression=consumeInvocation.ArgumentList.Arguments[0].Expression;
        } else if(NormalizeExpressionText(consumeInvocation.ArgumentList.Arguments[0].Expression.ToString())==normalizedLinqInvocation) {
            consumerExpression=memberAccess.Expression;
        }
        if(consumerExpression is null) {
            return false;
        }

        var snapshot = uniqueNumber;
        var tempName = GetUniqueIdentifier($"__linq{linqInvocationCount++}", usedNames);
        if(!呼び出し式フラグメント生成1(semanticModel,linqInvocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector)) {
            uniqueNumber=snapshot;
            return false;
        }
        if(fragment is not Enumerable enumerableFragment) {
            uniqueNumber=snapshot;
            return false;
        }
        if(!methodIsStatic) {
            fragment.PropagateContainingTypeName(containingType.ToDisplayString(完全修飾形式));
        }
        generatedFragments.Add(fragment);
        staticMethods=静的メソッド連結14(staticMethods,invocationStaticMethods);
        staticMethods=静的メソッド連結14(staticMethods,fragment.GetEnumeratorStruct);

        var consumerName = GetUniqueIdentifier($"consumer_{SanitizeIdentifier(tempName)}",usedNames);
        builder.AppendLine($"var {consumerName}={RenderExpressionForGeneratedMethod(consumerExpression,semanticModel,containingType,unsafeAccessorCollector)};");
        // 演算子別の Consume 展開は Fragment（Enumerable.GetConsumeExecutionStatement）が担う。
        builder.AppendLine(enumerableFragment.GetConsumeExecutionStatement(consumerName));
        return true;
    }

    /// <summary>
    /// 1 文を受け取り、対応するコードを <paramref name="builder"/> に生成する。
    /// <br/>文の種類に応じて専用メソッドへディスパッチする:
    /// <list type="bullet">
    ///   <item><description><c>Block</c> → 子文を再帰的に処理してブレースで囲む。</description></item>
    ///   <item><description><c>Try</c> → <see cref="Try文生成39"/>。catch/finally ブロックも再帰処理する。</description></item>
    ///   <item><description><c>Using</c> → <see cref="Using文生成40"/>。</description></item>
    ///   <item><description><c>LocalDeclaration</c> / <c>ExpressionStatement</c> → <see cref="LINQ呼び出し差し替え文生成38"/> で LINQ 呼び出しを差し替え。</description></item>
    ///   <item><description><c>For</c> / <c>ForEach</c> / <c>While</c> / <c>Do</c> / <c>If</c> / <c>Switch</c> → 各専用生成メソッドへ。</description></item>
    ///   <item><description>その他の文 → <see cref="LINQ呼び出し差し替え文生成38"/> にフォールバック。</description></item>
    /// </list>
    /// 1 文でも false を返した時点でメソッド全体の再構成を中止する。
    /// </summary>
    private static bool 文生成16(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        switch(statement) {
            case BlockSyntax block:
                builder.AppendLine("{");
                foreach(var childStatement in block.Statements) {
                    if(!文生成16(childStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
                        return false;
                    }
                }
                builder.AppendLine("}");
                return true;
            case TryStatementSyntax tryStatement:
                return Try文生成39(tryStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case UsingStatementSyntax usingStatement:
                return Using文生成40(usingStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case LocalDeclarationStatementSyntax localDeclaration:
                return LINQ呼び出し差し替え文生成38(localDeclaration,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case ExpressionStatementSyntax expressionStatement:
                return LINQ呼び出し差し替え文生成38(expressionStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case ForStatementSyntax forStatement:
                return For文生成44(forStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case ForEachStatementSyntax foreachStatement:
                return ForEach文生成45(foreachStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case WhileStatementSyntax whileStatement:
                return While文生成46(whileStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case DoStatementSyntax doStatement:
                return Do文生成47(doStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case IfStatementSyntax ifStatement:
                return If文生成48(ifStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            case SwitchStatementSyntax switchStatement:
                return Switch文生成49(switchStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
            default:
                return LINQ呼び出し差し替え文生成38(statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
        }
    }

    /// <summary>
    /// <c>try { … } catch(…) { … } finally { … }</c> 文を生成する。
    /// try ブロック・各 catch ブロック・finally ブロックをそれぞれ <see cref="文生成16"/> で再帰処理する。
    /// catch 宣言・catch フィルター (<c>when</c>) はソーステキストをそのまま出力する。
    /// </summary>
    private static bool Try文生成39(
        TryStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        builder.AppendLine("try");
        if(!文生成16(statement.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
            return false;
        }

        foreach(var catchClause in statement.Catches) {
            builder.Append("catch");
            if(catchClause.Declaration is not null) {
                builder.Append(catchClause.Declaration.ToString());
            }
            if(catchClause.Filter is not null) {
                builder.Append(catchClause.Filter.ToString());
            }
            builder.AppendLine();
            if(!文生成16(catchClause.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
                return false;
            }
        }

        if(statement.Finally is not null) {
            builder.AppendLine("finally");
            if(!文生成16(statement.Finally.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// <c>using (declaration) { … }</c> または <c>using (expression) { … }</c> 文を生成する。
    /// 変数宣言がある場合は <see cref="変数宣言生成43"/> で型名を完全修飾し、
    /// 式の場合は <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングする。
    /// 本体は <see cref="文生成16"/> で再帰処理する。
    /// </summary>
    private static bool Using文生成40(
        UsingStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        if(statement.Declaration is not null) {
            builder.AppendLine($"using({変数宣言生成43(statement.Declaration,semanticModel,containingType,unsafeAccessorCollector)})");
        } else if(statement.Expression is not null) {
            builder.AppendLine($"using({RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector)})");
        } else {
            return false;
        }
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>for (init; cond; incr) { … }</c> 文を生成する。
    /// 初期化部・条件部・増分部はそれぞれ <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// 本体は <see cref="文生成16"/> で再帰処理する。
    /// 初期化部が変数宣言の場合は <see cref="変数宣言生成43"/> で完全修飾する。
    /// </summary>
    private static bool For文生成44(
        ForStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var init = statement.Declaration is not null
            ? 変数宣言生成43(statement.Declaration,semanticModel,containingType,unsafeAccessorCollector)
            : string.Join(", ",statement.Initializers.Select(e=>RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        var cond = statement.Condition is not null
            ? RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector)
            : string.Empty;
        var incr = string.Join(", ",statement.Incrementors.Select(e=>RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        builder.AppendLine($"for({init}; {cond}; {incr})");
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>foreach (T x in expr) { … }</c> 文を生成する。
    /// 要素型をセマンティックモデルで解決して完全修飾名にする（<c>var</c> はそのまま残す）。
    /// 列挙対象式は <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングする。
    /// 本体は <see cref="文生成16"/> で再帰処理する。
    /// </summary>
    private static bool ForEach文生成45(
        ForEachStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var typeSymbol = semanticModel.GetTypeInfo(statement.Type).Type;
        var typeName = typeSymbol is not null
            ? typeSymbol.ToDisplayString(完全修飾形式)
            : (statement.Type is IdentifierNameSyntax { IsVar: true } ? "var" : NormalizeExpressionText(statement.Type.ToString()));
        var exprText = RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"foreach({typeName} {statement.Identifier} in {exprText})");
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>while (cond) { … }</c> 文を生成する。
    /// 条件式を <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// 本体を <see cref="文生成16"/> で再帰処理する。
    /// </summary>
    private static bool While文生成46(
        WhileStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"while({condText})");
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

    /// <summary>
    /// <c>do { … } while (cond);</c> 文を生成する。
    /// 本体を <see cref="文生成16"/> で先に再帰処理し、その後に条件式を出力する。
    /// </summary>
    private static bool Do文生成47(
        DoStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        builder.AppendLine("do");
        if(!文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"while({condText});");
        return true;
    }

    /// <summary>
    /// <c>if (cond) { … } else { … }</c> 文を生成する。
    /// 条件式を <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// then 節・else 節をそれぞれ <see cref="文生成16"/> で再帰処理する。
    /// </summary>
    private static bool If文生成48(
        IfStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"if({condText})");
        if(!文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
        if(statement.Else is not null) {
            builder.AppendLine("else");
            if(!文生成16(statement.Else.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
        }
        return true;
    }

    /// <summary>
    /// <c>switch (expr) { case …: … }</c> 文を生成する。
    /// switch 対象式を <see cref="RenderExpressionForGeneratedMethod"/> でレンダリングし、
    /// 各 section のラベルはソーステキストをそのまま出力、
    /// 各 section の文は <see cref="文生成16"/> で再帰処理する。
    /// </summary>
    private static bool Switch文生成49(
        SwitchStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var exprText = RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"switch({exprText})");
        builder.AppendLine("{");
        foreach(var section in statement.Sections) {
            foreach(var label in section.Labels) {
                builder.AppendLine(label.ToString());
            }
            foreach(var sectionStatement in section.Statements) {
                if(!文生成16(sectionStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames)) return false;
            }
        }
        builder.AppendLine("}");
        return true;
    }

    /// <summary>
    /// 文中の LINQ 呼び出しを Fragment 実行コードに置き換えた等価な文を生成する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>クエリ式が含まれる場合は <see cref="TryLowerQueriesToExtensionMethods{TSyntax}"/> で拡張メソッドチェーンへ変換し、セマンティックモデルを差し替える。</description></item>
    ///   <item><description><c>ReturnStatement</c> の式がクエリ式なら <see cref="クエリ式本体生成"/> へ渡す。</description></item>
    ///   <item><description>文全体が単一 LINQ 呼び出しで戻り値を捨てる場合（列挙自体に副作用がない遅延評価）は文ごとスキップする。</description></item>
    ///   <item><description><see cref="外側LINQ呼び出し取得41"/> で「ラムダ内部ではなく文直下の LINQ 呼び出し」を出現順に収集する。</description></item>
    ///   <item><description>各呼び出しに対して <see cref="呼び出し式フラグメント生成1"/> でFragment を生成し、<c>__linq0</c>, <c>__linq1</c>... という一時変数名に束縛するコードを出力する。</description></item>
    ///   <item><description>文のテキストを <c>replacementMap</c>（正規化済み LINQ 式テキスト → 一時変数名）で置換し、最終的な文を出力する。</description></item>
    ///   <item><description>匿名型をレシーバーに持つ LINQ 呼び出しは Fragment 化せず System.Linq に委譲するため、置換マップへの登録をスキップして続行する。</description></item>
    /// </list>
    /// </summary>
    private static bool LINQ呼び出し差し替え文生成38(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var originalStatementForQueryLowering = statement;
        if(TryLowerQueriesToExtensionMethods(statement,semanticModel,containingType,out var loweredStatement,out var loweredSemanticModel,out var loweredContainingType)) {
            statement=loweredStatement;
            semanticModel=loweredSemanticModel;
            containingType=loweredContainingType;
            builder.AppendLine(クエリLowering確認コメント生成(originalStatementForQueryLowering,loweredStatement));
        }
        if(TryRewriteScalarAlgebra(statement,semanticModel,containingType,out var algebraStatement,out var algebraSemanticModel,out var algebraContainingType)) {
            statement=algebraStatement;
            semanticModel=algebraSemanticModel;
            containingType=algebraContainingType;
        }

        // ReturnStatement の式がクエリ式（括弧なしまたはあり）の場合は専用パスへ
        if(statement is ReturnStatementSyntax { Expression: { } retExpr }) {
            var retUnwrapped = retExpr;
            while(retUnwrapped is ParenthesizedExpressionSyntax retParen) {
                retUnwrapped = retParen.Expression;
            }
            if(retUnwrapped is QueryExpressionSyntax retQueryExpr) {
                return クエリ式本体生成(
                    retQueryExpr,semanticModel,containingType,ref uniqueNumber,
                    methodIsStatic,ref staticMethods,unsafeAccessorCollector,
                    generatedFragments,builder,ref linqInvocationCount,usedNames);
            }
        }
        // 式文の全体が単一 LINQ 呼び出しで結果が破棄される場合（e.g. Take(0).Select(...)）、
        // GetEnumerator() を呼ばないよう文ごとスキップする（元 LINQ の遅延評価と整合させる）。
        var outerLinqInvocations = 外側LINQ呼び出し取得41(statement,semanticModel);
        if(statement is ExpressionStatementSyntax discardedExprStatement
            && outerLinqInvocations.Count==1
            && NormalizeExpressionText(discardedExprStatement.Expression.ToString())==NormalizeExpressionText(outerLinqInvocations[0].ToString())) {
            linqInvocationCount++;
            return true;
        }
        if(statement is ExpressionStatementSyntax expressionStatement
            && outerLinqInvocations.Count==1
            && 直接Consumeループ文生成43(
                expressionStatement,
                outerLinqInvocations[0],
                semanticModel,
                containingType,
                ref uniqueNumber,
                methodIsStatic,
                ref staticMethods,
                unsafeAccessorCollector,
                generatedFragments,
                builder,
                ref linqInvocationCount,
                usedNames)) {
            return true;
        }

        var replacementMap = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var invocation in outerLinqInvocations) {
            var snapshot = uniqueNumber;
            var tempName = GetUniqueIdentifier($"__linq{linqInvocationCount++}", usedNames);
            if(!呼び出し式フラグメント生成1(semanticModel,invocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                // 匿名型がソースに含まれる場合 → Fragment化できないが元の System.Linq 式を維持して続行する。
                // これにより匿名型を返す LINQ の結果に対してチェーンする操作は System.Linq にフォールバックする。
                if(invocation.Expression is MemberAccessExpressionSyntax { Expression: var recv }
                    && semanticModel.GetTypeInfo(recv).Type is { } recvType
                    && ContainsAnonymousType(recvType)) {
                    continue;
                }
                return false;
            }
            if(!methodIsStatic) {
                fragment.PropagateContainingTypeName(containingType.ToDisplayString(完全修飾形式));
            }
            generatedFragments.Add(fragment);

            staticMethods=静的メソッド連結14(staticMethods,invocationStaticMethods);
            staticMethods=静的メソッド連結14(staticMethods,fragment.GetEnumeratorStruct);
            var resultTypeName = semanticModel.GetTypeInfo(invocation).Type?.ToDisplayString(完全修飾形式)
                ?? semanticModel.GetTypeInfo(invocation).ConvertedType?.ToDisplayString(完全修飾形式)
                ?? "object";
            if(!フラグメントEnumerator実行文追加37(builder,fragment,tempName,resultTypeName,usedNames)) {
                return false;
            }
            replacementMap[NormalizeExpressionText(invocation.ToString())]=tempName;
        }

        var generatedStatement = statement switch {
            LocalDeclarationStatementSyntax localDeclaration=>ローカル宣言生成17(localDeclaration,semanticModel,containingType,replacementMap,unsafeAccessorCollector),
            ExpressionStatementSyntax expressionStatement2=>$"{RenderExpressionForGeneratedMethod(expressionStatement2.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap)};",
            _=>RenderStatementForGeneratedMethod(statement,replacementMap),
        };
        builder.AppendLine(generatedStatement);
        return true;
    }

    /// <summary>
    /// 式本体メソッド（<c>=> expr</c>）の LINQ 呼び出しを置き換え、<c>return <em>result</em>;</c> を出力する。
    /// <br/><see cref="LINQ呼び出し差し替え文生成38"/> の式専用版。
    /// <c>SyntaxFactory.ReturnStatement()</c> で合成ノードを作ると <c>SyntaxTree</c> が null になり
    /// <c>SemanticModel.GetSymbolInfo()</c> が失敗するため、元ツリーのノードをそのまま使う。
    /// <br/>Fragment 化結果が匿名型を含む場合は生成コードに不正な型名が埋まるため、
    /// その呼び出しはスナップショットに戻してスキップする（<see cref="LINQ呼び出し差し替え文生成38"/> と同じ方針）。
    /// </summary>
    private static bool LINQ呼び出し差し替え文生成38_式本体(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var originalExpressionForQueryLowering = expression;
        if(TryLowerQueriesToExtensionMethods(expression,semanticModel,containingType,out var loweredExpression,out var loweredSemanticModel,out var loweredContainingType)) {
            expression=loweredExpression;
            semanticModel=loweredSemanticModel;
            containingType=loweredContainingType;
            builder.AppendLine(クエリLowering確認コメント生成(originalExpressionForQueryLowering,loweredExpression));
        }
        if(TryRewriteScalarAlgebra(expression,semanticModel,containingType,out var algebraExpression,out var algebraSemanticModel,out var algebraContainingType)) {
            expression=algebraExpression;
            semanticModel=algebraSemanticModel;
            containingType=algebraContainingType;
        }

        // 括弧を剥がしてクエリ式かどうかを判定する（例: (from v in Source() select v*2)）
        var unwrapped = expression;
        while(unwrapped is ParenthesizedExpressionSyntax paren) {
            unwrapped = paren.Expression;
        }
        if(unwrapped is QueryExpressionSyntax queryExpression) {
            return クエリ式本体生成(
                queryExpression,semanticModel,containingType,ref uniqueNumber,
                methodIsStatic,ref staticMethods,unsafeAccessorCollector,
                generatedFragments,builder,ref linqInvocationCount,usedNames);
        }

        var outerLinqInvocations = 外側LINQ呼び出し取得41_式(expression,semanticModel);
        var replacementMap = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var invocation in outerLinqInvocations) {
            var snapshot = uniqueNumber;
            var tempName = GetUniqueIdentifier($"__linq{linqInvocationCount++}",usedNames);
            if(!呼び出し式フラグメント生成1(semanticModel,invocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                linqInvocationCount--;
                if(invocation.Expression is MemberAccessExpressionSyntax { Expression: var recv }
                    && semanticModel.GetTypeInfo(recv).Type is { } recvType
                    && ContainsAnonymousType(recvType)) {
                    continue;
                }
                return false;
            }
            // fragment 生成が成功しても結果型が匿名型を含む場合は struct コードに不正な型名が埋まるためスキップ。
            // uniqueNumber をスナップショットに戻してカウントも戻す。
            var invResultType = semanticModel.GetTypeInfo(invocation).Type;
            if(invResultType is not null && ContainsAnonymousType(invResultType)) {
                uniqueNumber=snapshot;
                linqInvocationCount--;
                continue;
            }
            if(!methodIsStatic) {
                fragment.PropagateContainingTypeName(containingType.ToDisplayString(完全修飾形式));
            }
            generatedFragments.Add(fragment);
            staticMethods=静的メソッド連結14(staticMethods,invocationStaticMethods);
            staticMethods=静的メソッド連結14(staticMethods,fragment.GetEnumeratorStruct);
            var resultTypeName = semanticModel.GetTypeInfo(invocation).Type?.ToDisplayString(完全修飾形式)
                ?? semanticModel.GetTypeInfo(invocation).ConvertedType?.ToDisplayString(完全修飾形式)
                ?? "object";
            if(!フラグメントEnumerator実行文追加37(builder,fragment,tempName,resultTypeName,usedNames)) {
                return false;
            }
            replacementMap[NormalizeExpressionText(invocation.ToString())]=tempName;
        }

        var rendered = RenderExpressionForGeneratedMethod(
            expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        // RenderStatementForGeneratedMethod と同じ後処理：this. → @this. への変換
        rendered=rendered
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);
        builder.AppendLine($"return {rendered};");
        return true;
    }

    /// <summary>
    /// クエリ式（<c>from … where … select …</c>）を拡張メソッドチェーン（<c>.Where(…).Select(…)</c>）へ
    /// 変換（Lowering）し、変換後ノードのセマンティックモデルと含む型シンボルを返す。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>ノードにクエリ式が含まれなければ false を即返す。</description></item>
    ///   <item><description><c>OptimizeVisitor.RewriteQueriesToExtensionMethods</c> で構文木を書き換える。等価なら false。</description></item>
    ///   <item><description>書き換え済みノードに <c>TsavoriteQueryLowering</c> アノテーションを付与し、元の構文ツリーを新ツリーで置き換えた新 <see cref="Microsoft.CodeAnalysis.Compilation"/> を作成する。</description></item>
    ///   <item><description>新ツリーからアノテーション付きノードを探し、そのセマンティックモデルと包含型シンボルを out パラメータで返す。</description></item>
    /// </list>
    /// 成功時は呼び出し元が <c>statement</c> / <c>semanticModel</c> / <c>containingType</c> を差し替えて続行する。
    /// </summary>
    private static bool TryLowerQueriesToExtensionMethods<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax loweredNode,
        out SemanticModel loweredSemanticModel,
        out INamedTypeSymbol loweredContainingType)
        where TSyntax:SyntaxNode {
        loweredNode=node;
        loweredSemanticModel=semanticModel;
        loweredContainingType=containingType;

        if(!node.DescendantNodesAndSelf().OfType<QueryExpressionSyntax>().Any()) {
            return false;
        }

        var rewritten = OptimizeVisitor.RewriteQueriesToExtensionMethods(node);
        return TryRebindRewritten(node,rewritten,semanticModel,containingType,"TsavoriteQueryLowering",
            out loweredNode,out loweredSemanticModel,out loweredContainingType);
    }

    /// <summary>
    /// <see cref="AlgebraicRewriter"/> で §1 代数的書換（Where(p).Op()→Op(p)、OrderBy(k).First()→MinBy(k)、
    /// Count()&gt;0→Any()、恒等 Select / 冗長 AsEnumerable 除去、連続 Where の選択率順入替）を適用する。
    /// 合成書換（Count()&gt;0→Any() の後の Where(p).Any()→Any(p) 等）を取りこぼさないよう、変化が無くなるまで
    /// 「書換→再束縛」を繰り返す（拡張メソッド形の Enumerable 呼び出しのみが対象なので静的 SystemEnumerable 参照は不変）。
    /// </summary>
    private static bool TryRewriteScalarAlgebra<TSyntax>(
        TSyntax node,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out TSyntax rewrittenNode,
        out SemanticModel rewrittenSemanticModel,
        out INamedTypeSymbol rewrittenContainingType)
        where TSyntax:SyntaxNode {
        rewrittenNode=node;
        rewrittenSemanticModel=semanticModel;
        rewrittenContainingType=containingType;
        var changed=false;
        for(var iteration=0;iteration<8;iteration++) {
            var rewritten = AlgebraicRewriter.Rewrite(rewrittenNode,rewrittenSemanticModel);
            if(!TryRebindRewritten(rewrittenNode,rewritten,rewrittenSemanticModel,rewrittenContainingType,"TsavoriteAlgebraicRewrite",
                out var nextNode,out var nextModel,out var nextType)) {
                break;
            }
            rewrittenNode=nextNode;
            rewrittenSemanticModel=nextModel;
            rewrittenContainingType=nextType;
            changed=true;
        }
        return changed;
    }

    /// <summary>
    /// 書き換え済みノード <paramref name="rewritten"/> を元ツリーへ差し込み、新 <see cref="Compilation"/> を作って
    /// セマンティックモデルと包含型を取り直す共通処理。<paramref name="node"/> と等価なら false を返し、out は元の値のまま。
    /// </summary>
    private static bool TryRebindRewritten<TSyntax>(
        TSyntax node,
        SyntaxNode rewritten,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        string annotationKind,
        out TSyntax reboundNode,
        out SemanticModel reboundSemanticModel,
        out INamedTypeSymbol reboundContainingType)
        where TSyntax:SyntaxNode {
        reboundNode=node;
        reboundSemanticModel=semanticModel;
        reboundContainingType=containingType;
        if(SyntaxFactory.AreEquivalent(node,rewritten)) {
            return false;
        }

        var annotation = new SyntaxAnnotation(annotationKind);
        var annotated = rewritten.WithAdditionalAnnotations(annotation);
        var root = node.SyntaxTree.GetRoot();
        var newRoot = root.ReplaceNode(node,annotated);
        var newTree = newRoot.SyntaxTree;
        var newCompilation = semanticModel.Compilation.ReplaceSyntaxTree(node.SyntaxTree,newTree);
        var newModel = newCompilation.GetSemanticModel(newTree);
        var annotatedNode = newTree.GetRoot()
            .GetAnnotatedNodes(annotation)
            .OfType<TSyntax>()
            .SingleOrDefault();
        if(annotatedNode is null) {
            return false;
        }

        var typeDeclaration = annotatedNode.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if(typeDeclaration is null || newModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol newContainingType) {
            return false;
        }

        reboundNode=annotatedNode;
        reboundSemanticModel=newModel;
        reboundContainingType=newContainingType;
        return true;
    }

    private static string クエリLowering確認コメント生成(SyntaxNode original,SyntaxNode lowered) =>
        $"// Tsavorite query lowered: {NormalizeExpressionText(original.ToString())}=>{NormalizeExpressionText(lowered.ToString())}";

    // クエリ式を直接 Fragment チェーンへ変換し "return <result>;" を出力する旧フォールバック。
    // 通常は TryLowerQueriesToExtensionMethods でクエリ式を拡張メソッドチェーンへ下げてから既存の LINQ パスで処理する。
    private static bool クエリ式本体生成(
        QueryExpressionSyntax queryExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        bool methodIsStatic,
        ref string staticMethods,
        List<string> unsafeAccessorCollector,
        List<Fragment> generatedFragments,
        StringBuilder builder,
        ref int linqInvocationCount,
        HashSet<string> usedNames) {
        var fromClause = queryExpression.FromClause;
        var body = queryExpression.Body;

        // let / join は未対応（フォールバック）。group は SelectOrGroup で別途チェック。
        if(body.Clauses.Any(static c=>c is LetClauseSyntax or JoinClauseSyntax)) {
            return false;
        }
        if(body.SelectOrGroup is GroupClauseSyntax) {
            return false;
        }

        var paramName = fromClause.Identifier.ValueText;
        var sourceExpression = fromClause.Expression;

        // Source fragment を構築
        if(!単独列挙フラグメント生成10(semanticModel,sourceExpression,containingType,
            ref uniqueNumber,out var sourceFragment,out var sourceStaticMethods,unsafeAccessorCollector)) {
            return false;
        }
        staticMethods=静的メソッド連結14(staticMethods,sourceStaticMethods);
        Enumerable currentFragment = sourceFragment;

        // Where 節
        foreach(var clause in body.Clauses.OfType<WhereClauseSyntax>()) {
            var predBody = RenderExpressionForGeneratedMethod(
                clause.Condition,semanticModel,containingType,unsafeAccessorCollector);
            var whereFunc = new LambdaFunc {
                InlineBody = predBody,
                ParameterNames = [paramName],
                ParameterTypeNames = [currentFragment.ElementType],
                ReturnTypeName = "bool",
                CanInlineAssign = false,
                CanGenerateMethod = true,
            };
            if(!フラグメント候補選択25("Where",string.Empty,out var whereCandidate)) {
                return false;
            }
            var whereFragment = (Enumerable)whereCandidate.Constructor.Invoke(
                [uniqueNumber++,currentFragment,whereFunc]);
            staticMethods=静的メソッド連結14(staticMethods,whereFragment.GetEnumeratorStruct);
            currentFragment=whereFragment;
        }

        // OrderBy / ThenBy 節
        foreach(var clause in body.Clauses.OfType<OrderByClauseSyntax>()) {
            var orderings = clause.Orderings.ToList();
            for(var i = 0; i<orderings.Count; i++) {
                var ordering = orderings[i];
                var keyBody = RenderExpressionForGeneratedMethod(
                    ordering.Expression,semanticModel,containingType,unsafeAccessorCollector);
                var keyTypeName = semanticModel.GetTypeInfo(ordering.Expression).Type?
                    .ToDisplayString(完全修飾形式) ?? "object";
                var orderFunc = new LambdaFunc {
                    InlineBody = keyBody,
                    ParameterNames = [paramName],
                    ParameterTypeNames = [currentFragment.ElementType],
                    ReturnTypeName = keyTypeName,
                    CanInlineAssign = false,
                    CanGenerateMethod = true,
                };
                var isDescending = ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                var obMethodName = i==0
                    ? (isDescending ? "OrderByDescending" : "OrderBy")
                    : (isDescending ? "ThenByDescending" : "ThenBy");
                if(!フラグメント候補選択25(obMethodName,string.Empty,out var obCandidate)) {
                    return false;
                }
                var obFragment = (Enumerable)obCandidate.Constructor.Invoke(
                    [uniqueNumber++,currentFragment,orderFunc]);
                staticMethods=静的メソッド連結14(staticMethods,obFragment.GetEnumeratorStruct);
                currentFragment=obFragment;
            }
        }

        // Select 節（identity select は不要）
        if(body.SelectOrGroup is SelectClauseSyntax selectClause) {
            var selectExpr = selectClause.Expression;
            var isIdentitySelect = selectExpr is IdentifierNameSyntax { Identifier.ValueText: var sid } && sid==paramName;
            if(!isIdentitySelect) {
                var selectorBody = RenderExpressionForGeneratedMethod(
                    selectExpr,semanticModel,containingType,unsafeAccessorCollector);
                var resultTypeName2 = semanticModel.GetTypeInfo(selectExpr).Type?
                    .ToDisplayString(完全修飾形式) ?? "object";
                var selectFunc = new LambdaFunc {
                    InlineBody = selectorBody,
                    ParameterNames = [paramName],
                    ParameterTypeNames = [currentFragment.ElementType],
                    ReturnTypeName = resultTypeName2,
                    CanInlineAssign = true,
                    CanGenerateMethod = true,
                };
                if(!フラグメント候補選択25("Select",string.Empty,out var selectCandidate)) {
                    return false;
                }
                var selectFrag = (Enumerable)selectCandidate.Constructor.Invoke(
                    [uniqueNumber++,currentFragment,selectFunc]);
                staticMethods=静的メソッド連結14(staticMethods,selectFrag.GetEnumeratorStruct);
                currentFragment=selectFrag;
            }
        }

        var tempName2 = GetUniqueIdentifier($"__linq{linqInvocationCount++}",usedNames);
        generatedFragments.Add(currentFragment);
        var returnTypeName = semanticModel.GetTypeInfo(queryExpression).Type?.ToDisplayString(完全修飾形式)
            ?? semanticModel.GetTypeInfo(queryExpression).ConvertedType?.ToDisplayString(完全修飾形式)
            ?? "object";
        if(!フラグメントEnumerator実行文追加37(builder,currentFragment,tempName2,returnTypeName,usedNames)) {
            return false;
        }
        builder.AppendLine($"return {tempName2};");
        return true;
    }
    private static string OpenGenericTypeName(INamedTypeSymbol namedType) {
        var definitionName = namedType.OriginalDefinition.ToDisplayString(完全修飾形式);
        var typeArgumentStart = definitionName.IndexOf('<',StringComparison.Ordinal);
        return typeArgumentStart<0 ? definitionName : definitionName[..typeArgumentStart];
    }
    /// <summary>
    /// <c>T x = expr, y = expr2;</c> のようなローカル変数宣言文を生成する。
    /// 型名はセマンティックモデルで解決して完全修飾し、初期化式は
    /// <see cref="RenderExpressionForGeneratedMethod"/> と <paramref name="replacementMap"/> で LINQ 一時変数に置換する。
    /// <c>var</c> はそのまま出力する。
    /// </summary>
    private static string ローカル宣言生成17(
        LocalDeclarationStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        IReadOnlyDictionary<string,string>? replacementMap = null,
        List<string>? unsafeAccessorCollector = null) {
        var variables = statement.Declaration.Variables.Select(variable=>{
            if(variable.Initializer?.Value is null) {
                return variable.ToString();
            }
            return $"{variable.Identifier.ValueText}={RenderExpressionForGeneratedMethod(variable.Initializer.Value,semanticModel,containingType,unsafeAccessorCollector,replacementMap)}";
        });
        var declType=statement.Declaration.Type;
        string typeName;
        if(declType is IdentifierNameSyntax { IsVar: true }) {
            typeName="var";
        } else {
            var typeSymbol=semanticModel.GetTypeInfo(declType).Type;
            typeName=typeSymbol is not null
                ? typeSymbol.ToDisplayString(完全修飾形式)
                : NormalizeExpressionText(declType.ToString());
        }
        return $"{typeName} {string.Join(", ",variables)};";
    }
    /// <summary>
    /// 変数宣言部（<c>T x = expr</c>）のテキストを生成する。
    /// <see cref="ローカル宣言生成17"/> と同様だが、文末セミコロンを付けない点が異なる。
    /// <c>using (…)</c> や <c>for (…; …; …)</c> の初期化部で使う。
    /// </summary>
    private static string 変数宣言生成43(
        VariableDeclarationSyntax declaration,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var variables = declaration.Variables.Select(variable=>{
            if(variable.Initializer?.Value is null) {
                return variable.ToString();
            }
            return $"{variable.Identifier.ValueText}={RenderExpressionForGeneratedMethod(variable.Initializer.Value,semanticModel,containingType,unsafeAccessorCollector,replacementMap)}";
        });
        string typeName;
        if(declaration.Type is IdentifierNameSyntax { IsVar: true }) {
            typeName="var";
        } else {
            var typeSymbol=semanticModel.GetTypeInfo(declaration.Type).Type;
            typeName=typeSymbol is not null
                ? typeSymbol.ToDisplayString(完全修飾形式)
                : NormalizeExpressionText(declaration.Type.ToString());
        }
        return $"{typeName} {string.Join(", ",variables)}";
    }
    /// <summary>
    /// 文のテキストを <paramref name="replacementMap"/>（LINQ 式テキスト → 一時変数名）で置換し、
    /// <c>SystemEnumerable.</c> を <c>global::System.Linq.Enumerable.</c> に、
    /// <c>this.</c> を <c>@this.</c> に変換して返す。
    /// キーは長い順に置換することで部分一致による誤置換を防ぐ。
    /// </summary>
    private static string RenderStatementForGeneratedMethod(
        StatementSyntax statement,
        IReadOnlyDictionary<string,string>? replacementMap) {
        // replacementMap のキーは NormalizeExpressionText 済みなので text も正規化して照合する。
        var text = NormalizeExpressionText(statement.ToString());
        if(replacementMap is not null) {
            foreach(var pair in replacementMap.OrderByDescending(static pair=>pair.Key.Length)) {
                text=text.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
            }
        }
        return text
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);
    }
    /// <summary>
    /// 文の直下にある「最も外側の」 LINQ 拡張メソッド呼び出しを出現順に列挙する。
    /// <br/>除外条件:
    /// <list type="bullet">
    ///   <item><description>ラムダ式の内部にある呼び出し（ラムダ内の LINQ は別の Fragment 生成パスで処理される）。</description></item>
    ///   <item><description>別の LINQ 呼び出しの内側にある呼び出し（メソッドチェーンの途中ノードは最外ノードとして扱う）。</description></item>
    ///   <item><description>静的呼び出し（<c>ReducedFrom == null</c>）および out/ref パラメータを持つメソッド。</description></item>
    /// </list>
    /// </summary>
    private static List<InvocationExpressionSyntax> 外側LINQ呼び出し取得41(StatementSyntax statement,SemanticModel semanticModel) {
        return statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            // 拡張メソッド呼び出しのみを対象とし、out/ref パラメータ持ちは除外する
            .Where(invocation=>SystemLinqEnumerable呼び出し判定42(invocation,semanticModel)
                && semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol { ReducedFrom: not null } method
                && !(method.ReducedFrom ?? method).Parameters.Any(static p=>p.RefKind != RefKind.None))
            .Where(invocation=>!invocation.Ancestors()
                .TakeWhile(ancestor=>ancestor!=statement)
                .OfType<LambdaExpressionSyntax>()
                .Any())
            .Where(invocation=>!invocation.Ancestors()
                .TakeWhile(ancestor=>ancestor!=statement)
                .OfType<InvocationExpressionSyntax>()
                .Any(ancestorInvocation=>SystemLinqEnumerable呼び出し判定42(ancestorInvocation,semanticModel)))
            // レシーバーが静的 System.Linq.Enumerable 呼び出しかつ Fragment 対応メソッドの場合は傍受しない。
            // Fragment 対応メソッドは拡張メソッド形式 source.X(...) でも呼ばれるため、
            // static 形式 Enumerable.X(source,...) は "正解値" として元コードのまま保持する。
            // Fragment 非対応メソッド（Repeat/Range/Empty 等）は DuckTypingRoot として展開を継続する。
            .Where(invocation =>
                !(invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiver }
                && 括弧除去53(receiver) is InvocationExpressionSyntax receiverInvocation
                && semanticModel.GetSymbolInfo(receiverInvocation).Symbol is IMethodSymbol { ReducedFrom: null } receiverMethod
                && SystemLinqEnumerableメソッド判定38(receiverMethod)
                && Enumerableメソッドフラグメント対応判定32(receiverMethod.Name)))
            .OrderBy(static invocation=>invocation.SpanStart)
            .ToList();
    }
    /// <summary>
    /// 式本体版 <see cref="外側LINQ呼び出し取得41"/>。
    /// <c>SyntaxFactory.ReturnStatement(アロー式)</c> のような合成ノードを避け、元ツリーのノードを直接 root として使う。
    /// <br/>境界を <c>expression.Parent</c> にする理由:
    /// expression（例: <c>.ToArray()</c>）自体が最外の LINQ 呼び出しであるとき、
    /// 祖先走査の境界を expression にすると expression 自身が TakeWhile から抜け落ち、
    /// その直下の <c>.Select()</c> が「外側に LINQ なし」と誤判定されて二重生成される。
    /// statement 版は statement が境界なので問題ない。
    /// </summary>
    private static List<InvocationExpressionSyntax> 外側LINQ呼び出し取得41_式(ExpressionSyntax expression,SemanticModel semanticModel) {
        // expression 自身が最外の LINQ 呼び出しになりうる（例: 式本体=>xxx.Where(...).Select(...).ToArray()）。
        // 祖先走査の境界を expression ではなく expression.Parent にしないと、expression(=.ToArray()) 自身が
        // TakeWhile から抜け落ち、その直下の .Select() が「外側に LINQ なし」と誤判定され __linq1 として
        // 二重生成される。statement 版は statement が境界なので問題ないが、式本体版はここで明示的に1段上げる。
        var boundary = expression.Parent;
        return expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation=>SystemLinqEnumerable呼び出し判定42(invocation,semanticModel)
                && semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol { ReducedFrom: not null } method
                && !(method.ReducedFrom ?? method).Parameters.Any(static p=>p.RefKind != RefKind.None))
            .Where(invocation=>!invocation.Ancestors()
                .TakeWhile(ancestor=>ancestor!=boundary)
                .OfType<LambdaExpressionSyntax>()
                .Any())
            .Where(invocation=>!invocation.Ancestors()
                .TakeWhile(ancestor=>ancestor!=boundary)
                .OfType<InvocationExpressionSyntax>()
                .Any(ancestorInvocation=>SystemLinqEnumerable呼び出し判定42(ancestorInvocation,semanticModel)))
            // レシーバーが静的 System.Linq.Enumerable 呼び出しかつ Fragment 対応メソッドの場合は傍受しない。
            // Fragment 非対応メソッド（Repeat/Range/Empty 等）は DuckTypingRoot として展開を継続する。
            .Where(invocation =>
                !(invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiver }
                && 括弧除去53(receiver) is InvocationExpressionSyntax receiverInvocation
                && semanticModel.GetSymbolInfo(receiverInvocation).Symbol is IMethodSymbol { ReducedFrom: null } receiverMethod
                && SystemLinqEnumerableメソッド判定38(receiverMethod)
                && Enumerableメソッドフラグメント対応判定32(receiverMethod.Name)))
            .OrderBy(static invocation=>invocation.SpanStart)
            .ToList();
    }
    /// <summary>
    /// 呼び出し式が <c>System.Linq.Enumerable</c> または <c>System.MemoryExtensions.Contains</c> の
    /// メソッドであるかを判定する。
    /// <c>MemoryExtensions.Contains</c> は <c>Span&lt;T&gt;</c>/<c>ReadOnlySpan&lt;T&gt;</c> の
    /// 拡張メソッドとして LINQ 的に使われるため対象に含める。
    /// </summary>
    private static bool SystemLinqEnumerable呼び出し判定42(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }
        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType=="System.Linq.Enumerable"
            || (definition.Name=="Contains" && containingType=="System.MemoryExtensions");
    }
    /// <summary>Fragment 識別番号をメソッドごとに 0 からリセットする。</summary>
    private static int フラグメント番号初期値取得18()=>0;

    /// <summary>
    /// <c>Struct0</c>, <c>Struct1</c> のような補助型をメソッド単位の補助クラス
    /// <c>{methodName}_補助0_Struct0</c> 等にリネームし、クラス直下での名前衝突を回避する。
    /// <br/>アルゴリズム:
    /// <list type="number">
    ///   <item><description>補助メソッド群が空なら何もしない。</description></item>
    ///   <item><description>正規表現 <c>\bStruct\d+(_suffix)?\b</c> で <c>staticMethods</c> と <c>body</c> の両方を一括置換する。</description></item>
    ///   <item><description><see cref="補助Struct関数AggressiveInlining付与43"/> で <c>Compute</c>/<c>Build</c> メソッドに <c>AggressiveInlining</c> を付与する。</description></item>
    /// </list>
    /// </summary>
    private static void 静的メソッド群と本体を補助クラスへ退避21(string methodName,ref string staticMethods,ref string body) {
        if(string.IsNullOrWhiteSpace(staticMethods)) {
            return;
        }
        var prefix = $"{SanitizeIdentifier(methodName)}_補助0_";
        const string pattern = @"\bStruct(?<番号>\d+)(?<接尾辞>_[A-Za-z_][A-Za-z0-9_]*)?\b";
        staticMethods = System.Text.RegularExpressions.Regex.Replace(
            staticMethods,
            pattern,
            $"{prefix}Struct${{番号}}${{接尾辞}}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        body = System.Text.RegularExpressions.Regex.Replace(
            body,
            pattern,
            $"{prefix}Struct${{番号}}${{接尾辞}}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        staticMethods=補助Struct関数AggressiveInlining付与43(staticMethods);
    }
    /// <summary>
    /// 生成済み補助コード内の <c>private static … Compute(…)</c> / <c>private static … Build(…)</c>
    /// メソッドに <c>[MethodImpl(AggressiveInlining)]</c> 属性が付いていなければ挿入する。
    /// 正規表現で前行を確認し、既に属性がある場合は重複を避ける。
    /// </summary>
    private static string 補助Struct関数AggressiveInlining付与43(string source) {
        const string attribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
        return System.Text.RegularExpressions.Regex.Replace(
            source,
            @"(?m)^(?<indent>\s*)private static (?<declaration>.+\b(?:Compute|Build)\s*\()",
            match=>{
                var index = match.Index;
                var previousLineEnd = index>1 ? source.LastIndexOf('\n',index-2) : -1;
                var previousLine = source.Substring(previousLineEnd+1,index-(previousLineEnd+1)).Trim();
                if(previousLine==attribute) {
                    return match.Value;
                }
                return $"{match.Groups["indent"].Value}{attribute}\n{match.Value}";
            },
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
    // インターセプト対象メソッドで宣言・使用されているすべての識別子名を収集する。
    // 生成コードの変数名がユーザーコードと衝突しないようにするための前処理。
    // ラムダ/匿名メソッドの内部はラムダスコープであるため再帰を止め収集しない。
    private static HashSet<string> CollectMethodBodyIdentifiers(MethodDeclarationSyntax methodSyntax, IMethodSymbol method) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in method.Parameters)
            names.Add(p.Name);
        if(methodSyntax.Body is not null)
            CollectMethodScopeIdentifiers(methodSyntax.Body, names);
        return names;
    }
    /// <summary>
    /// ノード以下の識別子をすべて <paramref name="names"/> に収集する再帰ヘルパー。
    /// ラムダ／匿名メソッドに入ったら再帰を止める（別スコープなので生成変数の衝突に使わない）。
    /// 対象: 変数宣言子・foreach 変数・catch 宣言・パターンの単変数指定。
    /// </summary>
    private static void CollectMethodScopeIdentifiers(SyntaxNode node, HashSet<string> names) {
        // ラムダ/匿名メソッドは別スコープなので子孫まで降りない
        if(node is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax) return;
        switch(node) {
            case VariableDeclaratorSyntax v:
                names.Add(v.Identifier.ValueText); break;
            case ForEachStatementSyntax fe:
                names.Add(fe.Identifier.ValueText); break;
            case CatchDeclarationSyntax cd when cd.Identifier.ValueText is { Length: > 0 } cid:
                names.Add(cid); break;
            case SingleVariableDesignationSyntax svd:
                names.Add(svd.Identifier.ValueText); break;
        }
        foreach(var child in node.ChildNodes())
            CollectMethodScopeIdentifiers(child, names);
    }
    /// <summary>
    /// <paramref name="usedNames"/> に衝突しない一意な識別子を返し、使用済みセットに追加する。
    /// <br/>衝突した場合は <c>baseName_</c>、<c>baseName__</c>... と <c>_</c> を増やして試す。
    /// 選んだ名前を <paramref name="usedNames"/> に追加するため、同一パス内での二重取得を防ぐ。
    /// </summary>
    private static string GetUniqueIdentifier(string baseName, HashSet<string> usedNames) {
        if(usedNames.Add(baseName)) return baseName;
        var suffix = "_";
        while(true) {
            var candidate = $"{baseName}{suffix}";
            if(usedNames.Add(candidate)) return candidate;
            suffix += "_";
        }
    }

    /// <summary>
    /// 文字列を C# 識別子として使えるようにサニタイズする。
    /// 非英数字を <c>_</c> に置換し、先頭が識別子開始文字でない場合は先頭に <c>_</c> を挿入する。
    /// 空または空白のみの場合は <c>"Aggregate"</c> にフォールバックする。
    /// </summary>
    private static string SanitizeIdentifier(string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return "Aggregate";
        }

        var builder = new StringBuilder(value.Length);
        foreach(var c in value) {
            //不正な文字列は_に置換する
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if(builder.Length==0 || !SyntaxFacts.IsIdentifierStartCharacter(builder[0])) {
            builder.Insert(0,'_');
        }

        return builder.ToString();
    }

    /// <summary>グローバル名前空間は空文字列、それ以外は表示文字列を返す。</summary>
    private static string 名前空間テキスト取得(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.IsGlobalNamespace ? string.Empty : namespaceSymbol.ToDisplayString();
    }

    /// <summary>
    /// <paramref name="value"/> の各行先頭に <paramref name="indent"/> を付けて
    /// <paramref name="builder"/> に追記する。空行はインデントなしで出力する。
    /// CRLF・LF のどちらにも対応する。
    /// </summary>
    private static void インデント付き追記(StringBuilder builder,string value,string indent) {
        foreach(var line in value.Replace("\r\n","\n").Split('\n')) {
            if(line.Length==0) {
                builder.AppendLine();
                continue;
            }

            builder.Append(indent);
            builder.AppendLine(line);
        }
    }

    /// <summary>
    /// 文字列から空白のみの行を除去して返す。
    /// データベーススキーマの生成ソース出力でコンパクト化のために使う。
    /// </summary>
    private static string 空行除去(string source) {
        var lines = source.Replace("\r\n","\n").Split('\n');
        var builder = new StringBuilder(source.Length);
        var firstWritten = true;
        foreach(var line in lines) {
            if(string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            if(!firstWritten) {
                builder.Append('\n');
            }
            builder.Append(line);
            firstWritten=false;
        }
        return builder.ToString();
    }

    private static 型対象型? 型対象生成(GeneratorSyntaxContext context) {
        if(context.Node is not TypeDeclarationSyntax declaration) {
            return null;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration);
        if(symbol is null) {
            return null;
        }

        var isMarker = symbol.Name=="DatabaseGenerator";
        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        var isContainerCandidate = symbol.TypeKind==TypeKind.Class;
        if(!isMarker && !isPartial && !isContainerCandidate) {
            return null;
        }

        return new 型対象型(symbol,isPartial);
    }

    private static List<string> ローカルメソッド未対応Enumerable取得31(Linq傍受対象型 target) {
        // ローカルメソッド全体を再構成する前に、Fragment クラス自体が存在しない Enumerable メソッド名を列挙する。
        // 「System.Linq.Enumerable を含むメソッドは全部 intercept する」が前提なので、
        // 未対応メソッドは target から落とすのではなく、生成 interceptor から例外で見えるようにする。
        return target.LinqAnalysis.UnsupportedMethods.ToList();
    }

    private static bool Enumerableメソッドフラグメント対応判定32(string methodName)
       =>methodName=="Any" || フラグメント候補一覧.ContainsKey(methodName);

    private static Enumerable CreateRootEnumerableFragment(string sourceExpression,string elementType,string enumeratorTypeName,bool useEnumerableInterface,int 番号) {
        return new Root(
            番号,
            sourceExpression,
            elementType,
            enumeratorTypeName,
            useEnumerableInterface);
    }

    // ブロックが「単一の値付き return 文だけ」かどうか。これに該当するものは式インライン（LambdaFunc）で扱う。
    private static bool IsSingleReturnBlock(BlockSyntax block) =>
        block.Statements is [ReturnStatementSyntax { Expression: not null }];

    /// <summary><c>delegate(params) {}</c> のパラメータ名リストを返す。引数リストがない場合は空リスト。</summary>
    private static List<string> GetAnonymousMethodParameters(AnonymousMethodExpressionSyntax anonymousMethod) =>
        anonymousMethod.ParameterList?.Parameters
            .Select(static parameter=>parameter.Identifier.ValueText)
            .ToList() ?? [];

    // 複数文ブロック本体（try-catch 含む）を文インライン展開する BlockFunc を構築する。
    // return 文は __Tsavorite_DEST__ への代入 + goto __Tsavorite_LABEL__ へ書き換える。
    private static bool TryBuildBlockFunc(
        BlockSyntax block,
        IReadOnlyList<string> parameters,
        ITypeSymbol delegateType,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType,
        out BlockFunc fragment) {
        fragment=null!;
        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument=>typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Count!=parameterTypeNames.Length) {
            return false;
        }

        // return をプレースホルダ代入 + goto に書き換える。ネストした lambda/local function には降りない。
        var rewritten = (BlockSyntax)new ReturnToResultRewriter().Visit(block)!;
        var bodyText = string.Join(
            "\n",
            rewritten.Statements.Select(statement=>NormalizeGeneratedExpressionText(statement.ToString())));

        var capturedLocals = Array.Empty<(string Name, string TypeName)>();
        if(semanticModel is not null && block.Statements.Count>0) {
            var dataFlow = semanticModel.AnalyzeDataFlow(block.Statements[0],block.Statements[^1]);
            if(dataFlow?.Succeeded==true) {
                var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
                capturedLocals = dataFlow.DataFlowsIn
                    .Where(sym=>(sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name!="this")
                    .Select(sym=>(sym.Name, sym is ILocalSymbol loc
                        ? loc.Type.ToDisplayString(完全修飾形式)
                        : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                    .Distinct()
                    .ToArray();
            }
        }

        fragment = new BlockFunc {
            ReturnTypeName = typeArguments[^1],
            ParameterTypeNames = parameterTypeNames,
            ParameterNames = parameters.ToArray(),
            CanGenerateMethod = false,
            CanInlineAssign = true,
            RequiresStatementInlining = true,
            BlockBody = bodyText,
            CapturedLocals = capturedLocals,
        };
        return true;
    }

    // ブロック本体内の return 文を __Tsavorite_DEST__ = (expr); goto __Tsavorite_LABEL__; へ書き換える。
    // ネストした lambda / 匿名メソッド / local function の return は対象外（中へ降りない）。
    private sealed class ReturnToResultRewriter : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)=>node;
        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)=>node;
        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)=>node;
        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)=>node;
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node) {
            var statementText = node.Expression is { } expr
                ? $"{{ __Tsavorite_DEST__ = ({expr}); goto __Tsavorite_LABEL__; }}"
                : "{ goto __Tsavorite_LABEL__; }";
            return SyntaxFactory.ParseStatement(statementText);
        }
    }

    /// <summary>
    /// ラムダ式から <see cref="LambdaSpec"/>（戻り値型・パラメータ型・パラメータ名・本体）を抽出する。
    /// 本体が式本体か単一 <c>return</c> 文の場合のみ成功する。
    /// デリゲート型が <c>Func&lt;…&gt;</c> でない場合・匿名型が混在する場合も失敗を返す。
    /// </summary>
    private static bool TryCreateLambdaFuncFragment(LambdaExpressionSyntax lambda,ITypeSymbol delegateType,out LambdaSpec fragment) {
        fragment=null!;
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }

        var parameters = GetLambdaParameters(lambda);
        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument=>typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Count!=parameterTypeNames.Length) {
            return false;
        }

        fragment=new LambdaSpec(
            typeArguments[^1],
            parameterTypeNames,
            parameters.ToArray(),
            NormalizeExpressionText(body.ToString()));
        return true;
    }

    /// <summary>
    /// <c>delegate(params) { return expr; }</c> の単一 return 本体から <see cref="LambdaSpec"/> を抽出する。
    /// 複数文ブロックや戻り値なし本体は失敗する（<see cref="TryBuildBlockFunc"/> の対象になる）。
    /// </summary>
    private static bool TryCreateAnonymousMethodFuncFragment(
        AnonymousMethodExpressionSyntax anonymousMethod,
        ITypeSymbol delegateType,
        out LambdaSpec fragment,
        out ExpressionSyntax body) {
        fragment=null!;
        body=null!;

        if(anonymousMethod.Block.Statements is not [
            ReturnStatementSyntax { Expression: { } returnExpression }
        ]) {
            return false;
        }

        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var parameters = anonymousMethod.ParameterList?.Parameters
            .Select(static parameter=>parameter.Identifier.ValueText)
            .ToArray() ?? [];
        var typeArguments = funcType.TypeArguments.Select(typeArgument=>typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Length!=parameterTypeNames.Length) {
            return false;
        }

        body=returnExpression;
        fragment=new LambdaSpec(
            typeArguments[^1],
            parameterTypeNames,
            parameters,
            NormalizeExpressionText(returnExpression.ToString()));
        return true;
    }

    private static bool IsInlineDelegateType(INamedTypeSymbol namedType) {
        // SelectMany では selector が IEnumerable<T> を返すため、
        // 戻り値がシーケンスでも FuncFragment 化できるようにしておく。
        // 実際にどの Fragment で受け入れるかは後段の候補選択側で決まる。
        return namedType.ContainingNamespace.ToDisplayString()=="System"
            && namedType.MetadataName is "Func`2" or "Func`3";
    }

    /// <summary>型が <c>System.Collections.Generic.IEnumerable&lt;T&gt;</c> そのものかを判定する。</summary>
    private static bool IsIEnumerableType(INamedTypeSymbol namedType)
       =>namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            && namedType.MetadataName=="IEnumerable`1";

    /// <summary>型またはその実装インターフェースに非ジェネリック <c>System.Collections.IEnumerable</c> が含まれるかを判定する。</summary>
    private static bool IsNonGenericIEnumerableType(ITypeSymbol? type)
       =>type is INamedTypeSymbol namedType
            && (IsNonGenericIEnumerableType(namedType)
                || namedType.AllInterfaces.Any(IsNonGenericIEnumerableType));

    /// <summary>型が <c>System.Collections.IEnumerable</c>（非ジェネリック）そのものかを判定する。</summary>
    private static bool IsNonGenericIEnumerableType(INamedTypeSymbol namedType)
       =>namedType.ContainingNamespace.ToDisplayString()=="System.Collections"
            && namedType.MetadataName=="IEnumerable";

    private static bool ContainsAnonymousType(ITypeSymbol type) {
        if(type.IsAnonymousType) {
            return true;
        }

        // 配列やジェネリックの内側に匿名型が埋まっていても生成署名には出せないので再帰的に見る。
        return type switch {
            IArrayTypeSymbol arrayType=>ContainsAnonymousType(arrayType.ElementType),
            INamedTypeSymbol namedType=>namedType.TypeArguments.Any(ContainsAnonymousType),
            _=>false,
        };
    }

    // 匿名型を "TResult" ジェネリック型パラメータに置き換えた型名を返す。
    // 例: TAnon → "TResult", IEnumerable<TAnon> → "global::System.Collections.Generic.IEnumerable<TResult>"
    private static string ReplaceAnonymousTypesWithTResult(ITypeSymbol type) {
        if(type.IsAnonymousType) return "TResult";
        if(type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsAnonymousType)) {
            var openName = OpenGenericTypeName(named);
            var args = string.Join(", ", named.TypeArguments.Select(a =>
                ContainsAnonymousType(a) ? ReplaceAnonymousTypesWithTResult(a) : a.ToDisplayString(完全修飾形式)));
            return $"{openName}<{args}>";
        }
        return type.ToDisplayString(完全修飾形式);
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) {
        // 開いた型パラメータが残っていると生成メソッド側の署名が閉じないため再帰的に検査する。
        return type switch {
            ITypeParameterSymbol=>true,
            IArrayTypeSymbol arrayType=>ContainsTypeParameter(arrayType.ElementType),
            INamedTypeSymbol namedType=>namedType.TypeArguments.Any(ContainsTypeParameter),
            IPointerTypeSymbol pointerType=>ContainsTypeParameter(pointerType.PointedAtType),
            _=>false,
        };
    }

    // メソッド自身の型パラメーター（ownParams）以外の開いた型パラメーターが型に含まれるか検査する。
    // メソッドレベルのジェネリックパラメーターのみを持つ型は生成シグネチャに出せるため通過させる。
    private static bool ContainsExternalTypeParameter(ITypeSymbol type,ImmutableArray<ITypeParameterSymbol> ownParams) {
        if(ownParams.IsDefaultOrEmpty) {
            return ContainsTypeParameter(type);
        }
        var ownNames = new HashSet<string>(ownParams.Select(static p=>p.Name),StringComparer.Ordinal);
        return ContainsExternalTypeParameterCore(type,ownNames);
    }

    private static bool ContainsExternalTypeParameterCore(ITypeSymbol type,HashSet<string> ownNames) => type switch {
        ITypeParameterSymbol tp=>!ownNames.Contains(tp.Name),
        IArrayTypeSymbol arr=>ContainsExternalTypeParameterCore(arr.ElementType,ownNames),
        INamedTypeSymbol named=>named.TypeArguments.Any(a=>ContainsExternalTypeParameterCore(a,ownNames)),
        _=>false,
    };

    private static bool ContainsGeneratedTypeAccessibilityRisk(ITypeSymbol type) {
        // 生成クラスから見えない型がどこかに混ざると署名生成時点でコンパイル不能になる。
        return type switch {
            IArrayTypeSymbol arrayType=>ContainsGeneratedTypeAccessibilityRisk(arrayType.ElementType),
            IPointerTypeSymbol pointerType=>ContainsGeneratedTypeAccessibilityRisk(pointerType.PointedAtType),
            INamedTypeSymbol namedType=>NamedTypeAccessibilityRisk(namedType) || namedType.TypeArguments.Any(ContainsGeneratedTypeAccessibilityRisk),
            _=>false,
        };
    }

    private static bool NamedTypeAccessibilityRisk(INamedTypeSymbol namedType) {
        if(namedType.IsAnonymousType) {
            return true;
        }

        if(namedType.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal) {
            return true;
        }

        return namedType.ContainingType is not null && NamedTypeAccessibilityRisk(namedType.ContainingType);
    }

    /// <summary>
    /// ラムダの本体を式として取り出す。
    /// 式本体ラムダ（<c>x =&gt; expr</c>）はそのまま、単一 <c>return</c> 文ブロック（<c>x =&gt; { return expr; }</c>）は
    /// return の式を返す。それ以外は false。
    /// </summary>
    private static bool TryGetLambdaExpressionBody(LambdaExpressionSyntax lambda,out ExpressionSyntax body) {
        body=null!;
        if(lambda.Body is ExpressionSyntax expressionBody) {
            body=expressionBody;
            return true;
        }
        if(lambda.Body is BlockSyntax {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            body=returnExpression;
            return true;
        }
        return false;
    }

    /// <summary>ラムダ式のパラメータ名リストを返す。単純ラムダは 1 要素、括弧ラムダは全パラメータ名。</summary>
    private static List<string> GetLambdaParameters(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simpleLambda) {
            return [simpleLambda.Parameter.Identifier.ValueText];
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda) {
            return parenthesizedLambda.ParameterList.Parameters.Select(static parameter=>parameter.Identifier.ValueText).ToList();
        }
        return [];
    }

    /// <summary>改行を空白に置換してトリミングする。置換マップのキー正規化に使う。</summary>
    private static string NormalizeExpressionText(string value) {
        return value.Replace("\r\n"," ").Replace('\n',' ').Trim();
    }

    /// <summary>
    /// <see cref="NormalizeExpressionText"/> に加え、
    /// <c>SystemEnumerable.</c> → <c>global::System.Linq.Enumerable.</c> および <c>this.</c> → <c>@this.</c> の変換も行う。
    /// 生成コードへ埋め込む式テキストの標準化に使う。
    /// </summary>
    private static string NormalizeGeneratedExpressionText(string value) =>
        NormalizeExpressionText(value)
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);

    private static string RenderArgumentForGeneratedMethod(
        ArgumentSyntax argument,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var prefix = RefKind接頭辞取得35(argument.RefKindKeyword.Kind());
        return $"{prefix}{RenderExpressionForGeneratedMethod(argument.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap)}";
    }

    private static string RenderTypeArgumentList(IMethodSymbol method) {
        if(method.TypeArguments.Length==0) {
            return string.Empty;
        }

        return $"<{string.Join(", ",method.TypeArguments.Select(static type=>type.ToDisplayString(完全修飾形式)))}>";
    }

    /// <summary>
    /// コンパイル時定数値を C# リテラルテキストに変換する。
    /// 浮動小数は <c>"R"</c> 書式でラウンドトリップ保証、文字列は逐語的文字列リテラル (<c>@"…"</c>) に変換する。
    /// </summary>
    private static string RenderConstantValue(object? value)=>value switch {
        null=>"null",
        string text=>"@\"" + text.Replace("\"","\"\"",StringComparison.Ordinal) + "\"",
        char ch=>"'" + ch.ToString().Replace("\\","\\\\",StringComparison.Ordinal).Replace("'","\\'",StringComparison.Ordinal) + "'",
        bool boolean=>boolean ? "true" : "false",
        float single=>single.ToString("R",System.Globalization.CultureInfo.InvariantCulture) + "f",
        double number=>number.ToString("R",System.Globalization.CultureInfo.InvariantCulture) + "d",
        decimal number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
        long number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
        ulong number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL",
        uint number=>number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "U",
        _=>Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture) ?? "default!",
    };

    private static string RefKind接頭辞取得35(SyntaxKind syntaxKind)=>syntaxKind switch {
        SyntaxKind.RefKeyword=>"ref ",
        SyntaxKind.OutKeyword=>"out ",
        SyntaxKind.InKeyword=>"in ",
        _=>string.Empty,
    };

    /// <summary>
    /// 式の構文ノードを生成コードとして出力可能なテキストに変換する。
    /// <br/>アルゴリズム（ノード種別ごとの処理を 1 回の <c>DescendantNodesAndSelf()</c> 走査でまとめて行う）:
    /// <list type="number">
    ///   <item><description>テキストを <see cref="NormalizeGeneratedExpressionText"/> で正規化し、<paramref name="replacementMap"/> で LINQ 一時変数に置換する（長いキーを先に処理して部分一致誤置換を防ぐ）。</description></item>
    ///   <item><description><b>呼び出し式</b>: <c>nameof(…)</c> は評価済み文字列リテラルに変換。
    ///     非拡張メソッドは <see cref="TryRenderOrdinaryMethodInvocationExpression"/> でインライン化または UnsafeAccessor 化。
    ///     非 System.Linq 拡張メソッドは <see cref="TryRenderExtensionMethodInvocationExpression"/> でインライン化。
    ///     失敗した場合は <see cref="RenderInlineFailureInvocationExpression"/> で例外式に置換。</description></item>
    ///   <item><description><b>識別子</b>: 非 public 静的フィールドはコンスタントなら値展開し、それ以外は UnsafeAccessor 経由の参照に変換する。</description></item>
    ///   <item><description><b>メンバーアクセス（メソッドグループ）</b>: 非 public メソッドへのデリゲート参照を UnsafeAccessor ラムダに変換する。</description></item>
    ///   <item><description><b>オブジェクト生成</b>: 型名を完全修飾名に置換して生成 namespace での参照エラーを防ぐ。</description></item>
    ///   <item><description><b>メンバーアクセス（フィールド）</b>: 非 public フィールドを UnsafeAccessor 経由の参照に変換する。</description></item>
    ///   <item><description><b>メンバーアクセス（プロパティ）</b>: 非 public インスタンスプロパティの getter を <c>get_X</c> UnsafeAccessor 呼び出しに変換する。</description></item>
    ///   <item><description><b>パラメータ型</b>: ネストしたラムダのパラメータ型名を完全修飾名に置換する。</description></item>
    /// </list>
    /// </summary>
    private static string RenderExpressionForGeneratedMethod(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var text = NormalizeGeneratedExpressionText(expression.ToString());

        if(replacementMap is not null) {
            foreach(var pair in replacementMap.OrderByDescending(static pair=>pair.Key.Length)) {
                text=text.Replace(NormalizeGeneratedExpressionText(pair.Key),pair.Value,StringComparison.Ordinal);
            }
        }

        // 仕様メモ:
        // - 拡張メソッド呼び出し（ReducedFrom != null, Enumerable以外）は本文を式へインライン化する。
        // - 普通メソッド呼び出し（非拡張）はインライン化しない。生成コードから呼べない非 public は UnsafeAccessor で元呼び出しを維持する。
        // - インライン化対象を元呼び出しへ黙って戻す経路は作らず、未対応は生成コード上の例外式にする。
        // 最適化: DescendantNodesAndSelf() を 1 回だけ実行し、すべてのノード種別を一括収集する。
        var allNodes = expression.DescendantNodesAndSelf().ToList();
        var allInvocations = new List<InvocationExpressionSyntax>(allNodes.Count);
        var allIdentifiers = new List<IdentifierNameSyntax>(allNodes.Count);
        var allMemberAccesses = new List<MemberAccessExpressionSyntax>(allNodes.Count);
        var allObjectCreations = new List<ObjectCreationExpressionSyntax>(allNodes.Count);
        var allParameters = new List<ParameterSyntax>(allNodes.Count);
        foreach(var node in allNodes) {
            switch(node) {
                case InvocationExpressionSyntax inv: allInvocations.Add(inv); break;
                case IdentifierNameSyntax id: allIdentifiers.Add(id); break;
                case MemberAccessExpressionSyntax ma: allMemberAccesses.Add(ma); break;
                case ObjectCreationExpressionSyntax oc: allObjectCreations.Add(oc); break;
                case ParameterSyntax param: allParameters.Add(param); break;
            }
        }

        var processedInvocations = new HashSet<InvocationExpressionSyntax>(ReferenceEqualityComparer.Instance);
        var processedInvocationTexts = new HashSet<string>(StringComparer.Ordinal);
        foreach(var invocation in allInvocations) {
            if(processedInvocations.Contains(invocation)) continue;

            // nameof() は生成 namespace ではスコープ外になる識別子を参照しうるため、文字列リテラルへ評価する。
            if(invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" }
                && invocation.ArgumentList.Arguments.Count==1) {
                var nameofArg = invocation.ArgumentList.Arguments[0].Expression;
                var nameofSymbol = semanticModel.GetSymbolInfo(nameofArg).Symbol;
                var nameString = nameofSymbol?.Name
                    ?? (nameofArg is MemberAccessExpressionSyntax maMember ? maMember.Name.Identifier.Text : nameofArg.ToString());
                var nameofText = NormalizeGeneratedExpressionText(invocation.ToString());
                text=text.Replace(nameofText,$"\"{nameString}\"",StringComparison.Ordinal);
                continue;
            }

            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || (method.ReducedFrom is null && method.MethodKind!=MethodKind.Ordinary)) {
                continue;
            }

            // text は先頭で this. → @this. 変換済みなので invocationText も同様に変換する。
            var invocationText = NormalizeGeneratedExpressionText(invocation.ToString());

            // 同じ invocationText を2回以上処理すると二重置換が発生するため、1回だけ処理する。
            if(!processedInvocationTexts.Add(invocationText)) {
                continue;
            }

            if(method.ReducedFrom is null) {
                if(TryRenderOrdinaryMethodInvocationExpression(method,invocation,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var ordinaryInvocationText)) {
                    text=text.Replace(invocationText,ordinaryInvocationText,StringComparison.Ordinal);
                    foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
                    continue;
                }

                text=text.Replace(invocationText,RenderInlineFailureInvocationExpression(method),StringComparison.Ordinal);
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
            } else if(method.ReducedFrom is not null
                && (method.ReducedFrom ?? method).ContainingType.ToDisplayString() != "System.Linq.Enumerable"
                && invocation.Expression is MemberAccessExpressionSyntax extensionMemberAccess) {
                if(TryRenderExtensionMethodInvocationExpression(method,invocation,extensionMemberAccess,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var inlineExtensionText)) {
                    text=text.Replace(invocationText,inlineExtensionText,StringComparison.Ordinal);
                } else {
                    text=text.Replace(invocationText,RenderInlineFailureInvocationExpression(method),StringComparison.Ordinal);
                }
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
            }
        }

        foreach(var identifier in allIdentifiers) {
            if(identifier.Parent is ArgumentSyntax argument
                && argument.Expression==identifier
                && semanticModel.GetSymbolInfo(identifier).Symbol is IMethodSymbol methodGroup
                && methodGroup.MethodKind==MethodKind.Ordinary
                && SymbolEqualityComparer.Default.Equals(methodGroup.ContainingType,containingType)) {
                var methodGroupText=NormalizeExpressionText(identifier.ToString());
                var replacement=$"@this.{methodGroup.Name}";
                if(methodGroup.IsStatic) {
                    replacement=$"{methodGroup.ContainingType.ToDisplayString(完全修飾形式)}.{methodGroup.Name}";
                }else{
                }
                text=System.Text.RegularExpressions.Regex.Replace(
                    text,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(methodGroupText)}\b",
                    replacement,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                continue;
            }

            if(semanticModel.GetSymbolInfo(identifier).Symbol is not IFieldSymbol field
                || !field.IsStatic
                || !SymbolEqualityComparer.Default.Equals(field.ContainingType,containingType)
                || field.DeclaredAccessibility==Accessibility.Public) {
                continue;
            }

            var identifierText = NormalizeExpressionText(identifier.ToString());
            if(field.HasConstantValue) {
                text=System.Text.RegularExpressions.Regex.Replace(
                    text,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(identifierText)}\b",
                    RenderConstantValue(field.ConstantValue),
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                continue;
            }

            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(field.ContainingType.ToDisplayString(完全修飾形式)),
                SyntaxFactory.IdentifierName(field.Name));
            var fieldExpression = RenderFieldForwardExpression(field,memberAccess,semanticModel,unsafeAccessorCollector);
            text=System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(identifierText)}\b",
                fieldExpression,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        foreach(var memberAccess in allMemberAccesses) {
            if(semanticModel.GetSymbolInfo(memberAccess).Symbol is not IMethodSymbol method
                || method.MethodKind!=MethodKind.Ordinary
                || method.DeclaredAccessibility==Accessibility.Public
                || semanticModel.GetTypeInfo(memberAccess).ConvertedType is not INamedTypeSymbol delegateType
                || !IsInlineDelegateType(delegateType)) {
                continue;
            }

            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            var receiver = method.IsStatic
                ? null
                : NormalizeExpressionText(memberAccess.Expression.ToString()).Replace("this","@this",StringComparison.Ordinal);
            text=text.Replace(
                memberText,
                RenderDelegateCreateExpression(method,delegateType,receiver,unsafeAccessorCollector),
                StringComparison.Ordinal);
        }

        foreach(var objectCreation in allObjectCreations) {
            var typeSymbol = semanticModel.GetTypeInfo(objectCreation.Type).Type;
            if(typeSymbol is null) {
                continue;
            }

            var originalTypeText = NormalizeExpressionText(objectCreation.Type.ToString());
            var qualifiedTypeText = typeSymbol.ToDisplayString(完全修飾形式);
            if(originalTypeText==qualifiedTypeText || originalTypeText.StartsWith("global::",StringComparison.Ordinal)) {
                continue;
            }

            text=ReplaceTypeNameForGeneratedMethod(text,originalTypeText,qualifiedTypeText);
        }

        foreach(var memberAccess in allMemberAccesses) {
            if(semanticModel.GetSymbolInfo(memberAccess).Symbol is not IFieldSymbol field
                || field.DeclaredAccessibility==Accessibility.Public) {
                continue;
            }

            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            text=text.Replace(
                memberText,
                RenderFieldForwardExpression(field,memberAccess,semanticModel,unsafeAccessorCollector),
                StringComparison.Ordinal);
        }

        // private インスタンスプロパティへのアクセスを UnsafeAccessor (Method "get_X") 経由に変換する。
        foreach(var memberAccess in allMemberAccesses) {
            if(semanticModel.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol property
                || property.IsStatic
                || property.DeclaredAccessibility==Accessibility.Public
                || !SymbolEqualityComparer.Default.Equals(property.ContainingType,containingType)
                || property.GetMethod is null) {
                continue;
            }
            var memberText = NormalizeExpressionText(memberAccess.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            var accessorName = $"__Property_{SanitizeIdentifier(property.ContainingType.Name)}_{SanitizeIdentifier(property.Name)}";
            var targetTypeName = property.ContainingType.ToDisplayString(完全修飾形式);
            var propTypeName = property.Type.ToDisplayString(完全修飾形式);
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_{property.Name}\")]\nprivate static extern {propTypeName} {accessorName}({targetTypeName} @this);";
            unsafeAccessorCollector?.Add(accessorCode);
            var receiverText = NormalizeExpressionText(memberAccess.Expression.ToString())
                .Replace("this","@this",StringComparison.Ordinal);
            text=text.Replace(memberText,$"{accessorName}({receiverText})",StringComparison.Ordinal);
        }
        foreach(var param in allParameters) {
            if(param.Type is null) continue;
            var typeSymbol=semanticModel.GetTypeInfo(param.Type).Type;
            if(typeSymbol is null) continue;
            var qualifiedName=typeSymbol.ToDisplayString(完全修飾形式);
            var originalName=NormalizeExpressionText(param.Type.ToString());
            if(originalName==qualifiedName||originalName.StartsWith("global::",StringComparison.Ordinal)) continue;
            text=ReplaceTypeNameForGeneratedMethod(text,originalName,qualifiedName);
        }
        return text;
    }
    /// <summary>呼び出し式が明示的な型引数リストを持つかどうか（例: <c>Cast&lt;int&gt;(…)</c>）。</summary>
    private static bool InvocationHasExplicitTypeArguments(InvocationExpressionSyntax invocation) =>
        invocation.Expression is GenericNameSyntax
        || invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax };

    /// <summary>
    /// 通常メソッド呼び出し（非拡張）を生成コード用にレンダリングする。
    /// <br/>public/internal メソッドは完全修飾名で直接呼び出し、
    /// 非 public メソッドは <c>UnsafeAccessor</c> 経由のアクセサ宣言を生成して呼び出す。
    /// static / インスタンスでアクセサの生成方法が異なる（<c>StaticMethod</c> / <c>Method</c>）。
    /// ジェネリックかつ非 public のメソッドは <c>UnsafeAccessor</c> で型引数を渡せないため失敗を返す。
    /// </summary>
    private static bool TryRenderOrdinaryMethodInvocationExpression(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out string expressionText) {
        expressionText=string.Empty;
        if(method.IsGenericMethod
            && method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)) {
            return false;
        }
        if(!TryCreateOrdinaryActualArgumentList(method,invocation,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var actualArguments)) {
            return false;
        }
        var explicitTypeArgs = InvocationHasExplicitTypeArguments(invocation) ? RenderTypeArgumentList(method) : string.Empty;
        var argumentList = string.Join(", ",actualArguments);
        var declaringType = method.ContainingType.ToDisplayString(完全修飾形式);
        var canCallDirectly = method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;
        if(method.IsStatic) {
            if(canCallDirectly) {
                // 生成 namespace は元 namespace と異なるため、静的メソッド呼び出しは型名を完全修飾する。
                expressionText=$"{declaringType}.{method.Name}{explicitTypeArgs}({argumentList})";
                return true;
            }
            var accessorName = $"__StaticMethod_{SanitizeIdentifier(method.ContainingType.Name)}_{SanitizeIdentifier(method.Name)}";
            var returnTypeName = method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(完全修飾形式);
            var paramParts = new List<string> { $"{declaringType}? _" };
            foreach(var param in method.Parameters) {
                paramParts.Add(ParameterDeclarationForAccessor(param));
            }
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({string.Join(", ",paramParts)});";
            unsafeAccessorCollector?.Add(accessorCode);
            expressionText=$"{accessorName}(null{(actualArguments.Count>0 ? ", " + argumentList : string.Empty)})";
            return true;
        }
        if(!TryRenderOrdinaryReceiverExpression(invocation,semanticModel,containingType,unsafeAccessorCollector,replacementMap,out var receiverText)) {
            return false;
        }
        if(canCallDirectly) {
            expressionText=$"{receiverText}.{method.Name}{explicitTypeArgs}({argumentList})";
            return true;
        }
        var instanceAccessorName = $"__Method_{SanitizeIdentifier(method.ContainingType.Name)}_{SanitizeIdentifier(method.Name)}";
        var instanceReturnTypeName = method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(完全修飾形式);
        var instanceParamParts = new List<string> { $"{declaringType} @this" };
        foreach(var param in method.Parameters) {
            instanceParamParts.Add(ParameterDeclarationForAccessor(param));
        }
        var instanceAccessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"{method.Name}\")]\nprivate static extern {instanceReturnTypeName} {instanceAccessorName}({string.Join(", ",instanceParamParts)});";
        unsafeAccessorCollector?.Add(instanceAccessorCode);
        expressionText=$"{instanceAccessorName}({receiverText}{(actualArguments.Count>0 ? ", " + argumentList : string.Empty)})";
        return true;
    }
    /// <summary>
    /// 通常メソッドの実引数リストを構築する。
    /// <br/>名前付き引数に対応するため位置引数とスロット配列で処理する。
    /// 省略可能パラメータは params 配列を空配列で補い、デフォルト値は <see cref="RenderConstantValue"/> で展開する。
    /// スロットが埋まらない必須パラメータがある場合は false を返す。
    /// </summary>
    private static bool TryCreateOrdinaryActualArgumentList(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out List<string> actualArguments) {
        actualArguments=[];
        var parameters = method.Parameters;
        var slots = new string?[parameters.Length];
        var paramsIndex = parameters.Length>0 && parameters[^1].IsParams ? parameters.Length-1 : -1;

        // params 展開呼び出し（例: Row(1, a, b, c)）は、固定引数を超える位置引数、または
        // 末尾の単一引数が配列型へ直接変換されない場合に該当する。その場合は末尾をまとめて配列生成式にする。
        if(paramsIndex>=0) {
            var positionalArgs = invocation.ArgumentList.Arguments.Where(static a=>a.NameColon is null).ToList();
            var isExpandedParamsCall = positionalArgs.Count>parameters.Length
                || (positionalArgs.Count==parameters.Length
                    && !SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetTypeInfo(positionalArgs[^1].Expression).ConvertedType,
                        parameters[paramsIndex].Type));
            if(isExpandedParamsCall) {
                if(positionalArgs.Count<paramsIndex || invocation.ArgumentList.Arguments.Any(a=>a.NameColon is not null)) {
                    return false;
                }
                var elementTypeName = ((IArrayTypeSymbol)parameters[paramsIndex].Type).ElementType.ToDisplayString(完全修飾形式);
                var elements = positionalArgs.Skip(paramsIndex)
                    .Select(a=>RenderArgumentForGeneratedMethod(a,semanticModel,containingType,unsafeAccessorCollector,replacementMap));
                for(var i=0;i<paramsIndex;i++) {
                    slots[i]=RenderArgumentForGeneratedMethod(positionalArgs[i],semanticModel,containingType,unsafeAccessorCollector,replacementMap);
                }
                slots[paramsIndex]=$"new {elementTypeName}[] {{ {string.Join(", ",elements)} }}";
                actualArguments=slots.Select(static slot=>slot!).ToList();
                return true;
            }
        }

        var nextParameterIndex = 0;
        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i=0;i<method.Parameters.Length;i++) {
                    if(method.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                while(nextParameterIndex<slots.Length && slots[nextParameterIndex] is not null) {
                    nextParameterIndex++;
                }
                targetParameterIndex=nextParameterIndex++;
            }
            if(targetParameterIndex<0
                || targetParameterIndex>=slots.Length
                || slots[targetParameterIndex] is not null) {
                return false;
            }
            slots[targetParameterIndex]=RenderArgumentForGeneratedMethod(argument,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        }
        for(var i=0;i<slots.Length;i++) {
            if(slots[i] is not null) {
                continue;
            }
            var parameter = method.Parameters[i];
            if(parameter.IsParams && parameter.Type is IArrayTypeSymbol paramsArray) {
                slots[i]=$"global::System.Array.Empty<{paramsArray.ElementType.ToDisplayString(完全修飾形式)}>()";
            } else if(parameter.HasExplicitDefaultValue) {
                slots[i]=RenderConstantValue(parameter.ExplicitDefaultValue);
            } else {
                return false;
            }
        }
        actualArguments=slots.Select(static slot=>slot!).ToList();
        return true;
    }
    /// <summary>
    /// 通常メソッド呼び出しのレシーバー式テキストを解決する。
    /// <c>obj.Method(…)</c> → <c>obj</c> のレンダリング結果、
    /// <c>Method(…)</c>（暗黙の this）→ <c>@this</c>。
    /// </summary>
    private static bool TryRenderOrdinaryReceiverExpression(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out string receiverText) {
        receiverText=string.Empty;
        if(invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            receiverText=RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
            return true;
        }
        if(invocation.Expression is IdentifierNameSyntax) {
            receiverText="@this";
            return true;
        }
        return false;
    }
    private static string ParameterDeclarationForAccessor(IParameterSymbol parameter) {
        var prefix = parameter.RefKind switch {
            RefKind.Ref=>"ref ",
            RefKind.Out=>"out ",
            RefKind.In=>"in ",
            _=>string.Empty,
        };
        return $"{prefix}{parameter.Type.ToDisplayString(完全修飾形式)} {parameter.Name}";
    }
    /// <summary>
    /// テキスト内の型名を完全修飾名に安全に置換する。
    /// 既に一致しているか <c>global::</c> 修飾済みなら何もしない。
    /// 単語境界を考慮した正規表現で部分一致置換を防ぐ。
    /// </summary>
    private static string ReplaceTypeNameForGeneratedMethod(string text,string originalName,string qualifiedName) {
        if(originalName==qualifiedName || originalName.StartsWith("global::",StringComparison.Ordinal)) {
            return text;
        }
        return System.Text.RegularExpressions.Regex.Replace(
            text,
            $@"(?<![\w\.:]){System.Text.RegularExpressions.Regex.Escape(originalName)}(?![\w])",
            qualifiedName,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
    /// <summary>
    /// 非 System.Linq 拡張メソッド呼び出しを、その本体をインライン展開した式に置き換える。
    /// <br/>成功条件: 拡張メソッドのソースが取得でき、本体が式本体か単一 <c>return</c> 文であること。
    /// <br/>実引数リストを <see cref="TryCreateExtensionActualArgumentList"/> で構築し、
    /// <see cref="RenderInlineMethodBodyExpression"/> でパラメータ参照を実引数に置換してインライン化する。
    /// </summary>
    private static bool TryRenderExtensionMethodInvocationExpression(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax extensionMemberAccess,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out string expressionText) {
        expressionText=string.Empty;
        if(method.ReducedFrom is not { } definition
            || !definition.IsExtensionMethod
            || definition.Parameters.Length==0
            || definition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax
            || invocation.ArgumentList.Arguments.Any(static argument=>!argument.RefKindKeyword.IsKind(SyntaxKind.None))) {
            return false;
        }
        if(!TryCreateExtensionActualArgumentList(
            definition,
            invocation,
            extensionMemberAccess,
            semanticModel,
            containingType,
            unsafeAccessorCollector,
            replacementMap,
            out var actualArguments)) {
            return false;
        }
        var bodySemanticModel = semanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }) {
            expressionText=RenderInlineMethodBodyExpression(expressionBody,definition.Parameters,actualArguments,bodySemanticModel,definition.ContainingType,unsafeAccessorCollector);
            return true;
        }
        if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            expressionText=RenderInlineMethodBodyExpression(returnExpression,definition.Parameters,actualArguments,bodySemanticModel,definition.ContainingType,unsafeAccessorCollector);
            return true;
        }
        return false;
    }
    /// <summary>
    /// 拡張メソッドのインライン展開用実引数リストを構築する。
    /// スロット[0] にレシーバー式（<c>source</c>）をセットし、
    /// 以降はソース呼び出しの引数リストを名前付き引数対応でスロットに当てはめる。
    /// <see cref="TryCreateOrdinaryActualArgumentList"/> の拡張メソッド版。
    /// </summary>
    private static bool TryCreateExtensionActualArgumentList(
        IMethodSymbol definition,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax extensionMemberAccess,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out List<string> actualArguments) {
        var actualArgumentSlots = new string?[definition.Parameters.Length];
        actualArgumentSlots[0]=RenderExpressionForGeneratedMethod(extensionMemberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        var nextParameterIndex = 1;
        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i=1;i<definition.Parameters.Length;i++) {
                    if(definition.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                while(nextParameterIndex<actualArgumentSlots.Length && actualArgumentSlots[nextParameterIndex] is not null) {
                    nextParameterIndex++;
                }
                targetParameterIndex=nextParameterIndex++;
            }
            if(targetParameterIndex<=0
                || targetParameterIndex>=actualArgumentSlots.Length
                || actualArgumentSlots[targetParameterIndex] is not null) {
                actualArguments=[];
                return false;
            }
            actualArgumentSlots[targetParameterIndex]=RenderArgumentForGeneratedMethod(argument,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        }
        for(var i=1;i<actualArgumentSlots.Length;i++) {
            if(actualArgumentSlots[i] is not null) {
                continue;
            }
            var parameter = definition.Parameters[i];
            if(parameter.IsParams && parameter.Type is IArrayTypeSymbol paramsArray) {
                actualArgumentSlots[i]=$"global::System.Array.Empty<{paramsArray.ElementType.ToDisplayString(完全修飾形式)}>()";
            } else if(parameter.HasExplicitDefaultValue) {
                actualArgumentSlots[i]=RenderConstantValue(parameter.ExplicitDefaultValue);
            } else {
                actualArguments=[];
                return false;
            }
        }
        actualArguments=actualArgumentSlots.Select(static argument=>argument!).ToList();
        return true;
    }
    /// <summary>
    /// メソッド本体式をインライン化する。
    /// <see cref="RenderExpressionForGeneratedMethod"/> で本体テキストを生成した後、
    /// <see cref="ReplaceInlineParameterReferences"/> でパラメータ参照を実引数テキストに置換する。
    /// コレクション式や暗黙の配列生成は変換先の型にキャストする（型推論が使えないため）。
    /// </summary>
    private static string RenderInlineMethodBodyExpression(
        ExpressionSyntax bodyExpression,
        ImmutableArray<IParameterSymbol> parameters,
        IReadOnlyList<string> actualArguments,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector) {
        var expressionText = RenderExpressionForGeneratedMethod(bodyExpression,semanticModel,containingType,unsafeAccessorCollector);
        expressionText=ReplaceInlineParameterReferences(expressionText,parameters,actualArguments);
        if(bodyExpression is CollectionExpressionSyntax or ImplicitArrayCreationExpressionSyntax) {
            var returnType = semanticModel.GetTypeInfo(bodyExpression).ConvertedType?.ToDisplayString(完全修飾形式);
            if(!string.IsNullOrEmpty(returnType)) {
                expressionText=$"(({returnType}){expressionText})";
            }
        }
        return expressionText;
    }
    /// <summary>
    /// 式テキスト内のパラメータ名を実引数テキストに置換する。
    /// <see cref="InlineParameterReferenceRewriter"/> で構文木を書き換えることで、
    /// 単純な文字列置換では不可能な「メンバー名・ラベル等としてのパラメータ名を置換しない」制約を実現する。
    /// </summary>
    private static string ReplaceInlineParameterReferences(string expressionText,ImmutableArray<IParameterSymbol> parameters,IReadOnlyList<string> actualArguments) {
        var replacements = new Dictionary<string,string>(StringComparer.Ordinal);
        for(var i=0;i<parameters.Length;i++) {
            replacements[parameters[i].Name]=actualArguments[i];
        }
        var expression = SyntaxFactory.ParseExpression(expressionText);
        return NormalizeExpressionText(new InlineParameterReferenceRewriter(replacements).Visit(expression)!.ToString());
    }
    private sealed class InlineParameterReferenceRewriter(IReadOnlyDictionary<string,string> replacements): CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,string> replacements = replacements;

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(!this.replacements.TryGetValue(node.Identifier.ValueText,out var replacement)
                || node.Parent is MemberAccessExpressionSyntax { Name: var memberName } && ReferenceEquals(memberName,node)
                || node.Parent is MemberBindingExpressionSyntax { Name: var bindingName } && ReferenceEquals(bindingName,node)
                || node.Parent is NameColonSyntax
                || node.Parent is NameEqualsSyntax) {
                return node;
            }
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.ParseExpression(replacement)).WithTriviaFrom(node);
        }
    }
    /// <summary>
    /// ローカルヘルパーメソッドをインライン化できなかった場合の代替式を生成する。
    /// 静かなフォールバックを作らず、実行時に即例外を投げるラムダ式を返す。
    /// <c>void</c> メソッドは <c>Action</c>、戻り値あり は <c>Func&lt;TResult&gt;</c> のキャストで即呼び出しする。
    /// </summary>
    private static string RenderInlineFailureInvocationExpression(IMethodSymbol method) {
        var message = EscapeStringLiteral($"Local helper method '{method.Name}' could not be inlined. Forwarding is disabled.");
        return method.ReturnsVoid
            ? $"((global::System.Action)(()=>throw global::Tsavorite.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\")))()"
            : $"((global::System.Func<{method.ReturnType.ToDisplayString(完全修飾形式)}>)(()=>throw global::Tsavorite.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\")))()";
    }

    /// <summary>文字列リテラルに埋め込むために <c>\</c> と <c>"</c> をエスケープする。</summary>
    private static string EscapeStringLiteral(string text) =>
        text.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);
    /// <summary>
    /// 非 public フィールドアクセスを <c>UnsafeAccessor</c> 経由の呼び出し式に変換する。
    /// static フィールドは <c>StaticField</c> アクセサ、インスタンスフィールドは <c>Field</c> アクセサを生成し、
    /// アクセサ宣言を <paramref name="unsafeAccessorCollector"/> に追加する。
    /// アクセサは <c>ref {FieldType}</c> を返すため読み書きの両方に使える。
    /// </summary>
    private static string RenderFieldForwardExpression(IFieldSymbol field,MemberAccessExpressionSyntax memberAccess,SemanticModel semanticModel,List<string>? unsafeAccessorCollector = null) {
        var typeName = SanitizeIdentifier(field.ContainingType.Name);
        var fieldName = SanitizeIdentifier(field.Name);
        var accessorName = $"__Field_{typeName}_{fieldName}";
        var targetTypeName = field.ContainingType.ToDisplayString(完全修飾形式);
        var fieldTypeName = field.Type.ToDisplayString(完全修飾形式);
        string accessorCode;
        string callExpr;
        if(field.IsStatic) {
            accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticField, Name = \"{field.Name}\")]\nprivate static extern ref {fieldTypeName} {accessorName}({targetTypeName}? _);";
            callExpr = $"{accessorName}(null)";
        } else {
            var receiverExpr = memberAccess.Expression is ThisExpressionSyntax
                ? "@this"
                : RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,field.ContainingType,unsafeAccessorCollector);
            accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = \"{field.Name}\")]\nprivate static extern ref {fieldTypeName} {accessorName}({targetTypeName} @this);";
            callExpr = $"{accessorName}({receiverExpr})";
        }
        unsafeAccessorCollector?.Add(accessorCode);
        return callExpr;
    }
    // メソッドグループ用の UnsafeAccessor 静的 extern メソッド名を返す。
    private static string GetDelegateAccessorMethodName(IMethodSymbol method) {
        var typeName = SanitizeIdentifier(method.ContainingType.Name);
        var methodName = SanitizeIdentifier(method.Name);
        return $"__DelegateAccess_{typeName}_{methodName}";
    }
    /// <summary>
    /// メソッドグループ参照をデリゲート生成式に変換する。アクセシビリティに応じて 4 通りに分岐する:
    /// <list type="bullet">
    ///   <item><description>public インスタンス: <c>((Delegate)@this.Method)</c>（直接バインド）。</description></item>
    ///   <item><description>非 public インスタンス: <c>UnsafeAccessor</c> ラムダ経由でデリゲート生成。</description></item>
    ///   <item><description>public static: 完全修飾名で直接参照。</description></item>
    ///   <item><description>非 public static: <c>UnsafeAccessor(StaticMethod)</c> ラムダ経由。</description></item>
    /// </list>
    /// いずれもリフレクションを使わない。
    /// </summary>
    private static string RenderDelegateCreateExpression(IMethodSymbol method,INamedTypeSymbol delegateType,string? receiver,List<string>? unsafeAccessorCollector = null) {
        var delegateTypeName = delegateType.ToDisplayString(完全修飾形式);
        var declaringType = method.ContainingType.ToDisplayString(完全修飾形式);
        // public インスタンスメソッド: @this.Method として直接バインド（リフレクション不要）
        if(!method.IsStatic && method.DeclaredAccessibility==Accessibility.Public) {
            return $"(({delegateTypeName}){receiver}.{method.Name})";
        }
        // 非 public インスタンスメソッド: UnsafeAccessor 経由のラムダでデリゲート生成（リフレクション不要）
        if(!method.IsStatic) {
            var accessorName = GetDelegateAccessorMethodName(method);
            var paramNames = method.Parameters.Select(static (_, i)=>$"__a{i}").ToArray();
            var paramList = string.Join(", ",paramNames);
            var argList = string.Join(", ",new[] { receiver }.Concat(paramNames));
            return $"(({delegateTypeName})(({paramList})=>{accessorName}({argList})))";
        }
        // public static メソッド: 完全修飾名で直接参照（リフレクション不要）
        if(method.DeclaredAccessibility==Accessibility.Public) {
            return $"(({delegateTypeName}){declaringType}.{method.Name})";
        }
        // 非 public static メソッド: UnsafeAccessor + lambda（リフレクション不要）
        {
            var typeName = SanitizeIdentifier(method.ContainingType.Name);
            var methodName = SanitizeIdentifier(method.Name);
            var accessorName = $"__StaticMethod_{typeName}_{methodName}";
            var returnTypeName = method.ReturnType.ToDisplayString(完全修飾形式);
            var paramParts = new List<string> { $"{declaringType}? _" };
            foreach(var param in method.Parameters) {
                paramParts.Add($"{param.Type.ToDisplayString(完全修飾形式)} {param.Name}");
            }
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({string.Join(", ",paramParts)});";
            unsafeAccessorCollector?.Add(accessorCode);
            var paramNames = method.Parameters.Select(static (_, i)=>$"__a{i}").ToArray();
            var lambdaParams = string.Join(", ",paramNames);
            var lambdaArgs = "null" + (paramNames.Length > 0 ? ", " + string.Join(", ",paramNames) : string.Empty);
            return $"(({delegateTypeName})(({lambdaParams})=>{accessorName}({lambdaArgs})))";
        }
    }
    private static string RenderUnsupportedLocalMethodBody(IReadOnlyList<string> unsupportedMethods) {
        // Fragment クラスが存在しない Enumerable メソッドは、元実装へ黙って戻さず例外にする。
        // これにより「intercept 対象だったが未対応だった」ことを生成コードと実行時の両方で確認できる。
        var methodList = string.Join(", ",unsupportedMethods);
        return $"throw global::Tsavorite.GeneratedCodeHelpers.LinqInterceptorFailure(\"Local method interception does not support System.Linq.Enumerable methods without Fragment implementations: {methodList}.\");";
    }
    private static string RenderUninlineableLocalMethodBody(Linq傍受対象型 target) {
        // Fragment クラス名は存在しても、source 形状や delegate 形状の都合で最終的に再構成できないことがある。
        // その場合も静かに元実装へ戻さず失敗を見える化する。
        var methodName = target.Method.ToDisplayString(完全修飾形式)
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal);
        return $"throw global::Tsavorite.GeneratedCodeHelpers.LinqInterceptorFailure(\"Local method interception could not inline '{methodName}'. Forwarding is disabled for methods that contain System.Linq.Enumerable calls.\");";
    }
    private static string RenderUnsupportedDirectMethodBody(Linq傍受対象型 target,string? detail = null) {
        var methodName = target.Method.ReducedFrom?.ToDisplayString(完全修飾形式) ?? target.Method.ToDisplayString(完全修飾形式);
        var message = $"Direct interception could not inline '{methodName}' inside '{target.ContainingMethodName}'.";
        if(!string.IsNullOrWhiteSpace(detail)) {
            message=$"{message} {detail}";
        }
        message=message
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal);
        return $"throw global::Tsavorite.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\");";
    }
    private sealed class ローカルメソッドLinq解析結果(bool hasEnumerableCall,ImmutableArray<string> unsupportedMethods,bool hasAnonymousTypeLinq) {
        public static readonly ローカルメソッドLinq解析結果 Empty = new(false,ImmutableArray<string>.Empty,false);
        public bool HasEnumerableCall { get; } = hasEnumerableCall;
        public ImmutableArray<string> UnsupportedMethods { get; } = unsupportedMethods;
        /// <summary>
        /// メソッド本体内の LINQ 呼び出しに匿名型が含まれる場合 true。
        /// 匿名型は生成メソッドの署名に出せないため完全なフラグメント最適化ができず、
        /// 該当 LINQ 呼び出しは IEnumerable&lt;T&gt; 汎用ヘルパーへ委譲する部分最適化になる。
        /// ValueTuple や struct record を使えば完全最適化が可能になるため、
        /// この値が true のとき Tsavorite001 警告を発行してユーザーに通知する。
        /// </summary>
        public bool HasAnonymousTypeLinq { get; } = hasAnonymousTypeLinq;
    }
    private sealed class Linq傍受対象型(string identifier,string containingMethodName,string interceptsLocationAttributeText,IMethodSymbol method,IMethodSymbol templateMethod,InvocationExpressionSyntax invocation,INamedTypeSymbol enclosingType,SemanticModel semanticModel,TsavoriteIncrementalGenerator.InterceptParameterSpec[] parameters,TsavoriteIncrementalGenerator.ローカルメソッドLinq解析結果? linqAnalysis = null,string? sourceElementTypeName = null,string[]? methodTypeParameterNames = null,bool isLocalMethodInterception = false):IEquatable<Linq傍受対象型> {
        public bool Equals(Linq傍受対象型? other) {
            if(other is null) return false;
            if(ReferenceEquals(this,other)) return true;
            return string.Equals(this.InterceptsLocationAttributeText,other.InterceptsLocationAttributeText,StringComparison.Ordinal);
        }
        public override bool Equals(object? obj)=>obj is Linq傍受対象型 other &&this.Equals(other);
        public override int GetHashCode()=>this.InterceptsLocationAttributeText.GetHashCode(StringComparison.Ordinal);
        private string Identifier { get; } = identifier;
        public string ContainingMethodName { get; } = containingMethodName;
        public string InterceptsLocationAttributeText { get; } = interceptsLocationAttributeText;
        public IMethodSymbol Method { get; } = method;
        private IMethodSymbol TemplateMethod { get; } = templateMethod;
        public InvocationExpressionSyntax Invocation { get; } = invocation;
        public string SourcePath=>this.Invocation.SyntaxTree.FilePath;
        public INamedTypeSymbol EnclosingType { get; } = enclosingType;
        public SemanticModel SemanticModel { get; } = semanticModel;
        private InterceptParameterSpec[] Parameters { get; } = parameters;
        public ローカルメソッドLinq解析結果 LinqAnalysis { get; } = linqAnalysis??ローカルメソッドLinq解析結果.Empty;
        private string? ExplicitSourceElementTypeName { get; } = sourceElementTypeName;
        private string[] MethodTypeParameters { get; } = methodTypeParameterNames?? [];
        public bool IsLocalMethodInterception { get; } = isLocalMethodInterception;
        private string SourceElementTypeName {
            get {
                if(!string.IsNullOrWhiteSpace(this.ExplicitSourceElementTypeName)) {
                    return this.ExplicitSourceElementTypeName;
                }
                if(this.Method.TypeArguments.Length>0) {
                    return this.Method.TypeArguments[0].ToDisplayString(完全修飾形式);
                }
                var parameters = this.TemplateMethod.Parameters;
                if(parameters.Length==0
                    ||parameters[0].Type is not INamedTypeSymbol sourceType
                    ||sourceType.TypeArguments.Length!=1) {
                    throw new InvalidOperationException($"Could not determine source element type for '{this.TemplateMethod.ToDisplayString(完全修飾形式)}'.");
                }
                return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
            }
        }
        public string ReturnTypeName=>this.Method.ReturnsVoid ? "void" : this.Method.ReturnType.ToDisplayString(完全修飾形式);
        public string InterceptorParameterList {
            get {
                if(this.IsLocalMethodInterception) {
                    var parameters = new List<string>();
                    if(!this.Method.IsStatic) {
                        parameters.Add($"this {this.Method.ContainingType.ToDisplayString(完全修飾形式)} @this");
                    }
                    parameters.AddRange(this.Parameters.Select(static parameter=>parameter.Declaration));
                    return string.Join(", ",parameters);
                }
                return $"this global::System.Collections.Generic.IEnumerable<{this.SourceElementTypeName}> source{this.MethodParameterList}";
            }
        }
        private string MethodParameterList=>this.Parameters.Length==0
            ? string.Empty
            : ", "+string.Join(", ",this.Parameters.Select(static parameter=>parameter.Declaration));
        public string MethodTypeParameterList=>this.MethodTypeParameters.Length==0
            ? string.Empty
            : "<"+string.Join(", ",this.MethodTypeParameters)+">";
        public string MethodName=>$"OptimizedLinq_{this.Identifier}";
    }
    private sealed class InterceptParameterSpec(string name,string typeName,RefKind refKind = RefKind.None) {
        private string Name { get; } = name;
        private string TypeName { get; } = typeName;
        private RefKind RefKind { get; } = refKind;
        public string Declaration=>$"{this.RefKindModifier}{this.TypeName} {this.Name}";
        private string RefKindModifier=>this.RefKind switch {
            RefKind.Ref=>"ref ",
            RefKind.Out=>"out ",
            RefKind.In=>"in ",
            _=>string.Empty,
        };
    }
    private sealed class LambdaSpec(string returnTypeName,string[] parameterTypeNames,string[] parameterNames,string body) {
        public string ReturnTypeName { get; } = returnTypeName;
        public string[] ParameterTypeNames { get; } = parameterTypeNames;
        public string[] ParameterNames { get; } = parameterNames;
        public string Body { get; } = body;
    }
    private sealed class LinqInterceptTargetComparer : IEqualityComparer<Linq傍受対象型> {
        public static readonly LinqInterceptTargetComparer Instance = new();
        public bool Equals(Linq傍受対象型? x,Linq傍受対象型? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null || y is null) {
                return false;
            }
            return x.InterceptsLocationAttributeText==y.InterceptsLocationAttributeText;
        }
        public int GetHashCode(Linq傍受対象型 obj) {
            return obj.InterceptsLocationAttributeText.GetHashCode(StringComparison.Ordinal);
        }
    }
    private sealed class PerFileInterceptGroup(string sourcePath,ImmutableArray<TsavoriteIncrementalGenerator.Linq傍受対象型> targets):IEquatable<PerFileInterceptGroup> {
        public string SourcePath { get; } = sourcePath;
        public ImmutableArray<Linq傍受対象型> Targets { get; } = targets;
        public bool Equals(PerFileInterceptGroup? other) {
            if(other is null) return false;
            if(ReferenceEquals(this,other)) return true;
            if(!string.Equals(this.SourcePath,other.SourcePath,StringComparison.Ordinal)) return false;
            if(this.Targets.Length!=other.Targets.Length) return false;
            for(var i = 0;i<this.Targets.Length;i++) {
                if(!this.Targets[i].Equals(other.Targets[i])) return false;
            }
            return true;
        }
        public override bool Equals(object? obj)=>obj is PerFileInterceptGroup other &&this.Equals(other);
        public override int GetHashCode() {
            var hc = StringComparer.Ordinal.GetHashCode(this.SourcePath);
            foreach(var t in this.Targets) {
                hc=HashCode.Combine(hc,t.GetHashCode());
            }
            return hc;
        }
    }
    private sealed class 型対象型(INamedTypeSymbol symbol,bool isPartial) {
        public INamedTypeSymbol Symbol { get; } = symbol;
        public bool IsPartial { get; } = isPartial;
    }
}
//20260531 3625
