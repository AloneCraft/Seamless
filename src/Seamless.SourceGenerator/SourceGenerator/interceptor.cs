/*
2026/05/23
# interceptor.cs の仕様まとめ

`SeamlessIncrementalGenerator`（`IIncrementalGenerator` 実装）の主要な仕様を以下にまとめます。

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
  - 例: `Aggregate_func`, `Where_index`, `Join`
- コンストラクタの第1引数は必ず `int`（一意な番号）
- パラメータ数の多い（より特化した）オーバーロードを優先して試行
- 新しい Fragment クラスを追加するだけで辞書が自動更新される

---

## 5. Fragment の選択・生成フロー

```
呼び出し式フラグメント生成1()
  ├─ Any → Anyフラグメント生成3()（Where + Any に変換）
  ├─ SelectMany のインライン化可能ならSelectManyインライン化試行50()
  └─ その他 → 登録済みフラグメント生成24()
                 └─ フラグメント候補一覧から MethodName で候補を検索
                    └─ 各候補を試行（登録済みフラグメント生成試行26）
```

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
1. 文の中の LINQ 呼び出し式を抽出（ラムダ内部やネストした LINQ は除外）
2. 各呼び出しを Fragment で展開し、`__linq0`, `__linq1`... などのローカル変数に一時保存
3. 元の文のテキストを一時変数名で置換して出力

---

## 8. Enumerator の実行方式（`フラグメントEnumerator実行文追加37`）

| 条件 | 出力コード |
|---|---|
| `SupportsFlatLoop == true`（かつ `SEAMLESS_ENUMERATOR_MODE` 未定義） | フラットループ（`GetFlatLoopCode`）展開 |
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

## 10. 匿名型の取り扱い（SEAMLESS001 警告）

- LINQ 呼び出しに匿名型が含まれる場合、完全なフラグメント最適化は不可。
- 代わりに型パラメータ化した汎用ヘルパーメソッドを生成し `IEnumerable<T>` 経由で System.Linq.Enumerable を再呼び出しする部分最適化に切り替える。
- 同時に `SEAMLESS001` 診断警告を発行し、ValueTuple または struct record 使用を促す。

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
*/
// #define SEAMLESS_ENUMERATOR_MODE  // コメントを外すと常に Enumerator struct 方式になる（フラットループ無効化）

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Seamless.SourceGenerator.Linq.Fragments;
using Enumerable = Seamless.SourceGenerator.Linq.Fragments.Enumerable;

namespace Seamless.SourceGenerator;

[Generator]
public sealed partial class SeamlessIncrementalGenerator : IIncrementalGenerator {
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
        .Select(static method => method.Name)
        .ToHashSet(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<Compilation,System.Collections.Concurrent.ConcurrentDictionary<IMethodSymbol,ローカルメソッドLinq解析結果>> ローカルメソッドLinq解析キャッシュ = new();

    // 匿名型を含む LINQ が検出されたとき、インターセプト元のメソッドに対して発行する警告。
    // 匿名型は生成メソッドの署名に出せないため完全なフラグメント最適化ができず、
    // 匿名型を含む LINQ 呼び出しのみ IEnumerable<T> 汎用ヘルパーへ委譲する部分最適化になる。
    // ValueTuple や struct record を使えば完全最適化が可能になることをユーザーに伝える。
    private static readonly DiagnosticDescriptor 匿名型警告Descriptor = new(
        id: "SEAMLESS001",
        title: "匿名型が検出されました",
        messageFormat: "'{0}' に匿名型を含む LINQ 式が検出されました。最適化可能な部分だけインターセプトします。ValueTuple または struct record を使用することを検討してください。",
        category: "Seamless.Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "匿名型を含む LINQ 式は、生成メソッドの署名に匿名型を記述できないため完全なフラグメント最適化が適用できません。ValueTuple (例: (int x, int y)) または struct record を使うことで最適化が有効になります.");

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var interceptTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is InvocationExpressionSyntax,
                static (syntaxContext,cancellationToken) => Linq傍受対象生成(syntaxContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var typeTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is TypeDeclarationSyntax,
                static (syntaxContext,_) => 型対象生成(syntaxContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        context.RegisterPostInitializationOutput(static productionContext => {
            productionContext.AddSource("InterceptsLocationAttribute.g.cs",SourceText.From(InterceptsLocationAttributeSource,Encoding.UTF8));
        });

        context.RegisterSourceOutput(typeTargets.Collect(),static (productionContext,typeTargets) => {

            if(!typeTargets.IsDefaultOrEmpty) {
                var databaseSource = データベースソース構築(typeTargets);
                if(databaseSource.Length>0) {
                    productionContext.AddSource("Seamless.SourceGenerator.Schema.g.cs",SourceText.From(空行除去(databaseSource),Encoding.UTF8));
                }
            }

        });

        context.RegisterSourceOutput(interceptTargets.Collect(),static (productionContext,interceptTargets) => {
            if(interceptTargets.IsDefaultOrEmpty) {
                return;
            }

            var distinctTargets = interceptTargets.Distinct(LinqInterceptTargetComparer.Instance).ToArray();

            // 匿名型を含む LINQ を持つメソッドに対して SEAMLESS001 警告を発行する。
            // 同一メソッドに複数の呼び出しサイトがある場合でも警告は 1 回のみにする。
            var reportedMethods = new HashSet<string>(StringComparer.Ordinal);
            foreach(var target in distinctTargets) {
                if(!target.LinqAnalysis.HasAnonymousTypeLinq) {
                    continue;
                }
                // メソッドの完全修飾名で重複を除去する。
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

            var sourceGroups = distinctTargets
                .GroupBy(static target => target.SourcePath,StringComparer.Ordinal)
                .ToArray();
            foreach(var sourceGroup in sourceGroups) {
                var source = インターセプターソース構築([..sourceGroup]);
                if(source.Length==0) {
                    continue;
                }

                productionContext.AddSource(インターセプターHintName取得(sourceGroup.Key),SourceText.From(source,Encoding.UTF8));
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

        // インターセプト対象にする主条件は 2 つ:
        // 1. その場所が通常の関数呼び出しであること
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
            || method.IsGenericMethod
            // ジェネリックメソッドは型引数再構成と private アクセス経路の組み合わせが未整備。
            || method.ContainingType.IsGenericType
            // ジェネリック型メンバーは生成メソッド側へ型パラメータを漏らしやすいので除外。
            || (!method.IsStatic && (method.IsAbstract || method.IsVirtual || method.IsOverride))
            // 仮想呼び出しを intercept すると動的ディスパッチを壊すのでそのまま残す。
            || ContainsGeneratedTypeAccessibilityRisk(method.ContainingType)
            // private/protected な入れ子型は生成コードから参照できずコンパイル不能になる。
            || method.ContainingType.IsRefLikeType
            // ref struct を interceptor 引数/receiver として扱う経路は未対応。
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
            || ContainsTypeParameter(method.ReturnType)
            // 戻り値に型パラメータが残ると、生成側の静的メソッドで閉じられない。
            || method.Parameters.Any(static parameter => parameter.RefKind!=RefKind.None)
            // ref/out/in は aliasing を壊しやすいのでいったん除外。
            || method.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))
            // 匿名型引数も署名へ出せない。
            || method.Parameters.Any(static parameter => ContainsGeneratedTypeAccessibilityRisk(parameter.Type))
            // 非公開型引数は生成コードから参照できない。
            || method.Parameters.Any(static parameter => parameter.Type.IsRefLikeType)
            // ref struct 引数は寿命制約を再現しにくい。
            || method.Parameters.Any(static parameter => ContainsTypeParameter(parameter.Type))) {
            // 開いた型パラメータを含む引数も現在の再生成対象外。
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
            .Select(static parameter => new InterceptParameterSpec(parameter.Name,parameter.Type.ToDisplayString(完全修飾形式),parameter.RefKind))
            .ToArray();

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
            isLocalMethodInterception: true);
        return true;
    }

    private static ローカルメソッドLinq解析結果 ローカルメソッドLinq解析取得36(IMethodSymbol method,Compilation compilation) {
        var cache = ローカルメソッドLinq解析キャッシュ.GetValue(
            compilation,
            static _ => new System.Collections.Concurrent.ConcurrentDictionary<IMethodSymbol,ローカルメソッドLinq解析結果>(SymbolEqualityComparer.Default));
        return cache.GetOrAdd(method,static (method,compilation) => ローカルメソッドLinq解析実行37(method,compilation),compilation);
    }

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
            if(definition.Parameters.Any(static p => p.RefKind != RefKind.None)) {
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
                    || invokedMethod.Parameters.Any(static p => ContainsAnonymousType(p.Type))) {
                    hasAnonymousTypeLinq=true;
                }
            }
        }

        return new ローカルメソッドLinq解析結果(
            hasEnumerableCall,
            [..unsupportedMethods.OrderBy(static methodName => methodName,StringComparer.Ordinal)],
            hasAnonymousTypeLinq);
    }

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

        return false;
    }

    // intercept target 判定用の広い条件。
    // ここでは「Fragment に落とせるか」は問わず、System.Linq.Enumerable であることだけを見て
    // まず対象化する。未対応ケースは生成 body 側の例外で表面化させる。
    private static bool SystemLinqEnumerableメソッド判定38(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        return definition.ContainingType.ToDisplayString()=="System.Linq.Enumerable";
    }

    private static string インターセプターソース構築(ImmutableArray<Linq傍受対象型> targets) {
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System.Linq;");

        foreach(var namespaceGroup in targets.GroupBy(static target => インターセプト名前空間取得(target.EnclosingType),StringComparer.Ordinal)) {
            builder.AppendLine();
            builder.AppendLine($"namespace {namespaceGroup.Key} {{");
            var originalNamespace = namespaceGroup.Key.StartsWith("Intercept.",StringComparison.Ordinal)
                ? namespaceGroup.Key["Intercept.".Length..]
                : string.Empty;
            if(!string.IsNullOrWhiteSpace(originalNamespace)) {
                builder.AppendLine($"    using {originalNamespace};");
                builder.AppendLine();
            }
            foreach(var typeGroup in namespaceGroup.GroupBy(static target => target.EnclosingType.ToDisplayString(完全修飾形式),StringComparer.Ordinal)) {
                var typeTargets = typeGroup.ToImmutableArray();
                var typeSource = インターセプター型ソース構築(typeTargets);
                インデント付き追記(builder,typeSource,"    ");
            }
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    private static string インターセプター型ソース構築(ImmutableArray<Linq傍受対象型> targets) {
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
                        インデント付き追記(builder,RenderUnsupportedLocalMethodBody(unsupportedMethods),"        ");
                    } else {
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

    private static List<INamedTypeSymbol> 型スタック取得(INamedTypeSymbol type) {
        var stack = new List<INamedTypeSymbol>();
        for(INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) {
            stack.Add(current);
        }
        stack.Reverse();
        return stack;
    }

    private static string インターセプト名前空間取得(INamedTypeSymbol type) {
        var originalNamespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : string.Empty;
        return string.IsNullOrWhiteSpace(originalNamespace)
            ? "Intercept"
            : $"Intercept.{originalNamespace}";
    }

    private static string インターセプターHintName取得(string sourcePath) {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        return string.IsNullOrWhiteSpace(fileName)
            ? "Unknown.g.cs"
            : $"{SanitizeIdentifier(fileName)}.g.cs";
    }

    private static void インターセプターXmlコメント追加(StringBuilder builder,Linq傍受対象型 target) {
        var cref = Xml属性値エスケープ(メソッドCref構築(target.Method));
        var originalName = Xmlテキストエスケープ(target.Method.Name);
        builder.AppendLine($"    /// <summary>Intercepts <see cref=\"{cref}\" /> ({originalName}).</summary>");
    }

    private static string メソッドCref構築(IMethodSymbol method) {
        var containingType = method.ContainingType.ToDisplayString(cref形式);
        var parameters = string.Join(",",method.Parameters.Select(static parameter => parameter.Type.ToDisplayString(cref形式)));
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

    // ローカルメソッドを丸ごと再構成し、文中の LINQ 呼び出しをフラグメント実行へ差し替える。
    private static bool 生成ローカルメソッド本体構築0(Linq傍受対象型 対象,out string 静的メソッド群,out string 本体,HashSet<string>? emittedUnsafeAccessors = null) {
        静的メソッド群=string.Empty;
        本体=string.Empty;

        // 以前は識別子ハッシュから開始番号を作っていたため Struct1144748 のような読みにくい名前になっていた。
        // いまはメソッドごとに 0 から連番を振り、必要なら外側で補助クラスに包んで衝突を避ける。
        var フラグメント番号 = フラグメント番号初期値取得18();
        if(対象.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax { Body: { } メソッド本体 } メソッド構文) {
            return false;
        }

        var セマンティックモデル = 対象.SemanticModel.Compilation.GetSemanticModel(メソッド構文.SyntaxTree);
        var usedNames = CollectMethodBodyIdentifiers(メソッド構文, 対象.Method);
        var sourceUnsafeAccessorCollector = new List<string>();
        var generatedFragments = new List<Fragment>();
        var 本体ビルダー = new StringBuilder();
        var linq呼び出し数 = 0;
        foreach(var 文 in メソッド本体.Statements) {
            if(!文生成16(文,セマンティックモデル,対象.EnclosingType,ref フラグメント番号,対象.Method.IsStatic,ref 静的メソッド群,sourceUnsafeAccessorCollector,generatedFragments,本体ビルダー,ref linq呼び出し数,usedNames)) {
                return false;
            }
        }

        if(linq呼び出し数==0) {
            return false;
        }

        本体=本体ビルダー
            .ToString()
            .TrimEnd()
            .Replace("throw NoElements();","throw global::Seamless.Linq.Enumerable.NoElements();",StringComparison.Ordinal);

        静的メソッド群と本体を補助クラスへ退避21(対象.MethodName,ref 静的メソッド群,ref 本体);

        // フラグメントツリーおよびソース式から UnsafeAccessor 宣言を収集し、補助クラスの外（クラス直下）に追記する。
        // これにより、インターセプタメソッド本体から短い名前で直接参照できる。
        // emittedUnsafeAccessors が渡された場合はクラスレベルで重複しないようフィルタする。
        var unsafeAccessorCodes = generatedFragments
            .SelectMany(static fragment => fragment.CollectAllUnsafeAccessorCodes())
            .Concat(sourceUnsafeAccessorCollector)
            .Where(code => emittedUnsafeAccessors?.Add(code) ?? true)
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
    /// source.Any(x => x > 0) → Where(x => x > 0) → Any()
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

        if(methodName=="Any") {
            return Anyフラグメント生成3(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
        }

        // SelectMany selector の内部チェーンを struct にインライン化できる場合は専用パスへ。
        if(methodName=="SelectMany"
            && SelectManyインライン化試行50(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector)) {
            return true;
        }

        return 登録済みフラグメント生成24(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
    }
    /// <summary>
    /// source.Any(x => x > 0)
    /// ↓
    /// Where(x => x > 0) → Any()
    /// </summary>
    private static bool Anyフラグメント生成3(SemanticModel semanticModel,InvocationExpressionSyntax invocation,INamedTypeSymbol containingType,ref int uniqueNumber,out Fragment fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null)
        => Any.TryBuildWithPredicate(
            semanticModel,invocation,containingType,ref uniqueNumber,
            out fragment,out staticMethods,unsafeAccessorCollector,
            列挙フラグメント生成9,
            関数フラグメント生成12,
            (name,variant,n,extraArgs) => {
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
    ///     Where_index             → MethodName="Where",     VariantName="index"
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
            .Where(type => !type.IsAbstract                          // 抽象クラスは除外
                && fragmentType.IsAssignableFrom(type)               // Fragment を継承している
                && !helperTypes.Contains(type))                      // 基底型/補助型を除外
            .Select(type => {
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
                    ? method => (bool)isApplicableMethod.Invoke(null,[method])!
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
            .Where(static candidate => candidate is not null)
            .Cast<フラグメント候補>()
            // パラメータ数の降順 → 型名の昇順でソート
            // パラメータ数が多い = より特化した overload を先に試行するため
            .OrderByDescending(static candidate => candidate.Parameters.Length)
            .ThenBy(static candidate => candidate.FragmentType.Name, StringComparer.Ordinal)
            // MethodName でグループ化して辞書化
            .GroupBy(static candidate => candidate.MethodName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToImmutableArray(),
                StringComparer.Ordinal);

        return candidates;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// Fragment 候補一覧から Method 名に対応する型
    /// 例:
    /// Aggregate_func / Aggregate_seed_func / Aggregate_seed_func_resultSelector
    /// Where / Where_index
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

        if(method.ReducedFrom is not null
            && invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            sourceExpression=receiverExpression;
            return true;
        }

        // 仕様: static な System.Linq.Enumerable 呼び出しでは第 1 引数を source として扱う。
        // Cast/OfType のため、source 型は IEnumerable<T> だけでなく非ジェネリック IEnumerable も許可する。
        var definition = method.ReducedFrom ?? method;
        if(definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            || definition.Parameters.Length==0
            || invocation.ArgumentList.Arguments.Count==0
            || !IsEnumerableSourceParameter(definition.Parameters[0].Type)) {
            return false;
        }

        sourceExpression=invocation.ArgumentList.Arguments[0].Expression;
        argumentStartIndex=1;
        var sourceActualType = semanticModel.GetTypeInfo(sourceExpression).Type ?? definition.Parameters[0].Type;
        fragmentMethod = definition.ReduceExtensionMethod(sourceActualType) ?? method;
        return true;
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
        List<string>? unsafeAccessorCollector = null)
        => SelectManyBase.TryBuildInlined(
            semanticModel,invocation,containingType,ref uniqueNumber,
            out fragment,out staticMethods,unsafeAccessorCollector,
            列挙フラグメント生成9,
            TryGetLambdaExpressionBody,
            GetLambdaParameters);

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
                if(!単独列挙フラグメント生成10(semanticModel,argumentExpression,containingType,ref uniqueNumber,out var enumerableFragment,out var enumerableStaticMethods)) {
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
        => candidate.IsApplicableOverload?.Invoke(method) ?? true;

    private static bool 関数フラグメント利用可能28(FuncFragment funcFragment) {
        if(funcFragment.CanGenerateMethod) {
            return true;
        }

        // キャプチャ付きラムダ/delegate 式は struct closure として primary constructor に閉じ込め、
        // ループ内では式へ展開する。delegate 変数・メソッドグループは受け付けない。
        if(funcFragment is LambdaFunc) {
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
        if(expression is InvocationExpressionSyntax invocation
            && 呼び出し式フラグメント生成1(semanticModel,invocation,containingType,ref uniqueNumber,out var nestedFragment,out staticMethods,unsafeAccessorCollector)
            && nestedFragment is Enumerable enumerableFragment) {
            fragment=enumerableFragment;
            return true;
        }

        return 単独列挙フラグメント生成10(semanticModel,expression,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
    }

    /// <summary>
    /// Source() / Second() / rows
    /// ↓
    /// Root(sourceExpression)
    /// として、メソッドチェーンの起点になる列挙ソースへ正規化する。
    /// </summary>
    private static bool 単独列挙フラグメント生成10(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null) {
        staticMethods=string.Empty;
        var expressionType = semanticModel.GetTypeInfo(expression).Type;
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
        var concreteEnumeratorTypeName = 具象エニュメレータ型名取得(expressionType,elementType,useEnumerableInterface);
        var enumeratorTypeName = concreteEnumeratorTypeName
            ?? $"global::System.Collections.Generic.IEnumerator<{elementTypeName}>";
        // 具象型が取れなかった場合はインターフェース IEnumerator<T> になるため struct 制約なし扱いにする。
        var effectiveUseEnumerableInterface = useEnumerableInterface || concreteEnumeratorTypeName is null;

        var _srcExpr=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);
        var _num=uniqueNumber++;
        fragment = expressionType is IArrayTypeSymbol
            ? new ArrayRoot(_num, _srcExpr, elementTypeName)
            : CreateRootEnumerableFragment(_srcExpr, elementTypeName, enumeratorTypeName, effectiveUseEnumerableInterface, _num);
        return true;
    }

    // source 型の public な非明示的実装 GetEnumerator() を探し、値型を返す場合はその型名を返す。
    // インターフェース型・配列・戻り値がインターフェースや抽象型の場合は null を返す。
    private static string? 具象エニュメレータ型名取得(ITypeSymbol? sourceType,ITypeSymbol elementType,bool useEnumerableInterface) {
        if(useEnumerableInterface || sourceType is not INamedTypeSymbol namedSource) {
            return null;
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
            return null;
        }
        // 戻り値が具象値型（インターフェースでも抽象型でもない）であれば使う。
        if(getEnumerator.ReturnType is not INamedTypeSymbol returnType
            || returnType.IsAbstract
            || returnType.TypeKind==TypeKind.Interface) {
            return null;
        }
        return returnType.ToDisplayString(完全修飾形式);
    }

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
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(static candidate => IsIEnumerableType(candidate) && candidate.TypeArguments.Length==1);
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

    /// <summary>
    /// x => x * 2 / (left, right) => left + right
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
        // 仕様: Func<T> f = x => ...; のようなローカル delegate 変数は initializer のラムダへ戻してインライン化する。
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
                    .Select(static t => t.ToDisplayString(完全修飾形式)).ToArray();
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
                            .Where(sym => (sym is ILocalSymbol || sym is IParameterSymbol) && !anonParamSet.Contains(sym.Name) && sym.Name != "this")
                            .Select(sym => (sym.Name, sym is ILocalSymbol loc
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
                    capturesThis = dataFlow.DataFlowsIn.Any(sym => sym.Name == "this");
                    capturedLocals = dataFlow.DataFlowsIn
                        .Where(sym => (sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name != "this")
                        .Select(sym => (sym.Name, sym is ILocalSymbol loc
                            ? loc.Type.ToDisplayString(完全修飾形式)
                            : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                        .ToArray();
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
                    && scalarFragment is Scalar) {
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

                throw LambdaInlineException(lambda,semanticModel,"ラムダ内 LINQ を Fragment として構築できませんでした。",scalarInvocation);
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
                        .Select(static t => t.ToDisplayString(完全修飾形式)).ToArray();
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
                    capturesThis = dataFlow.DataFlowsIn.Any(sym => sym.Name == "this");
                    capturedLocals = dataFlow.DataFlowsIn
                        .Where(sym => (sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name != "this")
                        .Select(sym => (sym.Name, sym is ILocalSymbol loc
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

        return false;
    }

    // 仕様: LINQ に渡された delegate（ローカル変数 / instance・static フィールド）は、
    // 宣言時 initializer がラムダ / anonymous delegate の場合だけその式を本体として扱う。
    // `field` / `this.field` / `Type.field` のいずれの参照形でも対象にする。
    // 変数・フィールド呼び出しを delegate Invoke として残す経路は作らない。
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

    private static string 静的メソッド連結14(string left,string right) {
        return string.Join("\n",new[] { left,right }.Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    // ローカルメソッド再構成の主経路。
    // Enumerable フラグメントがフラットループをサポートする場合は末尾メソッド内にループを展開し、
    // @this スコープで全ラムダをインライン化する。それ以外は enumerator struct + while で消費する。
    private static bool フラグメントEnumerator実行文追加37(StringBuilder builder,Fragment fragment,string actualVariableName,string actualTypeName,HashSet<string> usedNames) {
        var suffix = SanitizeIdentifier(actualVariableName);
        var enumeratorName = GetUniqueIdentifier($"e_{suffix}", usedNames);
        var currentName = GetUniqueIdentifier($"c_{suffix}", usedNames);

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
            : string.Join(", ",statement.Initializers.Select(e => RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        var cond = statement.Condition is not null
            ? RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector)
            : string.Empty;
        var incr = string.Join(", ",statement.Incrementors.Select(e => RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        builder.AppendLine($"for({init}; {cond}; {incr})");
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount,usedNames);
    }

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
        // 式文の全体が単一 LINQ 呼び出しで結果が破棄される場合（e.g. Take(0).Select(...)）、
        // GetEnumerator() を呼ばないよう文ごとスキップする（元 LINQ の遅延評価と整合させる）。
        var outerLinqInvocations = 外側LINQ呼び出し取得41(statement,semanticModel);
        if(statement is ExpressionStatementSyntax discardedExprStatement
            && outerLinqInvocations.Count==1
            && NormalizeExpressionText(discardedExprStatement.Expression.ToString())==NormalizeExpressionText(outerLinqInvocations[0].ToString())) {
            linqInvocationCount++;
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
            var resultTypeName = semanticModel.GetTypeInfo(invocation).ConvertedType?.ToDisplayString(完全修飾形式)
                ?? semanticModel.GetTypeInfo(invocation).Type?.ToDisplayString(完全修飾形式)
                ?? "object";
            if(!フラグメントEnumerator実行文追加37(builder,fragment,tempName,resultTypeName,usedNames)) {
                return false;
            }
            replacementMap[NormalizeExpressionText(invocation.ToString())]=tempName;
        }

        var generatedStatement = statement switch {
            LocalDeclarationStatementSyntax localDeclaration => ローカル宣言生成17(localDeclaration,semanticModel,containingType,replacementMap,unsafeAccessorCollector),
            ExpressionStatementSyntax expressionStatement2 => $"{RenderExpressionForGeneratedMethod(expressionStatement2.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap)};",
            _ => RenderStatementForGeneratedMethod(statement,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
        };
        builder.AppendLine(generatedStatement);
        return true;
    }

    private static bool 匿名型LINQ実行文追加52(
        StringBuilder builder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        string actualVariableName,
        List<string>? unsafeAccessorCollector,
        HashSet<string> usedNames,
        out string staticMethods) {
        staticMethods=string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        if(definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            || !呼び出しソース式取得51(semanticModel,invocation,method,out var sourceExpression,out var argumentStartIndex,out var fragmentMethod)) {
            return false;
        }

        if(!ContainsAnonymousType(method.ReturnType)
            && !fragmentMethod.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))) {
            return false;
        }

        var anonymousTypeParameters = new Dictionary<ITypeSymbol,string>(SymbolEqualityComparer.Default);
        var helperTypeParameters = new List<string>();
        var sourceElementType = semanticModel.GetTypeInfo(sourceExpression).Type is { } sourceType
            && 列挙要素型取得11(sourceType,out var resolvedSourceElementType,out _)
            ? resolvedSourceElementType
            : definition.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 1 } sourceParameterType
                ? sourceParameterType.TypeArguments[0]
                : null;
        if(sourceElementType is null) {
            return false;
        }

        var helperName = $"Struct{uniqueNumber++}_AnonymousLinq";
        var invocationArguments = invocation.ArgumentList.Arguments;

        // 「型推論のみ」パターンを使用してループ展開による完全インライン化を実現
        // Select、GroupByなどに対応
        if(definition.Name == "Select" && invocationArguments.Count == argumentStartIndex + 1) {
            return 匿名型Select生成53(builder, semanticModel, invocation, containingType, sourceExpression,
                invocationArguments[argumentStartIndex].Expression, sourceElementType,
                anonymousTypeParameters, helperTypeParameters, helperName, actualVariableName,
                unsafeAccessorCollector, usedNames, out staticMethods);
        }

        if(definition.Name == "GroupBy") {
            return 匿名型GroupBy生成54(builder, semanticModel, invocation, containingType, sourceExpression,
                argumentStartIndex, invocationArguments, fragmentMethod, sourceElementType,
                anonymousTypeParameters, helperTypeParameters, helperName, actualVariableName,
                unsafeAccessorCollector, usedNames, out staticMethods);
        }

        // その他のメソッドは従来通りSystem.Linq.Enumerableに委譲
        var parameterDeclarations = new List<string> {
            $"global::System.Collections.Generic.IEnumerable<{RenderGeneratedTypeName(sourceElementType,anonymousTypeParameters,helperTypeParameters)}> source"
        };
        var callArguments = new List<string> { "source" };
        for(var argumentIndex = argumentStartIndex; argumentIndex<invocationArguments.Count; argumentIndex++) {
            var methodParameterIndex = argumentIndex-argumentStartIndex;
            if(methodParameterIndex>=fragmentMethod.Parameters.Length) {
                return false;
            }

            var parameterName = $"arg{methodParameterIndex}";
            var parameterType = RenderGeneratedTypeName(fragmentMethod.Parameters[methodParameterIndex].Type,anonymousTypeParameters,helperTypeParameters);
            parameterDeclarations.Add($"{parameterType} {parameterName}");
            callArguments.Add(parameterName);
        }

        if(helperTypeParameters.Count==0) {
            return false;
        }

        var sourceText = RenderExpressionForGeneratedMethod(sourceExpression,semanticModel,containingType,unsafeAccessorCollector);
        var renderedArguments = invocationArguments
            .Skip(argumentStartIndex)
            .Select(argument => RenderArgumentForGeneratedMethod(argument,semanticModel,containingType,unsafeAccessorCollector))
            .ToArray();
        builder.AppendLine($"var {actualVariableName}={helperName}({string.Join(", ",new[] { sourceText }.Concat(renderedArguments))});");

        var returnType = RenderGeneratedTypeName(method.ReturnType,anonymousTypeParameters,helperTypeParameters);
        var typeParameterList = $"<{string.Join(", ",helperTypeParameters)}>";
        staticMethods = $$"""
            private static {{returnType}} {{helperName}}{{typeParameterList}}({{string.Join(", ",parameterDeclarations)}}) {
                return global::System.Linq.Enumerable.{{definition.Name}}({{string.Join(", ",callArguments)}});
            }
            """;
        return true;
    }

    // 「型推論のみ」パターンでSelectを生成：デリゲートを型推論に使い、ラムダ本体を直接インライン展開
    private static bool 匿名型Select生成53(
        StringBuilder builder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ExpressionSyntax sourceExpression,
        ExpressionSyntax selectorExpression,
        ITypeSymbol sourceElementType,
        Dictionary<ITypeSymbol,string> anonymousTypeParameters,
        List<string> helperTypeParameters,
        string helperName,
        string actualVariableName,
        List<string>? unsafeAccessorCollector,
        HashSet<string> usedNames,
        out string staticMethods) {
        staticMethods=string.Empty;

        // selectorがラムダ式の場合のみ対応
        if(selectorExpression is not LambdaExpressionSyntax selectorLambda) {
            return false;
        }

        if(!TryGetLambdaExpressionBody(selectorLambda, out var lambdaBody)) {
            return false;
        }

        var parameters = GetLambdaParameters(selectorLambda);
        if(parameters.Count == 0 || parameters.Count > 2) {
            return false;
        }

        // 結果型を取得
        var resultType = semanticModel.GetTypeInfo(invocation).Type;
        if(resultType is not INamedTypeSymbol { TypeArguments.Length: 1 } resultNamedType) {
            return false;
        }

        var resultElementType = resultNamedType.TypeArguments[0];
        var sourceTypeName = RenderGeneratedTypeName(sourceElementType, anonymousTypeParameters, helperTypeParameters);
        var resultTypeName = RenderGeneratedTypeName(resultElementType, anonymousTypeParameters, helperTypeParameters);

        if(helperTypeParameters.Count == 0) {
            return false;
        }

        var sourceText = RenderExpressionForGeneratedMethod(sourceExpression, semanticModel, containingType, unsafeAccessorCollector);
        var typeParameterList = $"<{string.Join(", ", helperTypeParameters)}>";

        // 型推論ヘルパーメソッドを生成（デリゲートは型推論のみに使用）
        var helperMethodName = $"{helperName}_CreateList";
        var selectorRendered = RenderExpressionForGeneratedMethod(selectorExpression, semanticModel, containingType, unsafeAccessorCollector);

        // ブロックスコープ（{}内）の変数は兄弟ブロック間で再利用可能なため、
        // 追加しない FindUniqueIdentifier で衝突のみチェックする。
        var itemVar = FindUniqueIdentifier(parameters[0], usedNames);
        var indexVar = parameters.Count == 2 ? FindUniqueIdentifier(parameters[1], usedNames) : null;
        var tempVar = FindUniqueIdentifier("temp", usedNames);

        var lambdaBodyInlined = RenderExpressionForGeneratedMethod(lambdaBody, semanticModel, containingType, unsafeAccessorCollector);
        // パラメータ名が itemVar/indexVar に変わった場合のみ本体を置換する
        if(itemVar != parameters[0]) {
            lambdaBodyInlined = lambdaBodyInlined.Replace(parameters[0], itemVar, StringComparison.Ordinal);
        }
        if(indexVar is not null && indexVar != parameters[1]) {
            lambdaBodyInlined = lambdaBodyInlined.Replace(parameters[1], indexVar, StringComparison.Ordinal);
        }

        // ループ展開コードを生成（ブロックスコープで重複宣言を防ぐ）
        builder.AppendLine($"var {actualVariableName} = {helperMethodName}({selectorRendered});");
        builder.AppendLine("{");
        if(indexVar is not null) {
            builder.AppendLine($"var {indexVar} = 0;");
        }
        builder.AppendLine($"foreach(var {itemVar} in {sourceText}) {{");
        builder.AppendLine($"    var {tempVar} = {lambdaBodyInlined};");
        builder.AppendLine($"    {actualVariableName}.Add({tempVar});");
        if(indexVar is not null) {
            builder.AppendLine($"    {indexVar}++;");
        }
        builder.AppendLine("}");
        builder.AppendLine("}");

        // 型推論ヘルパーメソッドを生成
        var funcType = parameters.Count == 1
            ? $"global::System.Func<{sourceTypeName}, {resultTypeName}>"
            : $"global::System.Func<{sourceTypeName}, int, {resultTypeName}>";

        staticMethods = $$"""
            private static global::System.Collections.Generic.List<{{resultTypeName}}> {{helperMethodName}}{{typeParameterList}}({{funcType}} _) {
                return new global::System.Collections.Generic.List<{{resultTypeName}}>();
            }
            """;
        return true;
    }

    // 「型推論のみ」パターンでGroupByを生成：デリゲートを型推論に使い、ラムダ本体を直接インライン展開
    private static bool 匿名型GroupBy生成54(
        StringBuilder builder,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ExpressionSyntax sourceExpression,
        int argumentStartIndex,
        SeparatedSyntaxList<ArgumentSyntax> invocationArguments,
        IMethodSymbol fragmentMethod,
        ITypeSymbol sourceElementType,
        Dictionary<ITypeSymbol,string> anonymousTypeParameters,
        List<string> helperTypeParameters,
        string helperName,
        string actualVariableName,
        List<string>? unsafeAccessorCollector,
        HashSet<string> usedNames,
        out string staticMethods) {
        staticMethods=string.Empty;

        // keySelectorがラムダ式の場合のみ対応
        if(invocationArguments.Count < argumentStartIndex + 1) {
            return false;
        }

        var keySelectorExpression = invocationArguments[argumentStartIndex].Expression;
        if(keySelectorExpression is not LambdaExpressionSyntax keySelectorLambda) {
            return false;
        }

        if(!TryGetLambdaExpressionBody(keySelectorLambda, out var keyLambdaBody)) {
            return false;
        }

        var keyParameters = GetLambdaParameters(keySelectorLambda);
        if(keyParameters.Count != 1) {
            return false;
        }

        // GroupBy(keySelector, resultSelector) オーバーロード:
        // 各グループ（IGrouping<TKey,TSource>）を resultSelector (key, elements)=>TResult で射影し List<TResult> を作る。
        if(invocationArguments.Count >= argumentStartIndex + 2
            && invocationArguments[argumentStartIndex + 1].Expression is LambdaExpressionSyntax resultSelectorLambda
            && TryGetLambdaExpressionBody(resultSelectorLambda, out var resultLambdaBody)) {
            var resultParameters = GetLambdaParameters(resultSelectorLambda);
            if(resultParameters.Count == 2
                && semanticModel.GetTypeInfo(invocation).Type is INamedTypeSymbol { TypeArguments.Length: 1 } resultSequenceType
                && semanticModel.GetTypeInfo(keyLambdaBody).Type is { } keyTypeSymbol) {
                var resultElementType = resultSequenceType.TypeArguments[0];
                var groupSourceTypeName = RenderGeneratedTypeName(sourceElementType, anonymousTypeParameters, helperTypeParameters);
                var groupKeyTypeName = RenderGeneratedTypeName(keyTypeSymbol, anonymousTypeParameters, helperTypeParameters);
                var resultElementTypeName = RenderGeneratedTypeName(resultElementType, anonymousTypeParameters, helperTypeParameters);
                if(helperTypeParameters.Count == 0) {
                    return false;
                }

                var groupSourceText = RenderExpressionForGeneratedMethod(sourceExpression, semanticModel, containingType, unsafeAccessorCollector);
                var groupTypeParameterList = $"<{string.Join(", ", helperTypeParameters)}>";
                var dictionaryHelperName = $"{helperName}_CreateDictionary";
                var resultListHelperName = $"{helperName}_CreateResultList";
                var groupKeySelectorRendered = RenderExpressionForGeneratedMethod(keySelectorExpression, semanticModel, containingType, unsafeAccessorCollector);
                var resultSelectorRendered = RenderExpressionForGeneratedMethod(invocationArguments[argumentStartIndex + 1].Expression, semanticModel, containingType, unsafeAccessorCollector);

                var groupItemVar = FindUniqueIdentifier("item", usedNames);
                var groupKeyVar = FindUniqueIdentifier("key", usedNames);
                var groupBucketVar = FindUniqueIdentifier("group", usedNames);
                var groupingVar = FindUniqueIdentifier("grouping", usedNames);

                var groupKeyBodyInlined = RenderExpressionForGeneratedMethod(keyLambdaBody, semanticModel, containingType, unsafeAccessorCollector);
                groupKeyBodyInlined = groupKeyBodyInlined.Replace(keyParameters[0], groupItemVar, StringComparison.Ordinal);
                // resultSelector 本体は引数名のまま埋め込み、各引数を同名のローカルへ束縛して匿名型のメンバー名を保つ。
                var resultBodyInlined = RenderExpressionForGeneratedMethod(resultLambdaBody, semanticModel, containingType, unsafeAccessorCollector);

                builder.AppendLine($"var {actualVariableName}_dict = {dictionaryHelperName}({groupKeySelectorRendered});");
                builder.AppendLine($"var {actualVariableName}_groups = new global::System.Collections.Generic.List<{helperName}_Grouping>();");
                builder.AppendLine($"foreach(var {groupItemVar} in {groupSourceText}) {{");
                builder.AppendLine($"    var {groupKeyVar} = {groupKeyBodyInlined};");
                builder.AppendLine($"    if(!{actualVariableName}_dict.TryGetValue({groupKeyVar}, out var {groupBucketVar})) {{");
                builder.AppendLine($"        {groupBucketVar} = new {helperName}_Grouping({groupKeyVar});");
                builder.AppendLine($"        {actualVariableName}_dict.Add({groupKeyVar}, {groupBucketVar});");
                builder.AppendLine($"        {actualVariableName}_groups.Add({groupBucketVar});");
                builder.AppendLine($"    }}");
                builder.AppendLine($"    {groupBucketVar}.Add({groupItemVar});");
                builder.AppendLine("}");
                builder.AppendLine($"var {actualVariableName}_projected = {resultListHelperName}({resultSelectorRendered});");
                builder.AppendLine($"foreach(var {groupingVar} in {actualVariableName}_groups) {{");
                builder.AppendLine($"    {groupKeyTypeName} {resultParameters[0]} = {groupingVar}.Key;");
                builder.AppendLine($"    global::System.Collections.Generic.IEnumerable<{groupSourceTypeName}> {resultParameters[1]} = {groupingVar};");
                builder.AppendLine($"    {actualVariableName}_projected.Add({resultBodyInlined});");
                builder.AppendLine("}");
                builder.AppendLine($"var {actualVariableName} = {actualVariableName}_projected;");

                staticMethods = $$"""
                    private sealed class {{helperName}}_Grouping : global::System.Linq.IGrouping<{{groupKeyTypeName}}, {{groupSourceTypeName}}> {
                        private readonly global::System.Collections.Generic.List<{{groupSourceTypeName}}> values = new();
                        public {{helperName}}_Grouping({{groupKeyTypeName}} key) {
                            this.Key = key;
                        }
                        public {{groupKeyTypeName}} Key { get; }
                        public void Add({{groupSourceTypeName}} value) => values.Add(value);
                        public global::System.Collections.Generic.IEnumerator<{{groupSourceTypeName}}> GetEnumerator() => values.GetEnumerator();
                        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
                    }
                    private static global::System.Collections.Generic.Dictionary<{{groupKeyTypeName}}, {{helperName}}_Grouping> {{dictionaryHelperName}}{{groupTypeParameterList}}(global::System.Func<{{groupSourceTypeName}}, {{groupKeyTypeName}}> _) {
                        return new global::System.Collections.Generic.Dictionary<{{groupKeyTypeName}}, {{helperName}}_Grouping>();
                    }
                    private static global::System.Collections.Generic.List<{{resultElementTypeName}}> {{resultListHelperName}}{{groupTypeParameterList}}(global::System.Func<{{groupKeyTypeName}}, global::System.Collections.Generic.IEnumerable<{{groupSourceTypeName}}>, {{resultElementTypeName}}> _) {
                        return new global::System.Collections.Generic.List<{{resultElementTypeName}}>();
                    }
                    """;
                return true;
            }
        }

        // 結果型を取得
        var resultType = semanticModel.GetTypeInfo(invocation).Type;
        if(resultType is not INamedTypeSymbol { TypeArguments.Length: 2 } resultNamedType) {
            return false;
        }

        var keyType = resultNamedType.TypeArguments[0];
        var sourceTypeName = RenderGeneratedTypeName(sourceElementType, anonymousTypeParameters, helperTypeParameters);
        var keyTypeName = RenderGeneratedTypeName(keyType, anonymousTypeParameters, helperTypeParameters);

        if(helperTypeParameters.Count == 0) {
            return false;
        }

        var sourceText = RenderExpressionForGeneratedMethod(sourceExpression, semanticModel, containingType, unsafeAccessorCollector);
        var typeParameterList = $"<{string.Join(", ", helperTypeParameters)}>";

        // 型推論ヘルパーメソッドを生成
        var helperMethodName = $"{helperName}_CreateDictionary";
        var keySelectorRendered = RenderExpressionForGeneratedMethod(keySelectorExpression, semanticModel, containingType, unsafeAccessorCollector);

        // foreach/if スコープの変数は兄弟ブロック間で再利用可能なため追加しない FindUniqueIdentifier を使う
        var itemVar = FindUniqueIdentifier("item", usedNames);
        var keyVar = FindUniqueIdentifier("key", usedNames);
        var groupVar = FindUniqueIdentifier("group", usedNames);

        var keyBodyInlined = RenderExpressionForGeneratedMethod(keyLambdaBody, semanticModel, containingType, unsafeAccessorCollector);
        keyBodyInlined = keyBodyInlined.Replace(keyParameters[0], itemVar, StringComparison.Ordinal);

        // ループ展開コードを生成
        builder.AppendLine($"var {actualVariableName}_dict = {helperMethodName}({keySelectorRendered});");
        builder.AppendLine($"var {actualVariableName}_result = new global::System.Collections.Generic.List<global::System.Linq.IGrouping<{keyTypeName}, {sourceTypeName}>>();");
        builder.AppendLine($"foreach(var {itemVar} in {sourceText}) {{");
        builder.AppendLine($"    var {keyVar} = {keyBodyInlined};");
        builder.AppendLine($"    if(!{actualVariableName}_dict.TryGetValue({keyVar}, out var {groupVar})) {{");
        builder.AppendLine($"        {groupVar} = new {helperName}_Grouping({keyVar});");
        builder.AppendLine($"        {actualVariableName}_dict.Add({keyVar}, {groupVar});");
        builder.AppendLine($"        {actualVariableName}_result.Add({groupVar});");
        builder.AppendLine($"    }}");
        builder.AppendLine($"    {groupVar}.Add({itemVar});");
        builder.AppendLine("}");
        builder.AppendLine($"var {actualVariableName} = {actualVariableName}_result;");

        // 型推論ヘルパーメソッドとGroupingクラスを生成
        staticMethods = $$"""
            private sealed class {{helperName}}_Grouping : global::System.Linq.IGrouping<{{keyTypeName}}, {{sourceTypeName}}> {
                private readonly global::System.Collections.Generic.List<{{sourceTypeName}}> values = new();
                public {{helperName}}_Grouping({{keyTypeName}} key) {
                    this.Key = key;
                }
                public {{keyTypeName}} Key { get; }
                public void Add({{sourceTypeName}} value) => values.Add(value);
                public global::System.Collections.Generic.IEnumerator<{{sourceTypeName}}> GetEnumerator() => values.GetEnumerator();
                global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
            }
            private static global::System.Collections.Generic.Dictionary<{{keyTypeName}}, {{helperName}}_Grouping> {{helperMethodName}}{{typeParameterList}}(global::System.Func<{{sourceTypeName}}, {{keyTypeName}}> _) {
                return new global::System.Collections.Generic.Dictionary<{{keyTypeName}}, {{helperName}}_Grouping>();
            }
            """;
        return true;
    }

    private static string RenderGeneratedTypeName(
        ITypeSymbol type,
        Dictionary<ITypeSymbol,string> anonymousTypeParameters,
        List<string> helperTypeParameters) {
        if(type.IsAnonymousType) {
            if(!anonymousTypeParameters.TryGetValue(type,out var typeParameterName)) {
                typeParameterName=$"TAnonymous{anonymousTypeParameters.Count}";
                anonymousTypeParameters.Add(type,typeParameterName);
                helperTypeParameters.Add(typeParameterName);
            }
            return typeParameterName;
        }

        return type switch {
            IArrayTypeSymbol arrayType => $"{RenderGeneratedTypeName(arrayType.ElementType,anonymousTypeParameters,helperTypeParameters)}[]",
            IPointerTypeSymbol pointerType => $"{RenderGeneratedTypeName(pointerType.PointedAtType,anonymousTypeParameters,helperTypeParameters)}*",
            INamedTypeSymbol { TypeArguments.Length: > 0 } namedType => $"{OpenGenericTypeName(namedType)}<{string.Join(", ",namedType.TypeArguments.Select(typeArgument => RenderGeneratedTypeName(typeArgument,anonymousTypeParameters,helperTypeParameters)))}>",
            _ => type.ToDisplayString(完全修飾形式),
        };
    }

    private static string OpenGenericTypeName(INamedTypeSymbol namedType) {
        var definitionName = namedType.OriginalDefinition.ToDisplayString(完全修飾形式);
        var typeArgumentStart = definitionName.IndexOf('<',StringComparison.Ordinal);
        return typeArgumentStart<0 ? definitionName : definitionName[..typeArgumentStart];
    }

    private static string ローカル宣言生成17(
        LocalDeclarationStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        IReadOnlyDictionary<string,string>? replacementMap = null,
        List<string>? unsafeAccessorCollector = null) {
        var variables = statement.Declaration.Variables.Select(variable => {
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

    private static string 変数宣言生成43(
        VariableDeclarationSyntax declaration,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var variables = declaration.Variables.Select(variable => {
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

    private static string RenderStatementForGeneratedMethod(
        StatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        // replacementMap のキーは NormalizeExpressionText 済みなので text も正規化して照合する。
        var text = NormalizeExpressionText(statement.ToString());
        if(replacementMap is not null) {
            foreach(var pair in replacementMap.OrderByDescending(static pair => pair.Key.Length)) {
                text=text.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
            }
        }
        return text
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);
    }

    private static List<InvocationExpressionSyntax> 外側LINQ呼び出し取得41(StatementSyntax statement,SemanticModel semanticModel) {
        return statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            // 拡張メソッド呼び出しのみを対象とし、out/ref パラメータ持ちは除外する
            .Where(invocation => SystemLinqEnumerable呼び出し判定42(invocation,semanticModel)
                && semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol { ReducedFrom: not null } method
                && !(method.ReducedFrom ?? method).Parameters.Any(static p => p.RefKind != RefKind.None))
            .Where(invocation => !invocation.Ancestors()
                .TakeWhile(ancestor => ancestor!=statement)
                .OfType<LambdaExpressionSyntax>()
                .Any())
            .Where(invocation => !invocation.Ancestors()
                .TakeWhile(ancestor => ancestor!=statement)
                .OfType<InvocationExpressionSyntax>()
                .Any(ancestorInvocation => SystemLinqEnumerable呼び出し判定42(ancestorInvocation,semanticModel)))
            .OrderBy(static invocation => invocation.SpanStart)
            .ToList();
    }

    private static bool SystemLinqEnumerable呼び出し判定42(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }
        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType=="System.Linq.Enumerable"
            || (definition.Name=="Contains" && containingType=="System.MemoryExtensions");
    }

    private static int フラグメント番号初期値取得18() => 0;

    // Struct0 / Struct1 ... のような補助型はクラス直下だと他のインターセプターと衝突するため、
    // メソッド単位の補助クラスへ退避し、本体側の参照だけを差し替える。
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

    private static string 補助Struct関数AggressiveInlining付与43(string source) {
        const string attribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
        return System.Text.RegularExpressions.Regex.Replace(
            source,
            @"(?m)^(?<indent>\s*)private static (?<declaration>.+\b(?:Compute|Build)\s*\()",
            match => {
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

    // baseName を試し、衝突したら baseName0、baseName1... と増やして未使用名を返す。
    // 選んだ名前は usedNames に追加され、同一パス内での二重取得を防ぐ（メソッドスコープ変数用）。
    private static string GetUniqueIdentifier(string baseName, HashSet<string> usedNames) {
        if(usedNames.Add(baseName)) return baseName;
        for(var i = 0; ; i++) {
            var candidate = $"{baseName}{i}";
            if(usedNames.Add(candidate)) return candidate;
        }
    }

    // ブロックスコープの変数名を決める際に使う読み取り専用バリアント。
    // usedNames を参照して衝突を検査するが追加はしない。
    // 同じ名前を複数の兄弟ブロックで再利用できるため、foreach/{ } 内の変数に使う。
    private static string FindUniqueIdentifier(string baseName, HashSet<string> usedNames) {
        if(!usedNames.Contains(baseName)) return baseName;
        for(var i = 0; ; i++) {
            var candidate = $"{baseName}{i}";
            if(!usedNames.Contains(candidate)) return candidate;
        }
    }

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

    private static string 名前空間テキスト取得(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.IsGlobalNamespace ? string.Empty : namespaceSymbol.ToDisplayString();
    }

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
        => methodName=="Any" || フラグメント候補一覧.ContainsKey(methodName);

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

    private static List<string> GetAnonymousMethodParameters(AnonymousMethodExpressionSyntax anonymousMethod) =>
        anonymousMethod.ParameterList?.Parameters
            .Select(static parameter => parameter.Identifier.ValueText)
            .ToList() ?? [];

    // 複数文ブロック本体（try-catch 含む）を文インライン展開する BlockFunc を構築する。
    // return 文は __SEAMLESS_DEST__ への代入 + goto __SEAMLESS_LABEL__ へ書き換える。
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

        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Count!=parameterTypeNames.Length) {
            return false;
        }

        // return をプレースホルダ代入 + goto に書き換える。ネストした lambda/local function には降りない。
        var rewritten = (BlockSyntax)new ReturnToResultRewriter().Visit(block)!;
        var bodyText = string.Join(
            "\n",
            rewritten.Statements.Select(statement => NormalizeGeneratedExpressionText(statement.ToString())));

        var capturedLocals = Array.Empty<(string Name, string TypeName)>();
        if(semanticModel is not null && block.Statements.Count>0) {
            var dataFlow = semanticModel.AnalyzeDataFlow(block.Statements[0],block.Statements[^1]);
            if(dataFlow?.Succeeded==true) {
                var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
                capturedLocals = dataFlow.DataFlowsIn
                    .Where(sym => (sym is ILocalSymbol || sym is IParameterSymbol) && !paramSet.Contains(sym.Name) && sym.Name!="this")
                    .Select(sym => (sym.Name, sym is ILocalSymbol loc
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

    // ブロック本体内の return 文を __SEAMLESS_DEST__ = (expr); goto __SEAMLESS_LABEL__; へ書き換える。
    // ネストした lambda / 匿名メソッド / local function の return は対象外（中へ降りない）。
    private sealed class ReturnToResultRewriter : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;
        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;
        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => node;
        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => node;
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node) {
            var statementText = node.Expression is { } expr
                ? $"{{ __SEAMLESS_DEST__ = ({expr}); goto __SEAMLESS_LABEL__; }}"
                : "{ goto __SEAMLESS_LABEL__; }";
            return SyntaxFactory.ParseStatement(statementText);
        }
    }

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

        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
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
            .Select(static parameter => parameter.Identifier.ValueText)
            .ToArray() ?? [];
        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
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

    private static bool IsIEnumerableType(INamedTypeSymbol namedType)
        => namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            && namedType.MetadataName=="IEnumerable`1";

    private static bool IsNonGenericIEnumerableType(ITypeSymbol? type)
        => type is INamedTypeSymbol namedType
            && (IsNonGenericIEnumerableType(namedType)
                || namedType.AllInterfaces.Any(IsNonGenericIEnumerableType));

    private static bool IsNonGenericIEnumerableType(INamedTypeSymbol namedType)
        => namedType.ContainingNamespace.ToDisplayString()=="System.Collections"
            && namedType.MetadataName=="IEnumerable";

    private static bool ContainsAnonymousType(ITypeSymbol type) {
        if(type.IsAnonymousType) {
            return true;
        }

        // 配列やジェネリックの内側に匿名型が埋まっていても生成署名には出せないので再帰的に見る。
        return type switch {
            IArrayTypeSymbol arrayType => ContainsAnonymousType(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(ContainsAnonymousType),
            _ => false,
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
            ITypeParameterSymbol => true,
            IArrayTypeSymbol arrayType => ContainsTypeParameter(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(ContainsTypeParameter),
            IPointerTypeSymbol pointerType => ContainsTypeParameter(pointerType.PointedAtType),
            _ => false,
        };
    }

    private static bool ContainsGeneratedTypeAccessibilityRisk(ITypeSymbol type) {
        // 生成クラスから見えない型がどこかに混ざると署名生成時点でコンパイル不能になる。
        return type switch {
            IArrayTypeSymbol arrayType => ContainsGeneratedTypeAccessibilityRisk(arrayType.ElementType),
            IPointerTypeSymbol pointerType => ContainsGeneratedTypeAccessibilityRisk(pointerType.PointedAtType),
            INamedTypeSymbol namedType => NamedTypeAccessibilityRisk(namedType) || namedType.TypeArguments.Any(ContainsGeneratedTypeAccessibilityRisk),
            _ => false,
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

    private static List<string> GetLambdaParameters(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simpleLambda) {
            return [simpleLambda.Parameter.Identifier.ValueText];
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda) {
            return parenthesizedLambda.ParameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToList();
        }
        return [];
    }

    private static string NormalizeExpressionText(string value) {
        return value.Replace("\r\n"," ").Replace('\n',' ').Trim();
    }

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

        return $"<{string.Join(", ",method.TypeArguments.Select(static type => type.ToDisplayString(完全修飾形式)))}>";
    }

    private static string RenderConstantValue(object? value) => value switch {
        null => "null",
        string text => "@\"" + text.Replace("\"","\"\"",StringComparison.Ordinal) + "\"",
        char ch => "'" + ch.ToString().Replace("\\","\\\\",StringComparison.Ordinal).Replace("'","\\'",StringComparison.Ordinal) + "'",
        bool boolean => boolean ? "true" : "false",
        float single => single.ToString("R",System.Globalization.CultureInfo.InvariantCulture) + "f",
        double number => number.ToString("R",System.Globalization.CultureInfo.InvariantCulture) + "d",
        decimal number => number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
        long number => number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
        ulong number => number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL",
        uint number => number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "U",
        _ => Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture) ?? "default!",
    };

    private static string RefKind接頭辞取得35(SyntaxKind syntaxKind) => syntaxKind switch {
        SyntaxKind.RefKeyword => "ref ",
        SyntaxKind.OutKeyword => "out ",
        SyntaxKind.InKeyword => "in ",
        _ => string.Empty,
    };

    private static string RenderExpressionForGeneratedMethod(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector = null,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        var text = NormalizeGeneratedExpressionText(expression.ToString());

        if(replacementMap is not null) {
            foreach(var pair in replacementMap.OrderByDescending(static pair => pair.Key.Length)) {
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

    private static bool InvocationHasExplicitTypeArguments(InvocationExpressionSyntax invocation) =>
        invocation.Expression is GenericNameSyntax
        || invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax };

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

    private static bool TryCreateOrdinaryActualArgumentList(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap,
        out List<string> actualArguments) {
        actualArguments=[];
        var slots = new string?[method.Parameters.Length];
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

        actualArguments=slots.Select(static slot => slot!).ToList();
        return true;
    }

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
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty,
        };
        return $"{prefix}{parameter.Type.ToDisplayString(完全修飾形式)} {parameter.Name}";
    }

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
            || invocation.ArgumentList.Arguments.Any(static argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None))) {
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

        actualArguments=actualArgumentSlots.Select(static argument => argument!).ToList();
        return true;
    }

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

    private static string ReplaceInlineParameterReferences(string expressionText,ImmutableArray<IParameterSymbol> parameters,IReadOnlyList<string> actualArguments) {
        var replacements = new Dictionary<string,string>(StringComparer.Ordinal);
        for(var i=0;i<parameters.Length;i++) {
            replacements[parameters[i].Name]=actualArguments[i];
        }

        var expression = SyntaxFactory.ParseExpression(expressionText);
        return NormalizeExpressionText(new InlineParameterReferenceRewriter(replacements).Visit(expression)!.ToString());
    }

    private sealed class InlineParameterReferenceRewriter : CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,string> replacements;

        public InlineParameterReferenceRewriter(IReadOnlyDictionary<string,string> replacements) {
            this.replacements=replacements;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(!replacements.TryGetValue(node.Identifier.ValueText,out var replacement)
                || node.Parent is MemberAccessExpressionSyntax { Name: var memberName } && ReferenceEquals(memberName,node)
                || node.Parent is MemberBindingExpressionSyntax { Name: var bindingName } && ReferenceEquals(bindingName,node)
                || node.Parent is NameColonSyntax
                || node.Parent is NameEqualsSyntax) {
                return node;
            }

            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.ParseExpression(replacement)).WithTriviaFrom(node);
        }
    }

    private static string RenderInlineFailureInvocationExpression(IMethodSymbol method) {
        var message = EscapeStringLiteral($"Local helper method '{method.Name}' could not be inlined. Forwarding is disabled.");
        return method.ReturnsVoid
            ? $"((global::System.Action)(() => throw global::Seamless.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\")))()"
            : $"((global::System.Func<{method.ReturnType.ToDisplayString(完全修飾形式)}>)(() => throw global::Seamless.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\")))()";
    }

    private static string EscapeStringLiteral(string text) =>
        text.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);

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
            var paramNames = method.Parameters.Select(static (_, i) => $"__a{i}").ToArray();
            var paramList = string.Join(", ",paramNames);
            var argList = string.Join(", ",new[] { receiver }.Concat(paramNames));
            return $"(({delegateTypeName})(({paramList}) => {accessorName}({argList})))";
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
            var paramNames = method.Parameters.Select(static (_, i) => $"__a{i}").ToArray();
            var lambdaParams = string.Join(", ",paramNames);
            var lambdaArgs = "null" + (paramNames.Length > 0 ? ", " + string.Join(", ",paramNames) : string.Empty);
            return $"(({delegateTypeName})(({lambdaParams}) => {accessorName}({lambdaArgs})))";
        }
    }

    private static string RenderUnsupportedLocalMethodBody(IReadOnlyList<string> unsupportedMethods) {
        // Fragment クラスが存在しない Enumerable メソッドは、元実装へ黙って戻さず例外にする。
        // これにより「intercept 対象だったが未対応だった」ことを生成コードと実行時の両方で確認できる。
        var methodList = string.Join(", ",unsupportedMethods);
        return $"throw global::Seamless.GeneratedCodeHelpers.LinqInterceptorFailure(\"Local method interception does not support System.Linq.Enumerable methods without Fragment implementations: {methodList}.\");";
    }

    private static string RenderUninlineableLocalMethodBody(Linq傍受対象型 target) {
        // Fragment クラス名は存在しても、source 形状や delegate 形状の都合で最終的に再構成できないことがある。
        // その場合も静かに元実装へ戻さず失敗を見える化する。
        var methodName = target.Method.ToDisplayString(完全修飾形式)
            .Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("\"","\\\"",StringComparison.Ordinal);
        return $"throw global::Seamless.GeneratedCodeHelpers.LinqInterceptorFailure(\"Local method interception could not inline '{methodName}'. Forwarding is disabled for methods that contain System.Linq.Enumerable calls.\");";
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
        return $"throw global::Seamless.GeneratedCodeHelpers.LinqInterceptorFailure(\"{message}\");";
    }

    private sealed class ローカルメソッドLinq解析結果 {
        public static readonly ローカルメソッドLinq解析結果 Empty = new(false,ImmutableArray<string>.Empty,false);

        public ローカルメソッドLinq解析結果(bool hasEnumerableCall,ImmutableArray<string> unsupportedMethods,bool hasAnonymousTypeLinq) {
            HasEnumerableCall=hasEnumerableCall;
            UnsupportedMethods=unsupportedMethods;
            HasAnonymousTypeLinq=hasAnonymousTypeLinq;
        }

        public bool HasEnumerableCall { get; }
        public ImmutableArray<string> UnsupportedMethods { get; }
        /// <summary>
        /// メソッド本体内の LINQ 呼び出しに匿名型が含まれる場合 true。
        /// 匿名型は生成メソッドの署名に出せないため完全なフラグメント最適化ができず、
        /// 該当 LINQ 呼び出しは IEnumerable&lt;T&gt; 汎用ヘルパーへ委譲する部分最適化になる。
        /// ValueTuple や struct record を使えば完全最適化が可能になるため、
        /// この値が true のとき SEAMLESS001 警告を発行してユーザーに通知する。
        /// </summary>
        public bool HasAnonymousTypeLinq { get; }
    }

    private sealed class Linq傍受対象型 {
        public Linq傍受対象型(string identifier,string containingMethodName,string interceptsLocationAttributeText,IMethodSymbol method,IMethodSymbol templateMethod,InvocationExpressionSyntax invocation,INamedTypeSymbol enclosingType,SemanticModel semanticModel,InterceptParameterSpec[] parameters,ローカルメソッドLinq解析結果? linqAnalysis = null,string? sourceElementTypeName = null,string[]? methodTypeParameterNames = null,bool isLocalMethodInterception = false) {
            Identifier=identifier;
            ContainingMethodName=containingMethodName;
            InterceptsLocationAttributeText=interceptsLocationAttributeText;
            Method=method;
            TemplateMethod=templateMethod;
            Invocation=invocation;
            EnclosingType=enclosingType;
            SemanticModel=semanticModel;
            Parameters=parameters;
            LinqAnalysis=linqAnalysis ?? ローカルメソッドLinq解析結果.Empty;
            ExplicitSourceElementTypeName=sourceElementTypeName;
            MethodTypeParameters=methodTypeParameterNames ?? [];
            IsLocalMethodInterception=isLocalMethodInterception;
        }

        public string Identifier { get; }
        public string ContainingMethodName { get; }
        public string InterceptsLocationAttributeText { get; }
        public IMethodSymbol Method { get; }
        public IMethodSymbol TemplateMethod { get; }
        public InvocationExpressionSyntax Invocation { get; }
        public string SourcePath => Invocation.SyntaxTree.FilePath;
        public INamedTypeSymbol EnclosingType { get; }
        public SemanticModel SemanticModel { get; }
        public InterceptParameterSpec[] Parameters { get; }
        public ローカルメソッドLinq解析結果 LinqAnalysis { get; }
        public string? ExplicitSourceElementTypeName { get; }
        public string[] MethodTypeParameters { get; }
        public bool IsLocalMethodInterception { get; }
        public string SourceElementTypeName {
            get {
                if(!string.IsNullOrWhiteSpace(ExplicitSourceElementTypeName)) {
                    return ExplicitSourceElementTypeName;
                }
                if(Method.TypeArguments.Length>0) {
                    return Method.TypeArguments[0].ToDisplayString(完全修飾形式);
                }
                var parameters = TemplateMethod.Parameters;
                if(parameters.Length==0
                    ||parameters[0].Type is not INamedTypeSymbol sourceType
                    ||sourceType.TypeArguments.Length!=1) {
                    throw new InvalidOperationException($"Could not determine source element type for '{TemplateMethod.ToDisplayString(完全修飾形式)}'.");
                }
                return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
            }
        }
        public string ReturnTypeName => Method.ReturnsVoid ? "void" : Method.ReturnType.ToDisplayString(完全修飾形式);
        public string InterceptorParameterList {
            get {
                if(IsLocalMethodInterception) {
                    var parameters = new List<string>();
                    if(!Method.IsStatic) {
                        parameters.Add($"this {Method.ContainingType.ToDisplayString(完全修飾形式)} @this");
                    }
                    parameters.AddRange(Parameters.Select(static parameter => parameter.Declaration));
                    return string.Join(", ",parameters);
                }

                return $"this global::System.Collections.Generic.IEnumerable<{SourceElementTypeName}> source{MethodParameterList}";
            }
        }
        public string MethodParameterList => Parameters.Length==0
            ? string.Empty
            : ", "+string.Join(", ",Parameters.Select(static parameter => parameter.Declaration));
        public string MethodTypeParameterList => MethodTypeParameters.Length==0
            ? string.Empty
            : "<"+string.Join(", ",MethodTypeParameters)+">";
        public string MethodName => $"OptimizedLinq_{Identifier}";
    }
    private sealed class InterceptParameterSpec {
        public InterceptParameterSpec(string name,string typeName,RefKind refKind = RefKind.None) {
            Name=name;
            TypeName=typeName;
            RefKind=refKind;
        }

        public string Name { get; }
        public string TypeName { get; }
        public RefKind RefKind { get; }

        public string Declaration => $"{RefKindModifier}{TypeName} {Name}";

        private string RefKindModifier => RefKind switch {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty,
        };
    }
    private sealed class LambdaSpec {
        public LambdaSpec(string returnTypeName,string[] parameterTypeNames,string[] parameterNames,string body) {
            ReturnTypeName=returnTypeName;
            ParameterTypeNames=parameterTypeNames;
            ParameterNames=parameterNames;
            Body=body;
        }
        public string ReturnTypeName { get; }
        public string[] ParameterTypeNames { get; }
        public string[] ParameterNames { get; }
        public string Body { get; }
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
    private sealed class 型対象型 {
        public 型対象型(INamedTypeSymbol symbol,bool isPartial) {
            Symbol=symbol;
            IsPartial=isPartial;
        }
        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }
}
