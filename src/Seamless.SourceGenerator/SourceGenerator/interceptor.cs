/*
2026/05/23
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
		Fragmentsフォルダの下のクラスが各メソッドをためのコード片になる
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
		privateメンバーにアクセスする場合があるのでUnsafeAccessorを使ってアクセスする
		全てはFragment継承クラスで定義してあるものに従い、interceptor.csで例外的な処理は入れない。
		EnumeratorベースのFragmentでは、static/capturing lambdaを安易にFunc delegateとしてprimary constructorに渡さない。
		Where/Select/TakeWhile/SkipWhileなどのループ展開で式として安全に展開できるLambdaFuncは、
		TryGetNext内へFuncInvoke/FuncAssignで直接埋め込み、生成g.cs上でもselector(item)ではなくitem * 2のような元ラムダ式が見える形を優先する。
		GeneratedMethodFragments的なSystem.Linq.Enumerable.Xxx(...)再呼び出しfallbackは追加しない。
		LINQメソッドごとに専用Fragmentを作り、enumeratorの状態遷移として意味を表現する。
        /// <summary>コメントタグで <see cref="MyNamespace.AdvancedProcessor"/> を参照してください。または、特定のメソッド <see cref="ProcessData(int)"/> へジャンプします。</summary>というインターセプト元へのリンクを書いて
*/
// #define SEAMLESS_ENUMERATOR_MODE  // コメントを外すと常に Enumerator struct 方式になる（フラットループ無効化）

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Compilation,System.Collections.Concurrent.ConcurrentDictionary<IMethodSymbol,ローカルメソッドLinq解析結果>> ローカルメソッドLinq解析キャッシュ = new();

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

            var sourceGroups = interceptTargets
                .Distinct(LinqInterceptTargetComparer.Instance)
                .GroupBy(static target => target.SourcePath,StringComparer.Ordinal)
                .ToArray();
            foreach(var sourceGroup in sourceGroups) {
                var source = インターセプターソース構築(sourceGroup.ToImmutableArray());
                if(source.Length==0) {
                    continue;
                }

                productionContext.AddSource(インターセプターHintName取得(sourceGroup.Key),SourceText.From(source,Encoding.UTF8));
            }
        });
    }

    private static Linq傍受対象型? Linq傍受対象生成(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
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
        System.Threading.CancellationToken cancellationToken,
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
                : global::System.Linq.Enumerable.Empty<InvocationExpressionSyntax>();

        var unsupportedMethods = new HashSet<string>(StringComparer.Ordinal);
        var hasEnumerableCall = false;
        var hasEnumerableCallInBody = false;
        var allLinqCallsDirectExpressionStatement = true;
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

            hasEnumerableCall=true;
            // Body-only フラグ: expression-bodied methods は Body が null なので false のまま。
            // ローカルメソッドEnumerable呼び出し含む34 と すべてのLINQ呼び出しが式文直下 は
            // 旧実装と同様に Block Body のみを対象とする。
            if(methodSyntax.Body is not null) {
                hasEnumerableCallInBody=true;
            }
            if(!Enumerableメソッドフラグメント対応判定32(definition.Name)) {
                unsupportedMethods.Add(definition.Name);
            }
            if(methodSyntax.Body is not null && !Linq呼び出し式文直下判定39(invocation)) {
                allLinqCallsDirectExpressionStatement=false;
            }
        }

        return new ローカルメソッドLinq解析結果(
            hasEnumerableCall,
            hasEnumerableCallInBody,
            unsupportedMethods.OrderBy(static methodName => methodName,StringComparer.Ordinal).ToImmutableArray(),
            allLinqCallsDirectExpressionStatement);
    }

    private static bool メソッド構文Linq候補含む40(MethodDeclarationSyntax methodSyntax) {
        var invocationNodes = methodSyntax.Body is not null
            ? methodSyntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            : methodSyntax.ExpressionBody is not null
                ? methodSyntax.ExpressionBody.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                : global::System.Linq.Enumerable.Empty<InvocationExpressionSyntax>();

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

    private static bool Linq呼び出し式文直下判定39(InvocationExpressionSyntax invocation) {
        SyntaxNode node = invocation;
        while(node.Parent is InvocationExpressionSyntax or MemberAccessExpressionSyntax) {
            node = node.Parent;
        }
        return node.Parent is ExpressionStatementSyntax;
    }

    private static bool Linqメソッド判定(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        if(!definition.IsExtensionMethod
            // 対象は Enumerable 拡張メソッドだけ。
            ||definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return false;
        }

        if(definition.Parameters.Length==0
            // source 引数がない形は LINQ チェーンの source として扱えない。
            || definition.Parameters[0].Type is not INamedTypeSymbol sourceType
            ||sourceType.TypeArguments.Length!=1
            // 第1引数が IEnumerable<T> 1 本の拡張だけを Fragment 化対象にする。
            ||!IsIEnumerableType(sourceType)) {
            return false;
        }

        if(method.TypeArguments.Any(static typeArgument => ContainsAnonymousType(typeArgument) || ContainsGeneratedTypeAccessibilityRisk(typeArgument))
            // 型引数に匿名型/非公開型が混ざると生成署名が壊れる。
            || ContainsAnonymousType(method.ReturnType)
            // 匿名型戻り値は生成メソッドへ出せない。
            || ContainsGeneratedTypeAccessibilityRisk(method.ReturnType)
            // 非公開戻り値型も生成コードから参照不能。
            || method.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))
            // 匿名型引数も同様に除外。
            || method.Parameters.Any(static parameter => ContainsGeneratedTypeAccessibilityRisk(parameter.Type))
            // 非公開型引数も除外。
            || ContainsTypeParameter(method.ReturnType)
            // 開いた型パラメータを返すものはローカル再構成先へ閉じ込められない。
            || method.Parameters.Any(static parameter => ContainsTypeParameter(parameter.Type))) {
            // 引数側に型パラメータが残るケースも除外する。
            return false;
        }

        return true;
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
        return 生成ソース整形(builder.ToString());
    }

    private static string インターセプター型ソース構築(ImmutableArray<Linq傍受対象型> targets) {
        var builder = new StringBuilder();
        var typeStack = 型スタック取得(targets[0].EnclosingType);
        for(var i = 0; i<typeStack.Count; i++) {
            builder.AppendLine($"internal static class {SanitizeIdentifier(typeStack[i].Name)} {{");
        }

        var emittedMethodNames = new HashSet<string>(StringComparer.Ordinal);
        var emittedUnsafeAccessors = new HashSet<string>(StringComparer.Ordinal);
        var pendingClassLevelDeclarations = new List<string>();
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
                    } else if(ローカルメソッドEnumerable呼び出し含む34(target) && !すべてのLINQ呼び出しが式文直下(target)) {
                        インデント付き追記(builder,RenderUninlineableLocalMethodBody(target),"        ");
                    } else {
                        if(TryRenderSimpleLocalMethodBody(target,out var simpleLocalMethodBody)) {
                            インデント付き追記(builder,simpleLocalMethodBody,"        ");
                        } else {
                            var fallbackBody = RenderLocalMethodForwardBody(target,out var fallbackAccessorDeclaration);
                            if(!string.IsNullOrEmpty(fallbackAccessorDeclaration) && emittedUnsafeAccessors.Add(fallbackAccessorDeclaration)) {
                                pendingClassLevelDeclarations.Add(fallbackAccessorDeclaration);
                            }
                            インデント付き追記(builder,fallbackBody,"        ");
                        }
                    }
                    builder.AppendLine("    }");
                    builder.AppendLine();
                    continue;
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

        foreach(var decl in pendingClassLevelDeclarations) {
            インデント付き追記(builder,decl,"    ");
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
        builder.AppendLine($"        /// <summary>Intercepts <see cref=\"{cref}\" /> ({originalName}).</summary>");
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
        var sourceUnsafeAccessorCollector = new List<string>();
        var generatedFragments = new List<Fragment>();
        var 本体ビルダー = new StringBuilder();
        var linq呼び出し数 = 0;
        foreach(var 文 in メソッド本体.Statements) {
            if(!文生成16(文,セマンティックモデル,対象.EnclosingType,ref フラグメント番号,対象.Method.IsStatic,ref 静的メソッド群,sourceUnsafeAccessorCollector,generatedFragments,本体ビルダー,ref linq呼び出し数)) {
                return false;
            }
        }

        if(linq呼び出し数==0) {
            return false;
        }

        本体=本体ビルダー
            .ToString()
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

        // Select/Where のラムダ本体がスカラー LINQ 式なら struct インライン化。
        if(methodName=="Select"
            && Select_WithInlinedScalar.TryBuild(
                semanticModel,invocation,containingType,ref uniqueNumber,
                out fragment,out staticMethods,unsafeAccessorCollector,
                列挙フラグメント生成9,呼び出し式フラグメント生成1,
                TryGetLambdaExpressionBody,GetLambdaParameters)) {
            return true;
        }
        if(methodName=="Where"
            && Where_WithInlinedScalarPredicate.TryBuild(
                semanticModel,invocation,containingType,ref uniqueNumber,
                out fragment,out staticMethods,unsafeAccessorCollector,
                列挙フラグメント生成9,呼び出し式フラグメント生成1,
                TryGetLambdaExpressionBody,GetLambdaParameters)) {
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
                    new[] { typeof(IMethodSymbol) },
                    null);
                Func<IMethodSymbol, bool>? isApplicableDelegate = isApplicableMethod is not null
                    ? method => (bool)isApplicableMethod.Invoke(null, new object[] { method })!
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

        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            return false;
        }

        var methodName = (method.ReducedFrom ?? method).Name;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var candidate in candidates) {
            var snapshot = uniqueNumber;
            if(登録済みフラグメント生成試行26(candidate,semanticModel,invocation,sourceExpression,method,containingType,ref snapshot,out fragment,out staticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                return true;
            }
        }

        return false;
    }

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
        return フラグメント候補一覧.TryGetValue(methodName,out var candidates)
            && (candidate=candidates.FirstOrDefault(current => current.VariantName==variantName)) is not null;
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

        var argumentIndex = 0;
        for(var parameterIndex = 1; parameterIndex<candidate.Parameters.Length; parameterIndex++) {
            var parameter = candidate.Parameters[parameterIndex];
            if(argumentIndex>=invocation.ArgumentList.Arguments.Count) {
                if(parameter.ParameterType==typeof(string)) {
                    if(TryResolveSyntheticStringArgument(parameter,method,out var syntheticArgument)) {
                        constructorArguments.Add(syntheticArgument);
                        continue;
                    }

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
                if(!関数フラグメント生成12(argumentExpression,method.Parameters[argumentIndex].Type,out var funcFragment,semanticModel,containingType)
                    || !関数フラグメント利用可能28(funcFragment,candidate.FragmentType)) {
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

    private static bool 関数フラグメント利用可能28(FuncFragment funcFragment,Type fragmentType) {
        if(funcFragment.CanGenerateMethod) {
            return true;
        }

        // instance method group / delegate などは helper struct を生成できないが、
        // いまの Fragment 実装は FuncInvoke/FuncDelegate で runtime delegate 呼び出しを保持できる。
        // 以前は Select/Aggregate の一部だけ許可していたが、
        // Enumerator ベース実行へ寄せた現在は Where/All/Join なども同じ仕組みで扱える。
        if(funcFragment is ParameterFunc or LambdaFunc) {
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
        // インターフェース経由の場合は IEnumerator<T> にフォールバックする。
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
    /// </summary>
    private static bool 関数フラグメント生成12(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        out FuncFragment fragment,
        SemanticModel? semanticModel = null,
        INamedTypeSymbol? containingType = null) {
        fragment=null!;
        if(expression is LambdaExpressionSyntax lambda
            && TryCreateLambdaFuncFragment(lambda,delegateType,out var lambdaSpec)) {
            var body = lambdaSpec.Body;
            var capturedLocals = Array.Empty<(string Name, string TypeName)>();
            var bodyAccessorCodes = Array.Empty<string>();
            var capturesThis = false;
            if(semanticModel is not null && TryGetLambdaExpressionBody(lambda,out var lambdaBodyExpr)) {
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
            // キャプチャなし（this 含む）なら実質 static → struct ゼロコピーでメソッド生成可能。
            var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
                || (!capturesThis && capturedLocals.Length == 0);
            fragment = new LambdaFunc {
                Type = Func型解決13(lambdaSpec.ParameterTypeNames.Length),
                ReturnTypeName = lambdaSpec.ReturnTypeName,
                ParameterTypeNames = lambdaSpec.ParameterTypeNames,
                ParameterNames = lambdaSpec.ParameterNames,
                DefineLambda = $" => {body};",
                CanGenerateMethod = canGenerateMethod,
                CanInlineAssign = true,
                InlineBody = body,
                CapturedLocals = capturedLocals,
                BodyUnsafeAccessorCodes = bodyAccessorCodes,
            };
            return true;
        }

        if(semanticModel is not null
            && containingType is not null
            && TryCreateDelegateParameterFuncFragment(expression,delegateType,semanticModel,containingType,out fragment)) {
            return true;
        }

        return false;
    }

    private static Type Func型解決13(int parameterCount) => parameterCount switch {
        1 => typeof(Func<,>),
        2 => typeof(Func<,,>),
        _ => throw new InvalidOperationException($"Unsupported parameter count: {parameterCount}."),
    };

    private static bool TryCreateDelegateParameterFuncFragment(
        ExpressionSyntax expression,
        ITypeSymbol delegateType,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        out FuncFragment fragment) {
        fragment=null!;
        if(delegateType is not INamedTypeSymbol funcType
            || !IsInlineDelegateType(funcType)
            || funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var convertedType = semanticModel.GetTypeInfo(expression).ConvertedType;
        if(convertedType is not INamedTypeSymbol convertedFuncType
            || !IsInlineDelegateType(convertedFuncType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();

        // 非 public インスタンスメソッドグループの場合は UnsafeAccessor による直接呼び出しを設定する。
        string? unsafeAccessorMethodName = null;
        var unsafeAccessorCode = string.Empty;
        if(expression is MemberAccessExpressionSyntax methodGroupAccess
            && semanticModel.GetSymbolInfo(methodGroupAccess).Symbol is IMethodSymbol methodGroupSymbol
            && !methodGroupSymbol.IsStatic
            && methodGroupSymbol.DeclaredAccessibility!=Accessibility.Public) {
            unsafeAccessorMethodName = GetDelegateAccessorMethodName(methodGroupSymbol);
            unsafeAccessorCode = RenderDelegateUnsafeAccessorCode(methodGroupSymbol);
        }

        fragment = new ParameterFunc {
            ParameterName=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType),
            Type=Func型解決13(parameterTypeNames.Length),
            ReturnTypeName=typeArguments[^1],
            ParameterTypeNames=parameterTypeNames,
            ParameterNames=Array.Empty<string>(),
            CanGenerateMethod=false,
            CanInlineAssign=false,
            UnsafeAccessorMethodName=unsafeAccessorMethodName,
            UnsafeAccessorCode=unsafeAccessorCode,
        };
        return true;
    }

    private static string 静的メソッド連結14(string left,string right) {
        return string.Join("\n",new[] { left,right }.Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    // ローカルメソッド再構成の主経路。
    // Enumerable フラグメントがフラットループをサポートする場合は末尾メソッド内にループを展開し、
    // @this スコープで全ラムダをインライン化する。それ以外は enumerator struct + while で消費する。
    private static bool フラグメントEnumerator実行文追加37(StringBuilder builder,Fragment fragment,string actualVariableName,string actualTypeName) {
        var suffix = SanitizeIdentifier(actualVariableName);
        var enumeratorName = $"e_{suffix}";
        var currentName = $"c_{suffix}";

        if(fragment is Enumerable enumerableFragment) {
            builder.AppendLine($"var {actualVariableName}=new global::System.Collections.Generic.List<{enumerableFragment.ElementType}>();");
#if !SEAMLESS_ENUMERATOR_MODE
            if(enumerableFragment.SupportsFlatLoop) {
                // フラットループ: @this が静的メソッドスコープで有効なので全ラムダをインライン化できる。
                builder.AppendLine(enumerableFragment.GetFlatLoopCode(currentName,$"{actualVariableName}.Add({currentName});"));
            } else
#endif
            {
                string enumeratorExpression;
                try {
                    enumeratorExpression=fragment.GetEnumeratorExpression;
                } catch(NotSupportedException) {
                    return false;
                }
                builder.AppendLine($"using var {enumeratorName}={enumeratorExpression};");
                builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {currentName})) {{");
                builder.AppendLine($"    {actualVariableName}.Add({currentName});");
                builder.AppendLine("}");
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
        ref int linqInvocationCount) {
        switch(statement) {
            case BlockSyntax block:
                builder.AppendLine("{");
                foreach(var childStatement in block.Statements) {
                    if(!文生成16(childStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) {
                        return false;
                    }
                }
                builder.AppendLine("}");
                return true;
            case TryStatementSyntax tryStatement:
                return Try文生成39(tryStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case UsingStatementSyntax usingStatement:
                return Using文生成40(usingStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case LocalDeclarationStatementSyntax localDeclaration:
                return LINQ呼び出し差し替え文生成38(localDeclaration,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case ExpressionStatementSyntax expressionStatement:
                return LINQ呼び出し差し替え文生成38(expressionStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case ForStatementSyntax forStatement:
                return For文生成44(forStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case ForEachStatementSyntax foreachStatement:
                return ForEach文生成45(foreachStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case WhileStatementSyntax whileStatement:
                return While文生成46(whileStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case DoStatementSyntax doStatement:
                return Do文生成47(doStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case IfStatementSyntax ifStatement:
                return If文生成48(ifStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            case SwitchStatementSyntax switchStatement:
                return Switch文生成49(switchStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
            default:
                return LINQ呼び出し差し替え文生成38(statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
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
        ref int linqInvocationCount) {
        builder.AppendLine("try");
        if(!文生成16(statement.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) {
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
            if(!文生成16(catchClause.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) {
                return false;
            }
        }

        if(statement.Finally is not null) {
            builder.AppendLine("finally");
            if(!文生成16(statement.Finally.Block,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) {
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
        ref int linqInvocationCount) {
        if(statement.Declaration is not null) {
            builder.AppendLine($"using({変数宣言生成43(statement.Declaration,semanticModel,containingType,unsafeAccessorCollector)})");
        } else if(statement.Expression is not null) {
            builder.AppendLine($"using({RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector)})");
        } else {
            return false;
        }
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
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
        ref int linqInvocationCount) {
        var init = statement.Declaration is not null
            ? 変数宣言生成43(statement.Declaration,semanticModel,containingType,unsafeAccessorCollector)
            : string.Join(", ",statement.Initializers.Select(e => RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        var cond = statement.Condition is not null
            ? RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector)
            : string.Empty;
        var incr = string.Join(", ",statement.Incrementors.Select(e => RenderExpressionForGeneratedMethod(e,semanticModel,containingType,unsafeAccessorCollector)));
        builder.AppendLine($"for({init}; {cond}; {incr})");
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
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
        ref int linqInvocationCount) {
        var typeSymbol = semanticModel.GetTypeInfo(statement.Type).Type;
        var typeName = typeSymbol is not null
            ? typeSymbol.ToDisplayString(完全修飾形式)
            : (statement.Type is IdentifierNameSyntax { IsVar: true } ? "var" : NormalizeExpressionText(statement.Type.ToString()));
        var exprText = RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"foreach({typeName} {statement.Identifier} in {exprText})");
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
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
        ref int linqInvocationCount) {
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"while({condText})");
        return 文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount);
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
        ref int linqInvocationCount) {
        builder.AppendLine("do");
        if(!文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) return false;
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
        ref int linqInvocationCount) {
        var condText = RenderExpressionForGeneratedMethod(statement.Condition,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"if({condText})");
        if(!文生成16(statement.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) return false;
        if(statement.Else is not null) {
            builder.AppendLine("else");
            if(!文生成16(statement.Else.Statement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) return false;
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
        ref int linqInvocationCount) {
        var exprText = RenderExpressionForGeneratedMethod(statement.Expression,semanticModel,containingType,unsafeAccessorCollector);
        builder.AppendLine($"switch({exprText})");
        builder.AppendLine("{");
        foreach(var section in statement.Sections) {
            foreach(var label in section.Labels) {
                builder.AppendLine(label.ToString());
            }
            foreach(var sectionStatement in section.Statements) {
                if(!文生成16(sectionStatement,semanticModel,containingType,ref uniqueNumber,methodIsStatic,ref staticMethods,unsafeAccessorCollector,generatedFragments,builder,ref linqInvocationCount)) return false;
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
        ref int linqInvocationCount) {
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
            var tempName = $"__linq{linqInvocationCount++}";
            if(!呼び出し式フラグメント生成1(semanticModel,invocation,containingType,ref uniqueNumber,out var fragment,out var invocationStaticMethods,unsafeAccessorCollector)) {
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
            if(!フラグメントEnumerator実行文追加37(builder,fragment,tempName,resultTypeName)) {
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
            .Where(invocation => SystemLinqEnumerable呼び出し判定42(invocation,semanticModel))
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
        return (method.ReducedFrom ?? method).ContainingType.ToDisplayString()=="System.Linq.Enumerable";
    }

    private static int フラグメント番号初期値取得18() => 0;

    private static bool 式文生成29(ExpressionStatementSyntax statement,SemanticModel semanticModel,INamedTypeSymbol containingType,out string generatedStatement) {
        generatedStatement=string.Empty;
        return statement.Expression is InvocationExpressionSyntax invocation
            && 非公開同一型呼び出し文生成30(invocation,semanticModel,containingType,out generatedStatement);
    }

    private static bool 非公開同一型呼び出し文生成30(InvocationExpressionSyntax invocation,SemanticModel semanticModel,INamedTypeSymbol containingType,out string generatedStatement) {
        generatedStatement=string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType,containingType)
            || method.DeclaredAccessibility!=Accessibility.Private) {
            return false;
        }

        var originalMethod = method.OriginalDefinition;
        var localFunctionTypeParameters = originalMethod.TypeParameters.Length==0
            ? string.Empty
            : "<"+string.Join(", ",originalMethod.TypeParameters.Select(static parameter => parameter.Name))+">";
        var targetTypeName = method.ContainingType.ToDisplayString(完全修飾形式);
        var accessorFunctionName = $"__UnsafeAccessor_{originalMethod.Name}";
        var localFunctionParameters = new List<string>();
        var accessorFunctionParameters = new List<string>();
        var localFunctionArguments = new List<string>();
        if(method.IsStatic) {
            accessorFunctionParameters.Add($"{targetTypeName} _");
        } else {
            accessorFunctionParameters.Add($"{targetTypeName} @this");
        }
        for(var index = 0; index<invocation.ArgumentList.Arguments.Count; index++) {
            var parameter = originalMethod.Parameters[index];
            var parameterDeclaration = $"{RefKind接頭辞取得35(parameter.RefKind)}{parameter.Type.ToDisplayString(完全修飾形式)} {parameter.Name}";
            localFunctionParameters.Add(parameterDeclaration);
            accessorFunctionParameters.Add(parameterDeclaration);
            localFunctionArguments.Add(RenderArgumentForGeneratedMethod(invocation.ArgumentList.Arguments[index],semanticModel,containingType));
        }

        var accessorKind = method.IsStatic
            ? "global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod"
            : "global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method";
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessorAttribute({accessorKind}, Name = \"{originalMethod.Name}\")]");
        builder.AppendLine($"extern static {method.ReturnType.ToDisplayString(完全修飾形式)} {accessorFunctionName}{localFunctionTypeParameters}({string.Join(", ",accessorFunctionParameters)});");
        var accessorArguments = method.IsStatic
            ? $"default!, {string.Join(", ",localFunctionParameters.Select(static parameter => parameter.Split(' ').Last()))}"
            : $"@this, {string.Join(", ",localFunctionParameters.Select(static parameter => parameter.Split(' ').Last()))}";
        accessorArguments=accessorArguments.TrimEnd(' ', ',');
        var localWrapperModifier = method.IsStatic ? "static " : string.Empty;
        if(method.ReturnsVoid) {
            builder.AppendLine($"{localWrapperModifier}void {originalMethod.Name}{localFunctionTypeParameters}({string.Join(", ",localFunctionParameters)}) => {accessorFunctionName}({accessorArguments});");
        } else {
            builder.AppendLine($"{localWrapperModifier}{method.ReturnType.ToDisplayString(完全修飾形式)} {originalMethod.Name}{localFunctionTypeParameters}({string.Join(", ",localFunctionParameters)}) => {accessorFunctionName}({accessorArguments});");
        }
        builder.AppendLine($"{originalMethod.Name}({string.Join(", ",localFunctionArguments)});");
        builder.AppendLine("}");
        generatedStatement=builder.ToString().TrimEnd();
        return true;
    }

    private static bool 直接呼び出しEnumerator生成33(
        Linq傍受対象型 target,
        ref int uniqueNumber,
        out string staticMethods,
        out string body) {
        staticMethods=string.Empty;
        body=string.Empty;

        if(target.Method.ReturnType is not INamedTypeSymbol returnType
            || !IsIEnumerableType(returnType)
            || !列挙要素型取得11(target.Method.ReturnType,out var returnElementType,out _)
            || target.Method.ReducedFrom is null
            || target.Invocation.Expression is not MemberAccessExpressionSyntax) {
            return false;
        }

        var methodName = target.Method.ReducedFrom.Name;
        if(!フラグメント候補一覧.TryGetValue(methodName,out var candidates)) {
            return false;
        }

        foreach(var candidate in candidates) {
            var snapshot = uniqueNumber;
            if(!候補使用可能27(candidate,target.Method)) {
                continue;
            }

            var constructorArguments = new List<object?>(candidate.Parameters.Length+1) {
                snapshot++,
                CreateRootEnumerableFragment("source",target.SourceElementTypeName,$"global::System.Collections.Generic.IEnumerator<{target.SourceElementTypeName}>",useEnumerableInterface: true,snapshot++)
            };

            var argumentIndex = 0;
            var supported = true;
            for(var parameterIndex = 1; parameterIndex<candidate.Parameters.Length; parameterIndex++) {
                var parameter = candidate.Parameters[parameterIndex];
                if(argumentIndex>=target.Invocation.ArgumentList.Arguments.Count) {
                    if(parameter.ParameterType==typeof(string)) {
                        if(TryResolveSyntheticStringArgument(parameter,target.Method,out var syntheticArgument)) {
                            constructorArguments.Add(syntheticArgument);
                            continue;
                        }
                    }
                    supported=false;
                    break;
                }

                var argumentExpression = target.Invocation.ArgumentList.Arguments[argumentIndex].Expression;
                if(parameter.ParameterType==typeof(string)) {
                    constructorArguments.Add(RenderExpressionForGeneratedMethod(argumentExpression,target.SemanticModel,target.EnclosingType));
                    argumentIndex++;
                    continue;
                }

                if(typeof(FuncFragment).IsAssignableFrom(parameter.ParameterType)) {
                    if(argumentIndex>=target.Parameters.Length
                        || target.Parameters[argumentIndex].TemplateArgument is not FuncFragment funcFragment
                        || !関数フラグメント利用可能28(funcFragment,candidate.FragmentType)) {
                        supported=false;
                        break;
                    }

                    constructorArguments.Add(funcFragment);
                    argumentIndex++;
                    continue;
                }

                supported=false;
                break;
            }

            if(!supported || argumentIndex!=target.Invocation.ArgumentList.Arguments.Count) {
                continue;
            }

            if(candidate.Constructor.Invoke(constructorArguments.ToArray()) is not Enumerable fragment
                || fragment.ElementType!=returnElementType.ToDisplayString(完全修飾形式)) {
                continue;
            }

            uniqueNumber=snapshot;
            staticMethods=静的メソッド連結14(fragment.StructMethod,fragment.GetEnumeratorStruct);
            body=$"return {fragment.GetEnumerableExpression};";
            return true;
        }

        return false;
    }

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

    private static string 生成ソース整形(string source) {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        return root.NormalizeWhitespace(indentation: "    ",eol: "\n").ToFullString();
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

    private static bool IsEffectivelyStaticLambda(LambdaExpressionSyntax lambda) {
        // 実質staticか判定: 明示的なstatic修飾があるか、非staticでも閉じ込める変数がない等の判定ロジックを実装
        // ここでは単純にstatic修飾有無を判定し、本来は閉じ込み変数の有無も検査すべき
        if(lambda.Modifiers.Any(SyntaxKind.StaticKeyword)) {
            return true;
        }

        // 追加実装例: 変数の閉じ込みがないなら実質staticと見なす
        // 今は単純にfalse返す
        return false;
    }

    private static bool TryCreateSourceFragment(SemanticModel semanticModel,ExpressionSyntax sourceExpression,string elementTypeName,ref int loopNumber,out Enumerable fragment) {
        if(sourceExpression is InvocationExpressionSyntax sourceInvocation
            && semanticModel.GetSymbolInfo(sourceInvocation).Symbol is IMethodSymbol sourceMethod
            && sourceMethod.ReducedFrom is not null
            && Linqメソッド判定(sourceMethod)) {
            if(sourceInvocation.Expression is not MemberAccessExpressionSyntax { Expression: var nestedSourceExpression }) {
                fragment=null!;
                return false;
            }

            if(!TryCreateSourceFragment(semanticModel,nestedSourceExpression,GetSourceElementTypeName(sourceMethod),ref loopNumber,out var nestedSourceFragment)) {
                fragment=null!;
                return false;
            }

            if(!TryCreateTemplateArguments(semanticModel,sourceMethod,sourceInvocation.ArgumentList.Arguments,ref loopNumber,out var templateArguments)) {
                fragment=null!;
                return false;
            }
            fragment=null!;
            return false;
        }

        fragment=CreateRootEnumerableFragment("source",elementTypeName,$"global::System.Collections.Generic.IEnumerator<{elementTypeName}>",useEnumerableInterface: true,loopNumber++);
        return true;
    }

    private static bool TryCreateTemplateArguments(SemanticModel semanticModel,IMethodSymbol method,SeparatedSyntaxList<ArgumentSyntax> arguments,ref int loopNumber,out object[] templateArguments) {
        var parameters = method.Parameters;
        if(parameters.Length!=arguments.Count) {
            templateArguments=Array.Empty<object>();
            return false;
        }

        templateArguments = new object[parameters.Length];
        for(var index = 0; index<parameters.Length; index++) {
            var parameter = parameters[index];
            var argumentExpression = arguments[index].Expression;

            if(argumentExpression is LambdaExpressionSyntax lambda) {
                if(!TryCreateLambdaFuncFragment(lambda,parameter.Type,out var lambdaSpec)) {
                    return false;
                }

                var isEffectivelyStatic = lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsEffectivelyStaticLambda(lambda);
                templateArguments[index] = isEffectivelyStatic
                    ? CreateStaticTemplateArgument(lambdaSpec)
                    : CreateParameterTemplateArgument(parameter.Name,lambdaSpec);
                continue;
            }

            templateArguments[index]=NormalizeExpressionText(argumentExpression.ToString());
        }

        return true;
    }

    private static List<string> ローカルメソッド未対応Enumerable取得31(Linq傍受対象型 target) {
        // ローカルメソッド全体を再構成する前に、Fragment クラス自体が存在しない Enumerable メソッド名を列挙する。
        // 「System.Linq.Enumerable を含むメソッドは全部 intercept する」が前提なので、
        // 未対応メソッドは target から落とすのではなく、生成 interceptor から例外で見えるようにする。
        return target.LinqAnalysis.UnsupportedMethods.ToList();
    }

    private static bool Enumerableメソッドフラグメント対応判定32(string methodName)
        => methodName=="Any" || フラグメント候補一覧.ContainsKey(methodName);

    private static bool ローカルメソッドEnumerable呼び出し含む34(Linq傍受対象型 target) {
        // 未対応メソッド名の列挙とは別に、「そもそも LINQ を含むのに再構成できなかった」ケースを検知する。
        return target.LinqAnalysis.HasEnumerableCallInBody;
    }

    // LINQ 呼び出しが全て式文の直下（結果を捨てる）かどうかを返す。
    // UnionImpl のように void メソッドで Union 等の結果を捨てているケースは true。
    // AssertEqualByKey のように Assert.Equal(...OrderBy(...), ...) と入れ子になっているケースは false。
    private static bool すべてのLINQ呼び出しが式文直下(Linq傍受対象型 target) {
        return target.LinqAnalysis.AllLinqCallsDirectExpressionStatement;
    }

    private static object CreateStaticTemplateArgument(LambdaSpec lambda) {
        return new LambdaFunc {
            Type=Func型解決13(lambda.ParameterTypeNames.Length),
            ReturnTypeName=lambda.ReturnTypeName,
            ParameterTypeNames=lambda.ParameterTypeNames,
            ParameterNames=lambda.ParameterNames,
            DefineLambda=$" => {lambda.Body};",
            CanGenerateMethod=true,
            CanInlineAssign=true,
            InlineBody=lambda.Body
        };
    }

    private static object CreateParameterTemplateArgument(string parameterName,LambdaSpec lambda) {
        return new ParameterFunc {
            ParameterName=parameterName,
            Type=Func型解決13(lambda.ParameterTypeNames.Length),
            ReturnTypeName=lambda.ReturnTypeName,
            ParameterTypeNames=lambda.ParameterTypeNames,
            ParameterNames=lambda.ParameterNames,
            CanGenerateMethod=false,
            CanInlineAssign=false,
        };
    }

    private static string GetSourceElementTypeName(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        if(definition.Parameters.Length==0
            ||definition.Parameters[0].Type is not INamedTypeSymbol sourceType
            ||sourceType.TypeArguments.Length!=1) {
            throw new InvalidOperationException($"Could not determine source element type for '{method.ToDisplayString(完全修飾形式)}'.");
        }

        return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
    }

    private static void CollectTypeParameterNames(ITypeSymbol? type,List<string> names,HashSet<string> seen) {
        switch(type) {
            case null:
                return;
            case ITypeParameterSymbol typeParameter:
                if(seen.Add(typeParameter.Name)) {
                    names.Add(typeParameter.Name);
                }
                return;
            case IArrayTypeSymbol arrayType:
                CollectTypeParameterNames(arrayType.ElementType,names,seen);
                return;
            case IPointerTypeSymbol pointerType:
                CollectTypeParameterNames(pointerType.PointedAtType,names,seen);
                return;
            case INamedTypeSymbol namedType:
                foreach(var typeArgument in namedType.TypeArguments) {
                    CollectTypeParameterNames(typeArgument,names,seen);
                }
                return;
        }
    }

    private static Enumerable CreateRootEnumerableFragment(string sourceExpression,string elementType,string enumeratorTypeName,bool useEnumerableInterface,int 番号) {
        return new Root(
            番号,
            sourceExpression,
            elementType,
            enumeratorTypeName,
            useEnumerableInterface);
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

    private static string RenderArgumentForGeneratedMethod(ArgumentSyntax argument,SemanticModel semanticModel,INamedTypeSymbol containingType) {
        var prefix = RefKind接頭辞取得35(argument.RefKindKeyword.Kind());
        return $"{prefix}{RenderExpressionForGeneratedMethod(argument.Expression,semanticModel,containingType)}";
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
        float single => single.ToString("R",global::System.Globalization.CultureInfo.InvariantCulture) + "f",
        double number => number.ToString("R",global::System.Globalization.CultureInfo.InvariantCulture) + "d",
        decimal number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + "m",
        long number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + "L",
        ulong number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + "UL",
        uint number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + "U",
        _ => Convert.ToString(value,global::System.Globalization.CultureInfo.InvariantCulture) ?? "default!",
    };

    private static string RefKind接頭辞取得35(RefKind refKind) => refKind switch {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.In => "in ",
        _ => string.Empty,
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
        var text = NormalizeExpressionText(expression.ToString())
            .Replace("SystemEnumerable.","global::System.Linq.Enumerable.",StringComparison.Ordinal)
            .Replace("this.","@this.",StringComparison.Ordinal);

        if(replacementMap is not null) {
            foreach(var pair in replacementMap.OrderByDescending(static pair => pair.Key.Length)) {
                text=text.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
            }
        }

        var processedInvocations = new HashSet<InvocationExpressionSyntax>(ReferenceEqualityComparer.Instance);
        foreach(var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()) {
            if(processedInvocations.Contains(invocation)) continue;

            // nameof() は生成 namespace ではスコープ外になる識別子を参照しうるため、文字列リテラルへ評価する。
            if(invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" }
                && invocation.ArgumentList.Arguments.Count==1) {
                var nameofArg = invocation.ArgumentList.Arguments[0].Expression;
                var nameofSymbol = semanticModel.GetSymbolInfo(nameofArg).Symbol;
                var nameString = nameofSymbol?.Name
                    ?? (nameofArg is MemberAccessExpressionSyntax maMember ? maMember.Name.Identifier.Text : nameofArg.ToString());
                var nameofText = NormalizeExpressionText(invocation.ToString())
                    .Replace("this.","@this.",StringComparison.Ordinal);
                text=text.Replace(nameofText,$"\"{nameString}\"",StringComparison.Ordinal);
                continue;
            }

            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || (!method.IsStatic && !method.IsExtensionMethod)) {
                continue;
            }

            // text は先頭で this. → @this. 変換済みなので invocationText も同様に変換する。
            var invocationText = NormalizeExpressionText(invocation.ToString())
                .Replace("this.","@this.",StringComparison.Ordinal);
            if(SymbolEqualityComparer.Default.Equals(method.ContainingType,containingType)
                && TryRenderSimpleLocalMethodInvocationExpression(method,semanticModel,containingType,out var inlineInvocationText,unsafeAccessorCollector)) {
                // static 引数なし helper で本文が単純式なら、そのまま式へ畳み込む。
                text=text.Replace(invocationText,inlineInvocationText,StringComparison.Ordinal);
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
                continue;
            }

            if(SymbolEqualityComparer.Default.Equals(method.ContainingType,containingType)
                && method.DeclaredAccessibility!=Accessibility.Public
                && !method.IsGenericMethod) {
                // private helper で単純式でないかつ非ジェネリック → UnsafeAccessor で直接呼び出す。
                text=text.Replace(invocationText,RenderStaticLocalMethodForwardExpression(method,invocation,semanticModel,containingType,unsafeAccessorCollector),StringComparison.Ordinal);
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
                continue;
            }

            if(!method.IsExtensionMethod && invocation.Expression is MemberAccessExpressionSyntax) {
                // 生成 namespace は元 namespace と異なるため、静的メソッド呼び出しは型名を完全修飾する。
                var qualifiedInvocationText = $"{method.ContainingType.ToDisplayString(完全修飾形式)}.{method.Name}{RenderTypeArgumentList(method)}({string.Join(", ",invocation.ArgumentList.Arguments.Select(argument => RenderArgumentForGeneratedMethod(argument,semanticModel,containingType)))})";
                text=text.Replace(invocationText,qualifiedInvocationText,StringComparison.Ordinal);
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
            } else if(method.IsExtensionMethod
                && (method.ReducedFrom ?? method).ContainingType.ToDisplayString() != "System.Linq.Enumerable"
                && invocation.Expression is MemberAccessExpressionSyntax extensionMemberAccess) {
                // 拡張メソッド: receiver.Method<T>(args) → global::ContainingType.Method<T>(receiver, args)
                var receiverText = RenderExpressionForGeneratedMethod(extensionMemberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
                var allArgs = new[]{receiverText}.Concat(invocation.ArgumentList.Arguments.Select(argument => RenderArgumentForGeneratedMethod(argument,semanticModel,containingType)));
                var qualifiedExtensionText = $"{method.ContainingType.ToDisplayString(完全修飾形式)}.{method.Name}{RenderTypeArgumentList(method)}({string.Join(", ",allArgs)})";
                text=text.Replace(invocationText,qualifiedExtensionText,StringComparison.Ordinal);
                foreach(var desc in invocation.DescendantNodes().OfType<InvocationExpressionSyntax>()) processedInvocations.Add(desc);
            }
        }

        foreach(var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
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

        foreach(var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
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

        foreach(var objectCreation in expression.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>()) {
            var typeSymbol = semanticModel.GetTypeInfo(objectCreation.Type).Type;
            if(typeSymbol is null) {
                continue;
            }

            var originalTypeText = NormalizeExpressionText(objectCreation.Type.ToString());
            var qualifiedTypeText = typeSymbol.ToDisplayString(完全修飾形式);
            if(originalTypeText==qualifiedTypeText || originalTypeText.StartsWith("global::",StringComparison.Ordinal)) {
                continue;
            }

            text=System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(originalTypeText)}\b",
                qualifiedTypeText,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        foreach(var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
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
        foreach(var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
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

        foreach(var param in expression.DescendantNodesAndSelf().OfType<ParameterSyntax>()) {
            if(param.Type is null) continue;
            var typeSymbol=semanticModel.GetTypeInfo(param.Type).Type;
            if(typeSymbol is null) continue;
            var qualifiedName=typeSymbol.ToDisplayString(完全修飾形式);
            var originalName=NormalizeExpressionText(param.Type.ToString());
            if(originalName==qualifiedName||originalName.StartsWith("global::",StringComparison.Ordinal)) continue;
            text=System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(originalName)}\b",
                qualifiedName,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        return text;
    }

    private static bool TryRenderSimpleLocalMethodInvocationExpression(IMethodSymbol method,SemanticModel semanticModel,INamedTypeSymbol containingType,out string expressionText,List<string>? unsafeAccessorCollector = null) {
        expressionText=string.Empty;

        if(!method.IsStatic
            // instance helper は this の意味論を壊しやすいのでここでは扱わない。
            || method.Parameters.Length!=0
            // 引数あり helper は多重評価や ref/out を考慮する必要があるので別扱い。
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType,containingType)
            // 他型の helper を展開するとアクセス制御や this 置換が崩れる。
            || method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }
            && IsSimpleInlineableLocalMethodExpression(expressionBody)) {
            expressionText=RenderExpressionForGeneratedMethod(expressionBody,semanticModel,containingType,unsafeAccessorCollector);
            if(expressionBody is CollectionExpressionSyntax or ImplicitArrayCreationExpressionSyntax) {
                expressionText=$"(({method.ReturnType.ToDisplayString(完全修飾形式)}){expressionText})";
            }
            return true;
        }

        if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        } && IsSimpleInlineableLocalMethodExpression(returnExpression)) {
            expressionText=RenderExpressionForGeneratedMethod(returnExpression,semanticModel,containingType,unsafeAccessorCollector);
            if(returnExpression is CollectionExpressionSyntax or ImplicitArrayCreationExpressionSyntax) {
                expressionText=$"(({method.ReturnType.ToDisplayString(完全修飾形式)}){expressionText})";
            }
            return true;
        }

        return false;
    }

    private static string RenderStaticLocalMethodForwardExpression(IMethodSymbol method,InvocationExpressionSyntax invocation,SemanticModel semanticModel,INamedTypeSymbol containingType,List<string>? unsafeAccessorCollector = null) {
        // 非ジェネリック private static helper: UnsafeAccessor 経由で直接呼び出す。
        var typeName = SanitizeIdentifier(method.ContainingType.Name);
        var methodName = SanitizeIdentifier(method.Name);
        var accessorName = $"__StaticMethod_{typeName}_{methodName}";
        var targetTypeName = method.ContainingType.ToDisplayString(完全修飾形式);
        var returnTypeName = method.ReturnType.ToDisplayString(完全修飾形式);
        var paramParts = new List<string> { $"{targetTypeName}? _" };
        foreach(var param in method.Parameters) {
            paramParts.Add($"{param.Type.ToDisplayString(完全修飾形式)} {param.Name}");
        }
        var paramList = string.Join(", ",paramParts);
        var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({paramList});";
        unsafeAccessorCollector?.Add(accessorCode);

        var renderedArgs = invocation.ArgumentList.Arguments.Count==0
            ? string.Empty
            : ", " + string.Join(", ",invocation.ArgumentList.Arguments.Select(argument => RenderArgumentForGeneratedMethod(argument,semanticModel,containingType)));
        return $"{accessorName}(null{renderedArgs})";
    }

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

    // メソッドグループ用の [UnsafeAccessor] extern static 宣言コードを返す。
    private static string RenderDelegateUnsafeAccessorCode(IMethodSymbol method) {
        var accessorName = GetDelegateAccessorMethodName(method);
        var targetTypeName = method.ContainingType.ToDisplayString(完全修飾形式);
        var returnTypeName = method.ReturnType.ToDisplayString(完全修飾形式);
        var parameters = new System.Collections.Generic.List<string> { $"{targetTypeName} @this" };
        foreach(var param in method.Parameters) {
            parameters.Add($"{param.Type.ToDisplayString(完全修飾形式)} {param.Name}");
        }
        var paramList = string.Join(", ",parameters);
        return $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({paramList});";
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

    private static bool TryRenderSimpleLocalMethodBody(Linq傍受対象型 target,out string body) {
        body=string.Empty;

        if(!target.Method.IsStatic
            // ここで扱うのは Source() のような private static 引数なし helper だけ。
            || target.Method.DeclaredAccessibility!=Accessibility.Private
            // public メソッドは元の可視 API なのでここでは丸ごと inline しない。
            || !SymbolEqualityComparer.Default.Equals(target.Method.ContainingType,target.EnclosingType)
            // 別型メソッドは同一ファイルでも可視性や receiver が変わるため除外。
            || target.Parameters.Length!=0) {
            // 引数あり helper は多重評価や side effect 管理が必要なので未対応。
            return false;
        }

        if(target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        if(methodSyntax.ParameterList.Parameters.Count!=target.Parameters.Length) {
            return false;
        }

        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }
            && IsSimpleInlineableLocalMethodExpression(expressionBody)) {
            // `static int[] Source() => [1,2,3];` のような形はそのまま return 式へ落とせる。
            var renderedExpression = RenderExpressionForGeneratedMethod(expressionBody,target.SemanticModel,target.EnclosingType);
            body=target.Method.ReturnsVoid
                ? $"{renderedExpression};"
                : $"return {renderedExpression};";
            return true;
        }

        if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        } && IsSimpleInlineableLocalMethodExpression(returnExpression)) {
            // `return [1,2,3];` の 1 文メソッドも同様に安全。
            body=$"return {RenderExpressionForGeneratedMethod(returnExpression,target.SemanticModel,target.EnclosingType)};";
            return true;
        }

        if(target.Method.ReturnsVoid
            && methodSyntax.Body is {
                Statements: [
                    ExpressionStatementSyntax expressionStatement
                ]
            }
            && IsSimpleInlineableLocalMethodExpression(expressionStatement.Expression)) {
            // void helper でも単一式文なら inline 可能。
            body=$"{RenderExpressionForGeneratedMethod(expressionStatement.Expression,target.SemanticModel,target.EnclosingType)};";
            return true;
        }

        return false;
    }

    private static bool IsSimpleInlineableLocalMethodExpression(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax { Expression: var innerExpression }) {
            expression=innerExpression;
        }

        // ここでは「評価回数を増やしても意味が変わりにくい単純式」だけ許可する。
        // 配列/collection/literal/default に絞ることで、Source() のような定数的 helper を安全に潰せる。
        return expression switch {
            CollectionExpressionSyntax => true,
            ArrayCreationExpressionSyntax => true,
            ImplicitArrayCreationExpressionSyntax => true,
            LiteralExpressionSyntax => true,
            DefaultExpressionSyntax => true,
            _ => false,
        };
    }

    private static string RenderLocalMethodForwardBody(Linq傍受対象型 target,out string unsafeAccessorDeclaration) {
        unsafeAccessorDeclaration=string.Empty;
        // ジェネリックメソッドは UnsafeAccessor が MakeGenericMethod に対応しないため非対応。
        if(target.Method.TypeArguments.Length > 0) {
            var methodDisplay = target.Method.ToDisplayString(完全修飾形式)
                .Replace("\\","\\\\",StringComparison.Ordinal)
                .Replace("\"","\\\"",StringComparison.Ordinal);
            return $"throw global::Seamless.GeneratedCodeHelpers.LinqInterceptorFailure(\"Local method interception fallback is not supported for generic method '{methodDisplay}'.\");";
        }

        // 非ジェネリックメソッド: UnsafeAccessor 経由で直接呼び出す。
        var typeName = SanitizeIdentifier(target.Method.ContainingType.Name);
        var methodName = SanitizeIdentifier(target.Method.Name);
        var returnTypeName = target.Method.ReturnsVoid ? "void" : target.ReturnTypeName;
        var targetTypeName = target.Method.ContainingType.ToDisplayString(完全修飾形式);
        string accessorName;
        string callExpr;
        if(target.Method.IsStatic) {
            accessorName = $"__StaticMethod_{typeName}_{methodName}";
            var paramParts = new List<string> { $"{targetTypeName}? _" };
            foreach(var param in target.Method.Parameters) {
                paramParts.Add($"{param.Type.ToDisplayString(完全修飾形式)} {param.Name}");
            }
            unsafeAccessorDeclaration = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{target.Method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({string.Join(", ",paramParts)});";
            var callArgs = "null" + (target.Parameters.Length > 0 ? ", " + string.Join(", ",target.Parameters.Select(static p => p.Name)) : string.Empty);
            callExpr = target.Method.ReturnsVoid
                ? $"{accessorName}({callArgs});"
                : $"return {accessorName}({callArgs});";
        } else {
            accessorName = $"__Method_{typeName}_{methodName}";
            var paramParts = new List<string> { $"{targetTypeName} @this" };
            foreach(var param in target.Method.Parameters) {
                paramParts.Add($"{param.Type.ToDisplayString(完全修飾形式)} {param.Name}");
            }
            unsafeAccessorDeclaration = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"{target.Method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({string.Join(", ",paramParts)});";
            var callArgs = "@this" + (target.Parameters.Length > 0 ? ", " + string.Join(", ",target.Parameters.Select(static p => p.Name)) : string.Empty);
            callExpr = target.Method.ReturnsVoid
                ? $"{accessorName}({callArgs});"
                : $"return {accessorName}({callArgs});";
        }
        return callExpr;
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
        return $"throw global::Seamless.GeneratedCodeHelpers.LinqInterceptorFailure(\"Local method interception could not inline '{methodName}'. Fallback forwarding is disabled for methods that contain System.Linq.Enumerable calls.\");";
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
        public static readonly ローカルメソッドLinq解析結果 Empty = new(false,false,ImmutableArray<string>.Empty,true);

        public ローカルメソッドLinq解析結果(bool hasEnumerableCall,bool hasEnumerableCallInBody,ImmutableArray<string> unsupportedMethods,bool allLinqCallsDirectExpressionStatement) {
            this.HasEnumerableCall=hasEnumerableCall;
            this.HasEnumerableCallInBody=hasEnumerableCallInBody;
            this.UnsupportedMethods=unsupportedMethods;
            this.AllLinqCallsDirectExpressionStatement=allLinqCallsDirectExpressionStatement;
        }

        public bool HasEnumerableCall { get; }
        /// <summary>Block-bodied methods のみ: expression-bodied は常に false。</summary>
        public bool HasEnumerableCallInBody { get; }
        public ImmutableArray<string> UnsupportedMethods { get; }
        public bool AllLinqCallsDirectExpressionStatement { get; }
    }

    private sealed class Linq傍受対象型 {
        public Linq傍受対象型(string identifier,string containingMethodName,string interceptsLocationAttributeText,IMethodSymbol method,IMethodSymbol templateMethod,InvocationExpressionSyntax invocation,INamedTypeSymbol enclosingType,SemanticModel semanticModel,InterceptParameterSpec[] parameters,ローカルメソッドLinq解析結果? linqAnalysis = null,string? sourceElementTypeName = null,string[]? methodTypeParameterNames = null,bool isLocalMethodInterception = false) {
            this.Identifier=identifier;
            this.ContainingMethodName=containingMethodName;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.Method=method;
            this.TemplateMethod=templateMethod;
            this.Invocation=invocation;
            this.EnclosingType=enclosingType;
            this.SemanticModel=semanticModel;
            this.Parameters=parameters;
            this.LinqAnalysis=linqAnalysis ?? ローカルメソッドLinq解析結果.Empty;
            this.ExplicitSourceElementTypeName=sourceElementTypeName;
            this.MethodTypeParameters=methodTypeParameterNames ?? Array.Empty<string>();
            this.IsLocalMethodInterception=isLocalMethodInterception;
        }

        public string Identifier { get; }
        public string ContainingMethodName { get; }
        public string InterceptsLocationAttributeText { get; }
        public IMethodSymbol Method { get; }
        public IMethodSymbol TemplateMethod { get; }
        public InvocationExpressionSyntax Invocation { get; }
        public string SourcePath => this.Invocation.SyntaxTree.FilePath;
        public INamedTypeSymbol EnclosingType { get; }
        public SemanticModel SemanticModel { get; }
        public InterceptParameterSpec[] Parameters { get; }
        public ローカルメソッドLinq解析結果 LinqAnalysis { get; }
        public string? ExplicitSourceElementTypeName { get; }
        public string[] MethodTypeParameters { get; }
        public bool IsLocalMethodInterception { get; }
        public string SourceElementTypeName {
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
        public string ReturnTypeName => this.Method.ReturnsVoid ? "void" : this.Method.ReturnType.ToDisplayString(完全修飾形式);
        public string InterceptorParameterList {
            get {
                if(this.IsLocalMethodInterception) {
                    var parameters = new List<string>();
                    if(!this.Method.IsStatic) {
                        parameters.Add($"this {this.Method.ContainingType.ToDisplayString(完全修飾形式)} @this");
                    }
                    parameters.AddRange(this.Parameters.Select(static parameter => parameter.Declaration));
                    return string.Join(", ",parameters);
                }

                return $"this global::System.Collections.Generic.IEnumerable<{this.SourceElementTypeName}> source{this.MethodParameterList}";
            }
        }
        public string MethodParameterList => this.Parameters.Length==0
            ? string.Empty
            : ", "+string.Join(", ",this.Parameters.Select(static parameter => parameter.Declaration));
        public string MethodTypeParameterList => this.MethodTypeParameters.Length==0
            ? string.Empty
            : "<"+string.Join(", ",this.MethodTypeParameters)+">";
        public string MethodName => $"OptimizedLinq_{this.Identifier}";
    }
    private sealed class InterceptParameterSpec {
        public InterceptParameterSpec(string name,string typeName,RefKind refKind = RefKind.None,LambdaSpec? lambda = null,bool lambdaIsStatic = false, LambdaExpressionSyntax? lambdaSyntax = null) {
            this.Name=name;
            this.TypeName=typeName;
            this.RefKind=refKind;
            this.Lambda=lambda;
            this.LambdaIsStatic=lambdaIsStatic;
            this.LambdaSyntax=lambdaSyntax;
        }

        public string Name { get; }
        public string TypeName { get; }
        public RefKind RefKind { get; }
        public LambdaSpec? Lambda { get; }

        // 追加: staticかどうか判定を保存
        public bool LambdaIsStatic { get; }

        // 追加: ラムダ構文の保存
        public LambdaExpressionSyntax? LambdaSyntax { get; }

        public string Declaration => $"{this.RefKindModifier}{this.TypeName} {this.Name}";

        private string RefKindModifier => this.RefKind switch {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty,
        };

        public object TemplateArgument => this.Lambda is null
            ? this.Name
            : this.LambdaIsStatic
                ? new LambdaFunc {
                    Type=Func型解決13(this.Lambda.ParameterTypeNames.Length),
                    ReturnTypeName=this.Lambda.ReturnTypeName,
                    ParameterTypeNames=this.Lambda.ParameterTypeNames,
                    ParameterNames=this.Lambda.ParameterNames,
                    DefineLambda=$" => {this.Lambda.Body};",
                    CanGenerateMethod=true,
                    CanInlineAssign=true,
                    InlineBody=this.Lambda.Body
                }
                : new ParameterFunc {
                    ParameterName=this.Name,
                    ReturnTypeName=this.Lambda.ReturnTypeName,
                    ParameterTypeNames=this.Lambda.ParameterTypeNames,
                    ParameterNames=this.Lambda.ParameterNames,
                };
    }
    private sealed class LambdaSpec {
        public LambdaSpec(string returnTypeName,string[] parameterTypeNames,string[] parameterNames,string body) {
            this.ReturnTypeName=returnTypeName;
            this.ParameterTypeNames=parameterTypeNames;
            this.ParameterNames=parameterNames;
            this.Body=body;
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
            this.Symbol=symbol;
            this.IsPartial=isPartial;
        }
        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }
}
