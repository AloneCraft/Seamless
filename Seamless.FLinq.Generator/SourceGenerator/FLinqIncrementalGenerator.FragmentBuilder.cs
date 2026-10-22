using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Seamless.FLinq;

using Enumerable = Seamless.FLinq.Generator.Enumerable;

namespace Seamless.FLinq.Generator;

public sealed partial class FLinqIncrementalGenerator {
    internal sealed partial class GenerationPipeline {

    // 呼び出し式から対応する Fragment を選び、再生成に必要な静的補助コードも回収する。
    /// <summary>
    /// source.Method(...)
    /// ↓
    /// Method に応じた Fragment 連結へ振り分ける
    /// 例:
    /// source.Any(x=>x > 0) → Where(x=>x > 0) → Any()
    /// source.Join(inner, outerKey, innerKey, result) → Join(...)
    /// </summary>
    private bool 呼び出し式からフラグメント生成(
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

        var definition = method.ReducedFrom??method;
        var methodName = definition.Name;

        if(TryBuildKnownEnumerableRootFromSystemInvocation(
            semanticModel,
            invocation,
            definition,
            containingType,
            ref uniqueNumber,
            out var rootFragment,
            unsafeAccessorCollector,
            replacementMap: null)) {
            fragment=rootFragment;
            return true;
        }

        if(TryBuildStaticSystemEnumerableCall(
            semanticModel,
            invocation,
            method,
            containingType,
            ref uniqueNumber,
            out fragment,
            out staticMethods,
            unsafeAccessorCollector)) {
            return true;
        }

        if(invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            if(methodName=="Any"&&method.Parameters.Length==0) {
                return false;
            }
        }

        if((this.state.Configuration.Optimizations&LinqOptimizations.PipelineFusion)!=0&&PipelineBuilder.TryBuild(
            semanticModel,
            invocation,
            ref uniqueNumber,
            out fragment,
            out staticMethods,
            new PipelineBuildServices(
                完全修飾形式,
                (ExpressionSyntax expression,ref int number,out Enumerable enumerable,out string methods) =>
                    列挙式フラグメント生成(semanticModel,expression,containingType,ref number,out enumerable,out methods,unsafeAccessorCollector),
                (ExpressionSyntax expression,ITypeSymbol delegateType,ref int number,out FuncFragment func) =>
                    関数式フラグメント生成(expression,delegateType,ref number,out func,semanticModel,containingType),
                関数フラグメントが利用可能か,
                expression => RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector),
                (ITypeSymbol type,out ITypeSymbol elementType) => {
                    if(列挙要素型取得(type,out elementType,out _)) {
                        return true;
                    }
                    elementType=null!;
                    return false;
                },
                ContainsAnonymousType,
                静的メソッドコード連結,
                this.state.ResolveOuterTypeParameters(containingType),
                this.state.Configuration,
                this.state.Trace),
            unsafeAccessorCollector)) {
            return true;
        }

        // SelectMany selector の内部チェーンを struct にインライン化できる場合は専用パスへ。
        // レジストリ有効のまま試行することで内側チェーンの匿名型を record struct 化し、
        // 結果要素が匿名型なら SelectManyインライン化を試行 内の出口 Select で元の匿名型へ復元する。
        if(methodName=="SelectMany"
            &&SelectManyインライン化を試行(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector)) {
            return true;
        }

        // 匿名型要素チェーン（TResult ファクトリ形）の ToList は要素型名を生成コードに書けないため、
        // Carrier の汎用 ToList へ型推論で委譲する直接実行文として生成する。
        // ソース構築はレジストリ有効のまま行い、内部の匿名型を record struct 置換できるようにする。
        if(methodName=="ToList"
            &&invocation.ArgumentList.Arguments.Count==0
            &&invocation.Expression is MemberAccessExpressionSyntax { Expression: var anonListSourceExpr }
            &&ContainsAnonymousType(method.ReturnType)) {
            var anonListSnapshot = uniqueNumber;
            if(列挙式フラグメント生成(semanticModel,anonListSourceExpr,containingType,ref uniqueNumber,out var anonListSource,out var anonListStatics,unsafeAccessorCollector)
                &&anonListSource.ElementType=="TResult") {
                fragment=new AnonymousExitToList(uniqueNumber++,anonListSource);
                staticMethods=anonListStatics;
                return true;
            }
            uniqueNumber=anonListSnapshot;
        }

        // ここから先は単発オペレーション等で「結果の匿名型がそのまま外へ逃げる」ケースが多い。
        // それらを record struct へ置換すると呼び出し元が期待する匿名型と型が合わなくなるため、
        // フォールバック経路では匿名型置換を抑止し、従来どおり匿名型を維持する。
            if(methodName=="Any") {
                return Any述語フラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
            }

            return 登録済みフラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
    }

    /// <summary>
    /// Connects static System.Linq.Enumerable.Contains(source, value[, comparer]) and
    /// Reverse(source) to their existing fragments. Unknown IEnumerable<T> keeps the
    /// fragment fallback; source roots with direct span capability retain it.
    /// </summary>
    private bool TryBuildStaticSystemEnumerableCall(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector) {
        fragment=null!;
        staticMethods=string.Empty;
        if(method.ReducedFrom is not null
            ||method.ContainingType.Name!="Enumerable"
            ||method.ContainingNamespace.ToDisplayString()!="System.Linq"
            ||(method.Name=="Contains"&&invocation.ArgumentList.Arguments.Count is not (2 or 3))
            ||(method.Name=="Reverse"&&invocation.ArgumentList.Arguments.Count!=1)
            ||method.Name is not ("Contains" or "Reverse")) {
            return false;
        }

        var snapshot=uniqueNumber;
        if(!列挙式フラグメント生成(
                semanticModel,
                invocation.ArgumentList.Arguments[0].Expression,
                containingType,
                ref uniqueNumber,
                out var source,
                out staticMethods,
                unsafeAccessorCollector)) {
            uniqueNumber=snapshot;
            staticMethods=string.Empty;
            return false;
        }

        if(method.Name=="Reverse") {
            fragment=new Reverse(uniqueNumber++,source);
            return true;
        }

        var value=RenderExpressionForGeneratedMethod(
            invocation.ArgumentList.Arguments[1].Expression,
            semanticModel,
            containingType,
            unsafeAccessorCollector);
        if(invocation.ArgumentList.Arguments.Count==2) {
            fragment=new Contains(uniqueNumber++,source,value,"bool");
            return true;
        }

        var comparer=RenderExpressionForGeneratedMethod(
            invocation.ArgumentList.Arguments[2].Expression,
            semanticModel,
            containingType,
            unsafeAccessorCollector);
        fragment=new Contains_comparer(uniqueNumber++,source,value,comparer,"bool");
        return true;
    }

    /// <summary>
    /// source.Any(x=>x > 0)
    /// ↓
    /// Where(x=>x > 0) → Any()
    /// </summary>
    private bool Any述語フラグメント生成(SemanticModel semanticModel,InvocationExpressionSyntax invocation,INamedTypeSymbol containingType,ref int uniqueNumber,out Fragment fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null)
       => Any.TryBuildWithPredicate(
            semanticModel,invocation,containingType,ref uniqueNumber,
            out fragment,out staticMethods,unsafeAccessorCollector,
            列挙式フラグメント生成,
            関数式フラグメント生成,
            (name,variant,n,extraArgs) => {
                // ここで構築した Fragment の StructMethod は呼び出し元（Any.TryBuildWithPredicate）が
                // 直後に文字列化するため、他の候補生成経路と同様にここで外側型パラメータを確定させる。
                // 後段の PropagateContainingTypeName 呼び出しでは StructMethod 文字列化に間に合わない。
                Fragment? built = null;
                if(extraArgs.Length==1&&extraArgs[0] is Enumerable sourceFragment
                    &&TryCreateBackendNativeIterateFragment(new OperatorKey(name,variant),n,sourceFragment,null,new object?[] { n }.Concat(extraArgs).ToArray(),out var nativeFragment)) {
                    built=nativeFragment;
                } else if(extraArgs.Length==2&&extraArgs[0] is Enumerable sourceFragmentWithPredicate&&extraArgs[1] is FuncFragment predicateFragment
                    &&TryCreateBackendNativeIterateFragment(new OperatorKey(name,variant),n,sourceFragmentWithPredicate,predicateFragment,new object?[] { n }.Concat(extraArgs).ToArray(),out var nativePredicateFragment)) {
                    built=nativePredicateFragment;
                } else if(OperatorRegistry.TryCreateDirect(name,variant,new DirectFragmentArguments(n,extraArgs),out var directFragment)) {
                    built=directFragment;
                }
                built?.ApplyRenderContext(FragmentRenderContext.ForOuterTypeParameters(this.state.ResolveOuterTypeParameters(containingType)));
                return built;
            });

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
    private bool 登録済みフラグメント生成(
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

        if(!呼び出しソース式取得(semanticModel,invocation,method,out var sourceExpression,out var argumentStartIndex,out var fragmentMethod)) {
            return false;
        }

        var methodName = (fragmentMethod.ReducedFrom??fragmentMethod).Name;
        if(!OperatorRegistry.TryGetCandidates(methodName,out var candidates)) {
            return false;
        }

        foreach(var candidate in candidates) {
            var snapshot = uniqueNumber;
            if(登録済みフラグメント生成を試行(candidate,semanticModel,invocation,sourceExpression,argumentStartIndex,fragmentMethod,containingType,ref snapshot,out fragment,out staticMethods,unsafeAccessorCollector)) {
                uniqueNumber=snapshot;
                return true;
            }
        }
        return false;
    }

    private bool 呼び出しソース式取得(
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
        // 列挙式フラグメント生成 が単独列挙ソースフラグメント生成 にフォールバックして不透明な Root として扱う。
        if(method.ReducedFrom is not null
            &&invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            sourceExpression=括弧除去(receiverExpression);
            return true;
        }

        return false;
    }

    private bool IsEnumerableSourceParameter(ITypeSymbol type) =>
        type is INamedTypeSymbol namedType
        &&(IsIEnumerableType(namedType)||IsNonGenericIEnumerableType(namedType));

    /// <summary>
    /// source.SelectMany(p =&gt; innerChain) の内部チェーンを struct にインライン化する。
    /// <br/>selector が単一引数ラムダで本体が LINQ チェーンにパース可能な場合のみ成功する。
    /// 外側パラメータ名（例: p）を内側 Fragment の GetEnumeratorExpression 内で
    /// outerValue フィールド名に置換し、SelectMany_WithInlinedInner を生成する。
    /// </summary>
    private bool SelectManyインライン化を試行(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector = null) {
            if(!SelectManyBase.TryBuildInlined(
                semanticModel,invocation,containingType,ref uniqueNumber,
                out fragment,out staticMethods,unsafeAccessorCollector,
                列挙式フラグメント生成,
                TryGetLambdaExpressionBody,
                GetLambdaParameters)) {
                return false;
            }
        // 結果要素が匿名型でなければ struct インライン化結果をそのまま使う。
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            ||method.TypeArguments.Length==0) {
            return true;
        }
        var resultElementType = method.TypeArguments[method.TypeArguments.Length-1];
        if(!ContainsAnonymousType(resultElementType)) {
            return true;
        }
        // 内側チェーンは record struct 置換で構築済みのため、要素を元の匿名型へ復元する
        // 出口 Select を被せる（トップレベル匿名型 Select と同じ TResult 推論形）。
        var registry = this.state.AnonymousRegistry;
        if(registry is null||fragment is not Enumerable selectManyFragment) {
            // レジストリ無効時は record struct 置換が行われず TryBuildInlined がここまで
            // 来ないはずだが、保険としてフォールバック経路へ委ねる。
            return false;
        }
        var recordTypeName = registry.ResolveClosedName(resultElementType);
        var exitParamName = $"__anonExit{uniqueNumber}";
        var exitBody = registry.ReconstructAnonymousCreation(resultElementType,exitParamName);
        var exitFragment = new AnonymousDelegateFuncFragment(
            $"global::System.Func<{recordTypeName},TResult>",
            $"_anonExitDel{uniqueNumber++}",
            $"({recordTypeName} {exitParamName})=>{exitBody}") {
            ReturnTypeName="TResult",
            ParameterTypeNames=new[] { recordTypeName },
            ParameterNames=new[] { exitParamName },
            CanGenerateMethod=false,
        };
        var exitSelect = new Select_selector(uniqueNumber++,selectManyFragment,exitFragment);
        fragment=exitSelect;
        staticMethods=静的メソッドコード連結(staticMethods,exitSelect.StructMethod);
        return true;
    }

    /// <summary>
    /// source.Method(arg1, arg2, ...)
    /// ↓
    /// candidate(番号, sourceFragment, ...)
    /// のコンストラクター形へ引数を並べ替えて、1 本の Fragment チェーンへ正規化する。
    /// 例:
    /// source.Aggregate(seed, func) → Aggregate_seed_func(sourceFragment, seed, funcFragment)
    /// source.Select(selector) → Select(sourceFragment, selectorFragment)
    /// source generator の生成コードを検証する。
    ///   → Join(outerFragment, innerFragment, outerKeyFragment, innerKeyFragment, resultFragment, comparer)
    /// </summary>
    private bool 登録済みフラグメント生成を試行(
        IOperatorDescriptor candidate,
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
        if(!candidate.IsApplicable(method)) {
            return false;
        }

        var context = new FragmentFactoryContext(
            semanticModel,
            invocation,
            sourceExpression,
            argumentStartIndex,
            method,
            containingType,
            uniqueNumber,
            unsafeAccessorCollector,
            null,
            this);
        if(!candidate.TryCreate(context,out fragment)) {
            return false;
        }

        context.ApplyRenderContext(fragment);
        staticMethods=静的メソッドコード連結(context.StaticMethods,fragment.StructMethod);
        uniqueNumber=context.UniqueNumber;
        return true;
    }
    internal static bool TryCreateBackendNativeIterateFragment(
        OperatorKey key,
        int number,
        Enumerable source,
        FuncFragment? predicate,
        IReadOnlyList<object?> constructorArguments,
        out Fragment fragment) {
        fragment=null!;
        if(!OperatorRegistry.TryGetProviderNativeProfile(key,out var profile)
            ||(profile.PredicateBinding!=ProviderNativePredicateBinding.None&&predicate is null)) {
            return false;
        }

        var root = source;
        var stages = new List<PipelineStage>();
        var currentElementType = source.ElementType;
        if(source is PipelineEnumerable existingPipeline) {
            root=existingPipeline.Source;
            stages.AddRange(existingPipeline.Stages);
            currentElementType=existingPipeline.ElementType;
        }

        var terminalPredicate = profile.PredicateBinding==ProviderNativePredicateBinding.Terminal ? predicate : null;
        if(predicate is not null) {
            switch(profile.PredicateBinding) {
                case ProviderNativePredicateBinding.Select:
                    stages.Add(new PipelineStage(PipelineStageKind.Select,predicate,null,currentElementType,predicate.ReturnTypeName));
                    currentElementType=predicate.ReturnTypeName;
                    break;
                case ProviderNativePredicateBinding.Filter:
                    stages.Add(new PipelineStage(PipelineStageKind.Where,predicate,null,currentElementType,currentElementType));
                    break;
            }
        }

        var containsValue = profile.ContainsValueIndex>=0&&constructorArguments.Count>profile.ContainsValueIndex
            ? constructorArguments[profile.ContainsValueIndex] as string
            : null;
        var containsComparer = profile.ContainsComparerIndex>=0&&constructorArguments.Count>profile.ContainsComparerIndex
            ? constructorArguments[profile.ContainsComparerIndex] as string
            : null;
        var pipeline = new PipelineEnumerable(number,root,stages,currentElementType);
        if(profile.PipelineTerminalKind is { } pipelineKind) {
            var funcs = profile.UsesTerminalFunctions ? constructorArguments.OfType<FuncFragment>().ToArray() : Array.Empty<FuncFragment>();
            var terminalFunc1 = funcs.Length>0 ? funcs[0] : null;
            var terminalFunc2 = funcs.Length>1 ? funcs[1] : null;
            var terminalComparer = profile.UsesTrailingComparer&&constructorArguments.Count>=2
                ? constructorArguments[constructorArguments.Count-2] as string
                : null;
            if(ProviderIterateTerminal.TryCreate(number,pipeline,pipelineKind,terminalFunc1,terminalFunc2,terminalComparer,out var materializeTerminal)) {
                fragment=materializeTerminal;
                return true;
            }
            return false;
        }
        if(profile.UsesNativeLookup
            &&profile.TerminalKind==ProviderIterateTerminalKind.Contains
            &&stages.All(stage => stage.Kind==PipelineStageKind.AsEnumerable)
            &&containsComparer is null
            &&containsValue is not null
            &&root is ProviderNativeRootBase nativeRoot
            &&nativeRoot.NativeContainsExpression(containsValue) is { } lookupExpression) {
            fragment=new ProviderNativeLookupTerminal(number,lookupExpression,"bool");
            return true;
        }
        if(ProviderIterateTerminal.TryCreate(number,pipeline,profile.TerminalKind,terminalPredicate,containsValue,containsComparer,out var providerTerminal)) {
            fragment=providerTerminal;
            return true;
        }

        return false;
    }
    internal bool 関数フラグメントが利用可能か(FuncFragment funcFragment) {
        if(funcFragment.CanGenerateMethod) {
            return true;
        }

        // キャプチャ付きラムダ/delegate 式は struct closure として primary constructor に閉じ込め、
        // source generator の生成コードを検証する。
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
    internal bool 列挙式フラグメント生成(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null) {
        expression=括弧除去(expression);
        var outerTypeParameters=this.state.ResolveOuterTypeParameters(containingType);
        if(expression is InvocationExpressionSyntax invocation
            &&呼び出し式からフラグメント生成(semanticModel,invocation,containingType,ref uniqueNumber,out var nestedFragment,out staticMethods,unsafeAccessorCollector)
            &&nestedFragment is Enumerable enumerableFragment) {
            enumerableFragment.ApplyRenderContext(FragmentRenderContext.ForOuterTypeParameters(outerTypeParameters));
            fragment=enumerableFragment;
            return true;
        }

        var built=単独列挙ソースフラグメント生成(semanticModel,expression,containingType,ref uniqueNumber,out fragment,out staticMethods,unsafeAccessorCollector);
        if(built) {
            fragment.ApplyRenderContext(FragmentRenderContext.ForOuterTypeParameters(outerTypeParameters));
        }
        return built;
    }

    /// <summary>
    /// 式を包む <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.ParenthesizedExpressionSyntax"/> を再帰的に剥がし、内側の式を返す。
    /// Fragment 生成の前処理として呼ぶことで、<c>(source.Where(…))</c> のような余分な括弧を無視できる。
    /// </summary>
    private ExpressionSyntax 括弧除去(ExpressionSyntax expression) {
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
    private bool 単独列挙ソースフラグメント生成(SemanticModel semanticModel,ExpressionSyntax expression,INamedTypeSymbol containingType,ref int uniqueNumber,out Enumerable fragment,out string staticMethods,List<string>? unsafeAccessorCollector = null) {
        staticMethods=string.Empty;
        if(TryBuildKnownEnumerableRoot(semanticModel,expression,containingType,ref uniqueNumber,out fragment,unsafeAccessorCollector)) {
            return true;
        }
        var typeInfo = semanticModel.GetTypeInfo(expression);
        var expressionType = typeInfo.Type??typeInfo.ConvertedType;
        if(!列挙要素型取得(expressionType,out var elementType,out var useEnumerableInterface)) {
            if(IsNonGenericIEnumerableType(expressionType)) {
                fragment=new NonGenericRoot(
                    uniqueNumber++,
                    RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector)) {
                    SourceContainsAnchor=AnchorAnalysis.ContainsAnchor(expression,semanticModel),
                };
                return true;
            }

            fragment=null!;
            return false;
        }

        // 具象型エニュメレータ名を取得する。具象 GetEnumerator() が値型を返す場合はその型名を使い、
        // インターフェース経由の場合は IEnumerator<T> 扱いに切り替える。
        var elementTypeName = elementType.ToDisplayString(完全修飾形式);
        var (concreteEnumeratorTypeName,isDuckTyping)=具象エニュメレータ型名取得(expressionType,elementType,useEnumerableInterface);
        var enumeratorTypeName = concreteEnumeratorTypeName
            ??$"global::System.Collections.Generic.IEnumerator<{elementTypeName}>";
        // 具象型が取れなかった場合はインターフェース IEnumerator<T> になるため struct 制約なし扱いにする。
        var effectiveUseEnumerableInterface = useEnumerableInterface||concreteEnumeratorTypeName is null;

        var _srcExpr = RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);
        var _num = uniqueNumber++;
        fragment=CreateSourceRoot(
            expressionType,
            _num,
            _srcExpr,
            elementTypeName,
            enumeratorTypeName,
            concreteEnumeratorTypeName,
            isDuckTyping,
            effectiveUseEnumerableInterface);
        fragment.SourceContainsAnchor=AnchorAnalysis.ContainsAnchor(expression,semanticModel);
        return true;
    }

    private bool TryHoistInvariantSubexpressions(
        ExpressionSyntax body,
        IReadOnlyCollection<string> parameterNames,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out IReadOnlyDictionary<string,string>? renderReplacementMap,
        out HoistedLocal[] hoistedLocals,
        out string[] unsafeAccessorCodes,
        out IReadOnlyCollection<ExpressionSyntax> hoistedExpressions) {
        renderReplacementMap=null;
        hoistedLocals=Array.Empty<HoistedLocal>();
        unsafeAccessorCodes=Array.Empty<string>();
        hoistedExpressions=Array.Empty<ExpressionSyntax>();

        var parameterSet = new HashSet<string>(parameterNames,StringComparer.Ordinal);
        var candidates = body.DescendantNodesAndSelf(descendIntoChildren: node => !IsNestedFunctionBoundary(node))
            .OfType<ExpressionSyntax>()
            .Where(expr => !ReferenceEquals(expr,body))
            .Where(expr => IsMeaningfulHoistCandidate(expr,semanticModel))
            .Where(expr => !ContainsDisallowedHoistNode(expr))
            .Where(expr => !AnchorAnalysis.ContainsAnchor(expr,semanticModel))
            .Where(expr => !IsInsideAnchorAccess(expr,semanticModel))
            .Where(expr => !ReferencesAnyLambdaParameter(expr,parameterSet,semanticModel))
            .Select(expr => (Expression: expr,Type: semanticModel.GetTypeInfo(expr).ConvertedType??semanticModel.GetTypeInfo(expr).Type))
            .Where(pair => IsUsableHoistedType(pair.Type))
            .ToArray();
        if(candidates.Length==0) {
            return false;
        }

        var selected = new List<(ExpressionSyntax Expression,ITypeSymbol Type)>();
        foreach(var candidate in candidates
            .OrderByDescending(pair => pair.Expression.Span.Length)
            .ThenBy(pair => pair.Expression.SpanStart)) {
            if(selected.Any(existing => SpansOverlap(existing.Expression.Span,candidate.Expression.Span))) {
                continue;
            }
            selected.Add((candidate.Expression,candidate.Type!));
        }
        if(selected.Count==0) {
            return false;
        }

        selected=selected.OrderBy(pair => pair.Expression.SpanStart).ToList();
        var replacements = new Dictionary<string,string>(StringComparer.Ordinal);
        var locals = new List<HoistedLocal>(selected.Count);
        var accessorCodes = new List<string>();
        foreach(var (expression,type) in selected) {
            var expressionKey = expression.ToString();
            if(replacements.ContainsKey(expressionKey)) {
                continue;
            }
            var name = $"__hoist{uniqueNumber++}";
            var expressionAccessorCodes = new List<string>();
            var expressionText = RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,expressionAccessorCodes);
            replacements.Add(expressionKey,name);
            locals.Add(new HoistedLocal(
                name,
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                expressionText));
            accessorCodes.AddRange(expressionAccessorCodes);
        }

        if(locals.Count==0) {
            return false;
        }
        renderReplacementMap=replacements;
        hoistedLocals=locals.ToArray();
        unsafeAccessorCodes=accessorCodes.ToArray();
        hoistedExpressions=selected.Select(pair => pair.Expression).ToArray();
        return true;
    }

    private bool IsNestedFunctionBoundary(SyntaxNode node) =>
        node is AnonymousFunctionExpressionSyntax
            or LocalFunctionStatementSyntax;

    private bool IsInsideAnchorAccess(ExpressionSyntax expression,SemanticModel semanticModel) =>
        expression.Ancestors()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => AnchorAnalysis.IsAnchorAccess(memberAccess,semanticModel));

    private bool IsMeaningfulHoistCandidate(ExpressionSyntax expression,SemanticModel semanticModel) {
        if(expression.Parent is InvocationExpressionSyntax invocation
            &&ReferenceEquals(invocation.Expression,expression)) {
            return false;
        }
        if(expression is MemberBindingExpressionSyntax) {
            return false;
        }
        // 型名(new global::A.B.C(...) の C、キャスト・default・typeof の型など)は値ではないので巻き上げない。
        // 完全修飾名(QualifiedName / AliasQualifiedName)は MemberAccess ではないため、下の判定をすり抜けて
        // 「型を値として巻き上げた __hoistN」になり、new this.__hoistN(...) というコンパイルできないコードを出していた(2026-10-22)。
        if(expression is TypeSyntax) {
            var typeSymbolInfo = semanticModel.GetSymbolInfo(expression);
            if(typeSymbolInfo.Symbol is INamespaceOrTypeSymbol
                ||typeSymbolInfo.CandidateSymbols.Any(symbol => symbol is INamespaceOrTypeSymbol)) {
                return false;
            }
        }
        // 代入の左辺(オブジェクト初期化子・with 式の { Price = ... } のメンバー名など)は値ではなく書き込み先なので巻き上げない。
        // 巻き上げると with{this.__hoistN=...} のようなコンパイルできないコードになる(2026-10-22)。
        if(expression is IdentifierNameSyntax
            &&expression.Parent is AssignmentExpressionSyntax assignment
            &&ReferenceEquals(assignment.Left,expression)) {
            return false;
        }
        if(expression is SimpleNameSyntax simpleName
            &&((simpleName.Parent is MemberAccessExpressionSyntax memberAccess&&ReferenceEquals(memberAccess.Name,simpleName))
                ||(simpleName.Parent is MemberBindingExpressionSyntax memberBinding&&ReferenceEquals(memberBinding.Name,simpleName)))) {
            return false;
        }
        if(IsPredefinedTypeAlias(expression.ToString())) {
            return false;
        }
        var symbolInfo = semanticModel.GetSymbolInfo(expression);
        if(expression is MemberAccessExpressionSyntax
            &&(symbolInfo.Symbol is INamespaceOrTypeSymbol or IMethodSymbol
                ||symbolInfo.CandidateSymbols.Any(symbol => symbol is INamespaceOrTypeSymbol or IMethodSymbol))) {
            return false;
        }
        if(expression is LiteralExpressionSyntax
            or ParenthesizedExpressionSyntax
            or ThisExpressionSyntax
            or BaseExpressionSyntax) {
            return false;
        }
        // -2 のような定数式(単項マイナス+リテラルなど)は巻き上げても得が無い。しかも Range の述語の閉形式などは、比較の相手を
        // 巻き上げ前の式として読み直すので、定義していない __hoistN を参照する不正なコードになる(CS0103)。
        if(semanticModel.GetConstantValue(expression).HasValue||IsConstantLiteralExpression(expression)) {
            return false;
        }
        if(expression is IdentifierNameSyntax identifier) {
            if(symbolInfo.Symbol is INamespaceOrTypeSymbol or IMethodSymbol
                ||symbolInfo.CandidateSymbols.Any(symbol => symbol is INamespaceOrTypeSymbol or IMethodSymbol)) {
                return false;
            }
            return symbolInfo.Symbol is IFieldSymbol or IPropertySymbol;
        }
        return true;
    }

    // リテラル(符号つき・括弧つきを含む)は、ユーザー定義の暗黙変換(int から Int256 など)で型が変わっても定数のまま。
    // 変換後の式は GetConstantValue が値を持たないので、リテラルの形で見分ける。巻き上げると、述語を別の場所へ展開する描画で
    // 巻き上げた変数の宣言が届かず CS0103 になることがある(2026-10-22)。
    private static bool IsConstantLiteralExpression(ExpressionSyntax expression) =>
        expression switch {
            LiteralExpressionSyntax => true,
            ParenthesizedExpressionSyntax parenthesized => IsConstantLiteralExpression(parenthesized.Expression),
            PrefixUnaryExpressionSyntax prefix when prefix.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryMinusExpression)
                || prefix.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryPlusExpression) => IsConstantLiteralExpression(prefix.Operand),
            _ => false,
        };

    private bool IsPredefinedTypeAlias(string text) =>
        text is "bool"
            or "byte"
            or "sbyte"
            or "short"
            or "ushort"
            or "int"
            or "uint"
            or "long"
            or "ulong"
            or "double"
            or "float"
            or "decimal"
            or "string"
            or "char"
            or "object";

    private bool ContainsDisallowedHoistNode(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf(descendIntoChildren: node => !IsNestedFunctionBoundary(node))
            .Any(node => node is AssignmentExpressionSyntax
                or AwaitExpressionSyntax
                or ThrowExpressionSyntax
                or AnonymousFunctionExpressionSyntax
                or QueryExpressionSyntax
                or DeclarationExpressionSyntax
                or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression }
                or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression });

    private bool ReferencesAnyLambdaParameter(
        ExpressionSyntax expression,
        HashSet<string> parameterNames,
        SemanticModel semanticModel) =>
        expression.DescendantNodesAndSelf(descendIntoChildren: node => !IsNestedFunctionBoundary(node))
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => semanticModel.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter
                &&parameterNames.Contains(parameter.Name));

    private bool IsUsableHoistedType(ITypeSymbol? type) =>
        type is not null
        &&type.SpecialType!=SpecialType.System_Void
        &&type is not IErrorTypeSymbol
        &&!ContainsAnonymousType(type);

    private bool SpansOverlap(TextSpan left,TextSpan right) =>
        left.Start<right.End&&right.Start<left.End;

    private bool SymbolReferencedOutsideHoistedExpressions(
        ISymbol symbol,
        ExpressionSyntax body,
        IReadOnlyCollection<ExpressionSyntax> hoistedExpressions,
        SemanticModel semanticModel) {
        if(hoistedExpressions.Count==0) {
            return true;
        }
        if(symbol is IParameterSymbol { Name: "this" }) {
            return UsesThisOutsideHoistedExpressions(body,hoistedExpressions,semanticModel);
        }
        foreach(var identifier in body.DescendantNodesAndSelf(descendIntoChildren: node => !IsNestedFunctionBoundary(node)).OfType<IdentifierNameSyntax>()) {
            if(IsInsideHoistedExpression(identifier,hoistedExpressions)) {
                continue;
            }
            if(SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier).Symbol,symbol)) {
                return true;
            }
        }
        return false;
    }

    private bool UsesThisOutsideHoistedExpressions(
        ExpressionSyntax body,
        IReadOnlyCollection<ExpressionSyntax> hoistedExpressions,
        SemanticModel semanticModel) {
        foreach(var node in body.DescendantNodesAndSelf(descendIntoChildren: node => !IsNestedFunctionBoundary(node))) {
            if(IsInsideHoistedExpression(node,hoistedExpressions)) {
                continue;
            }
            if(node is ThisExpressionSyntax or BaseExpressionSyntax) {
                return true;
            }
            if(node is IdentifierNameSyntax identifier
                &&semanticModel.GetSymbolInfo(identifier).Symbol is ISymbol identifierSymbol
                &&IsInstanceMember(identifierSymbol)) {
                return true;
            }
            if(node is MemberAccessExpressionSyntax memberAccess
                &&semanticModel.GetSymbolInfo(memberAccess).Symbol is ISymbol memberSymbol
                &&IsInstanceMember(memberSymbol)) {
                return true;
            }
        }
        return false;
    }

    private bool IsInstanceMember(ISymbol symbol) =>
        symbol switch {
            IFieldSymbol field => !field.IsStatic,
            IPropertySymbol property => !property.IsStatic,
            IMethodSymbol method => !method.IsStatic,
            IEventSymbol @event => !@event.IsStatic,
            _ => false
        };

    private bool IsInsideHoistedExpression(SyntaxNode node,IReadOnlyCollection<ExpressionSyntax> hoistedExpressions) =>
        hoistedExpressions.Any(expression =>
            node.SpanStart>=expression.SpanStart
            &&node.Span.End<=expression.Span.End);

    private sealed class HoistedExpressionRewriter(IReadOnlyDictionary<ExpressionSyntax,string> replacements):CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<ExpressionSyntax,string> replacements = replacements;

        public override SyntaxNode? Visit(SyntaxNode? node) {
            if(node is ExpressionSyntax expression
                &&this.replacements.TryGetValue(expression,out var replacement)) {
                return SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(expression);
            }
            return base.Visit(node);
        }
    }

    /// <summary>
    /// ソース型の public な非明示的実装 <c>GetEnumerator()</c> を探し、具象エニュメレータ型名と
    /// duck-typing フラグを返す。
    /// source generator の生成コードを検証する。
    /// <br/><c>isDuckTyping=true</c>: エニュメレータが <c>IEnumerator&lt;T&gt;</c> を実装しない
    /// duck-typing struct（例: <c>ImmutableArray&lt;T&gt;.Enumerator</c>）。
    /// この場合 struct 制約 (<c>where TEnum : struct, IEnumerator&lt;T&gt;</c>) に渡せず
    /// <see cref="Linq.Fragments.DuckTypingRoot"/> で直接展開する必要がある。
    /// </summary>
    private (string? typeName,bool isDuckTyping) 具象エニュメレータ型名取得(ITypeSymbol? sourceType,ITypeSymbol elementType,bool useEnumerableInterface) {
        if(useEnumerableInterface||sourceType is not INamedTypeSymbol namedSource) {
            return (null,false);
        }
        // 明示的インターフェース実装を除いた public GetEnumerator() を探す。
        // GetMembers は宣言メンバーしか返さないため、Table<T>:HashSet<T> のような
        // 派生コレクション型でも見つかるよう基底型チェーンを辿る。
        IMethodSymbol? getEnumerator = null;
        for(var current = namedSource;current is not null&&getEnumerator is null;current=current.BaseType) {
            getEnumerator=current.GetMembers("GetEnumerator")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.Parameters.IsEmpty
                    &&m.DeclaredAccessibility==Accessibility.Public
                    &&m.MethodKind!=MethodKind.ExplicitInterfaceImplementation
                    &&!m.IsStatic);
        }
        if(getEnumerator is null) {
            return (null,false);
        }
        // 戻り値が具象値型（インターフェースでも抽象型でもない）であれば使う。
        if(getEnumerator.ReturnType is not INamedTypeSymbol returnType
            ||returnType.IsAbstract
            ||returnType.TypeKind==TypeKind.Interface) {
            return (null,false);
        }
        var typeName = returnType.ToDisplayString(完全修飾形式);
        // IEnumerator<elementType> を実装しているかチェック。
        // ImmutableArray<T>.Enumerator などは duck-typing パターンのみで IEnumerator<T> を実装しない。
        // そのような型を struct 制約 (where TEnum : struct, IEnumerator<T>) に渡すと CS0315 が発生する。
        var implementsIEnumerator = returnType.AllInterfaces.Any(i =>
            i.ContainingNamespace?.ToDisplayString()=="System.Collections.Generic"
            &&i.MetadataName=="IEnumerator`1"
            &&i.TypeArguments.Length==1
            &&SymbolEqualityComparer.Default.Equals(i.TypeArguments[0],elementType));
        return (typeName,isDuckTyping: !implementsIEnumerator);
    }

    /// <summary>
    /// 型がちょうど <c>System.Collections.Generic.List&lt;T&gt;</c> かを判定する。
    /// 継承クラスは内部配列の意味が同じでも別物として扱い、従来の Root パスへ流す。
    /// <c>List&lt;T&gt;</c> と判定された場合は <see cref="Linq.Fragments.ListRoot"/> で
    /// <c>CollectionsMarshal.AsSpan</c> ベースの直走査を使える。
    /// </summary>
    private bool IsListType(ITypeSymbol? type) =>
        type is INamedTypeSymbol named
        &&named.OriginalDefinition.ToDisplayString()=="System.Collections.Generic.List<T>";

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
    internal bool 列挙要素型取得(ITypeSymbol? type,out ITypeSymbol elementType,out bool useEnumerableInterface) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType)&&namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(candidate => IsIEnumerableType(candidate)&&candidate.TypeArguments.Length==1);
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

    private bool TryCreateDelegateFuncFragment(
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

        var invoke = namedDelegate.DelegateInvokeMethod
            ??namedDelegate.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();
        if(invoke is null||invoke.ReturnsVoid) {
            return false;
        }

        var delegateTypeName = delegateType.ToDisplayString(完全修飾形式);
        var delegateFieldName = $"_del{uniqueNumber++}";
        string delegateConstructorArg;
        var delegateConstructorAccessorCodes = Array.Empty<string>();
        if(semanticModel is not null&&containingType is not null) {
            var delegateConstructorCollector = new List<string>();
            delegateConstructorArg=RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,delegateConstructorCollector);
            delegateConstructorAccessorCodes=delegateConstructorCollector.ToArray();
        } else {
            delegateConstructorArg=NormalizeExpressionText(expression.ToString());
        }
        fragment=new DelegateFuncFragment(delegateTypeName,delegateFieldName,delegateConstructorArg) {
            ReturnTypeName=invoke.ReturnType.ToDisplayString(完全修飾形式),
            ParameterTypeNames=invoke.Parameters.Select(p => p.Type.ToDisplayString(完全修飾形式)).ToArray(),
            ParameterNames=invoke.Parameters.Select(p => p.Name).ToArray(),
            CanGenerateMethod=false,
            CanInlineAssign=false,
            BodyUnsafeAccessorCodes=delegateConstructorAccessorCodes,
            ContainsAnchor=AnchorAnalysis.ContainsAnchor(expression,semanticModel),
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
    internal bool 関数式フラグメント生成(
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
            return 関数式フラグメント生成(
                initializerExpression,
                delegateType,
                ref uniqueNumber,
                out fragment,
                semanticModel,
                containingType);
        }

        if(expression is LambdaExpressionSyntax lambda) {
            // 複数文ブロック本体（=>{ ...; return x; ... }、try-catch 含む）は BlockFunc で文インライン化する。
            // source generator の生成コードを検証する。
            if(lambda.Body is BlockSyntax lambdaBlock
                &&!IsSingleReturnBlock(lambdaBlock)
                &&TryBuildBlockFunc(lambdaBlock,GetLambdaParameters(lambda),delegateType,semanticModel,containingType,out var lambdaBlockFunc)) {
                fragment=lambdaBlockFunc;
                return true;
            }

            // 匿名型を含む delegate 型は、匿名型→ジェネリック record struct 透過置換で「名指し可能な型」へ写像して
            // 通常の LambdaFunc としてインライン展開する（delegate 保持に頼らない）。
            // これによりメソッドチェーン中間の匿名型が後続ステージへ展開できるようになる。
            if(TryBuildRegistryMappedLambdaFunc(lambda,delegateType,semanticModel,containingType,out var registryFragment)) {
                fragment=registryFragment;
                return true;
            }

            // source generator の生成コードを検証する。
            // TryBuildRegistryMappedLambdaFunc がこのケースを除外するため、ここで専用フラグメントを生成する。
            if(TryBuildAnonExitDelegateFuncFragment(lambda,delegateType,ref uniqueNumber,out var anonExitFragment,semanticModel,containingType)) {
                fragment=anonExitFragment;
                return true;
            }

            // ラムダが匿名型（または匿名型を含む型）を「返す」場合 → AnonymousDelegateFuncFragment を生成。
            // 引数型のみ匿名型の場合（ソースが匿名型）は対象外：TryCreateLambdaFuncFragment 失敗で return false。
            // TryCreateLambdaFuncFragment より先にチェック（匿名型は delegate 型解決に失敗するため）。
            if(semanticModel is not null
                &&containingType is not null
                &&delegateType is INamedTypeSymbol { TypeArguments.Length:>0 } funcTypeSym
                &&ContainsAnonymousType(funcTypeSym.TypeArguments[funcTypeSym.TypeArguments.Length-1])) {
                var returnTypeSym = funcTypeSym.TypeArguments[funcTypeSym.TypeArguments.Length-1];
                var returnTypeName = ReplaceAnonymousTypesWithTResult(returnTypeSym);
                var paramTypeNames = funcTypeSym.TypeArguments.Take(funcTypeSym.TypeArguments.Length-1)
                    .Select(t => t.ToDisplayString(完全修飾形式)).ToArray();
                var lambdaParamNames = GetLambdaParameters(lambda).ToArray();
                var anonCapturedLocals = Array.Empty<(string Name,string TypeName)>();
                var anonBodyAccessorCodes = Array.Empty<string>();
                if(TryGetLambdaExpressionBody(lambda,out var anonBodyExpr)) {
                    {
                        var anonBodyCollector = new List<string>();
                        RenderExpressionForGeneratedMethod(anonBodyExpr,semanticModel,containingType!,anonBodyCollector);
                        anonBodyAccessorCodes=anonBodyCollector.ToArray();
                    }
                    var anonDataFlow = semanticModel.AnalyzeDataFlow(anonBodyExpr);
                    if(anonDataFlow?.Succeeded==true) {
                        var anonParamSet = new HashSet<string>(lambdaParamNames,StringComparer.Ordinal);
                        anonCapturedLocals=anonDataFlow.DataFlowsIn
                            .Where(sym => (sym is ILocalSymbol||sym is IParameterSymbol)&&!anonParamSet.Contains(sym.Name)&&sym.Name!="this")
                            .Select(sym => (sym.Name,sym is ILocalSymbol loc
                                ? loc.Type.ToDisplayString(完全修飾形式)
                                : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                            .ToArray();
                    }
                }
                var allTypeNames = paramTypeNames.Append(returnTypeName);
                var delegateTypeName = $"global::System.Func<{string.Join(", ",allTypeNames)}>";
                var delegateFieldName = $"_anonDel{uniqueNumber++}";
                var delegateConstructorCollector = new List<string>();
                var delegateConstructorArg = RenderExpressionForGeneratedMethod(lambda,semanticModel,containingType,delegateConstructorCollector);
                fragment=new AnonymousDelegateFuncFragment(delegateTypeName,delegateFieldName,delegateConstructorArg) {
                    ReturnTypeName=returnTypeName,
                    ParameterTypeNames=paramTypeNames,
                    ParameterNames=lambdaParamNames,
                    CanGenerateMethod=false,
                    CapturedLocals=anonCapturedLocals,
                    BodyUnsafeAccessorCodes=anonBodyAccessorCodes
                    .Concat(delegateConstructorCollector)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                    ContainsAnchor=AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
                };
                return true;
            }

            if(!TryCreateLambdaFuncFragment(lambda,delegateType,out var lambdaSpec)) {
                // delegate 型の引数に匿名型が含まれる場合（ソースが匿名型など）は投げずに return false。
                // 呼び出し元が元の System.Linq 式を維持する処理を行う。
                if(delegateType is INamedTypeSymbol { TypeArguments.Length:>0 } ft
                    &&ft.TypeArguments.Any(ContainsAnonymousType)) {
                    fragment=null!;
                    return false;
                }
                throw LambdaInlineException(lambda,semanticModel,"ラムダ本体が式または単一 return 文ではないか、delegate 型を解決できませんでした。");
            }

            var body = lambdaSpec.Body;
            var capturedLocals = Array.Empty<(string Name,string TypeName)>();
            var hoistedLocals = Array.Empty<HoistedLocal>();
            var bodyAccessorCodes = Array.Empty<string>();
            var capturesThis = false;
            ExpressionSyntax? lambdaBodyExpr = null;
            ExpressionSyntax? originalLambdaBodyExpr = null;
            IReadOnlyCollection<ExpressionSyntax> hoistedExpressions = Array.Empty<ExpressionSyntax>();
            IReadOnlyDictionary<string,string>? hoistReplacementMap = null;
            if(semanticModel is not null&&TryGetLambdaExpressionBody(lambda,out lambdaBodyExpr)) {
                originalLambdaBodyExpr=lambdaBodyExpr;
                if(containingType is not null) {
                    if(TryHoistInvariantSubexpressions(
                        lambdaBodyExpr,
                        lambdaSpec.ParameterNames,
                        semanticModel,
                        containingType,
                        ref uniqueNumber,
                        out hoistReplacementMap,
                        out hoistedLocals,
                        out var hoistAccessorCodes,
                        out hoistedExpressions)) {
                        bodyAccessorCodes=hoistAccessorCodes;
                    }
                    var bodyCollector = new List<string>();
                    body=RenderExpressionForGeneratedMethod(lambdaBodyExpr,semanticModel,containingType,bodyCollector,hoistReplacementMap);
                    bodyAccessorCodes=bodyAccessorCodes.Concat(bodyCollector).ToArray();
                }
                // source generator の生成コードを検証する。
                // lambda 自身のパラメータと暗黙の this は除外する。
                var dataFlow = semanticModel.AnalyzeDataFlow(originalLambdaBodyExpr);
                if(dataFlow?.Succeeded==true) {
                    var paramSet = new HashSet<string>(lambdaSpec.ParameterNames,StringComparer.Ordinal);
                    var thisSymbol = dataFlow.DataFlowsIn.FirstOrDefault(sym => sym.Name=="this") as IParameterSymbol;
                    capturesThis=thisSymbol is not null
                        &&SymbolReferencedOutsideHoistedExpressions(thisSymbol,originalLambdaBodyExpr!,hoistedExpressions,semanticModel);
                    capturedLocals=dataFlow.DataFlowsIn
                        .Where(sym => (sym is ILocalSymbol||sym is IParameterSymbol)&&!paramSet.Contains(sym.Name)&&sym.Name!="this")
                        .Where(sym => SymbolReferencedOutsideHoistedExpressions(sym,originalLambdaBodyExpr!,hoistedExpressions,semanticModel))
                        .Select(sym => (sym.Name,sym is ILocalSymbol loc
                            ? loc.Type.ToDisplayString(完全修飾形式)
                            : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                        .ToArray();
                    // ラムダ内の this. は Fragment struct 内では @this として参照されるため、
                    // struct closure のキャプチャ変数として明示的に持ち回る。
                    // 型は containingType（呼び出し元の囲み型。Full Fusion 時は呼び出し先と異なりうる）ではなく
                    // 実際の暗黙 this パラメーターの型（ラムダの宣言元インスタンスの型）を用いる。
                    if(capturesThis&&thisSymbol is not null) {
                        capturedLocals=capturedLocals
                            .Append(("@this",thisSymbol.Type.ToDisplayString(完全修飾形式)))
                            .ToArray();
                    }
                }
            }

            if(semanticModel is not null
                &&containingType is not null
                &&lambdaBodyExpr is not null
                &&hoistedLocals.Length==0
                &&TryFindSystemLinqInvocation(lambdaBodyExpr,semanticModel,out var scalarInvocation,out var scalarMethod)) {
                var snapshot = uniqueNumber;
                var scalarUnsafeAccessorCollector = new List<string>();
                if(呼び出し式からフラグメント生成(
                        semanticModel,
                        scalarInvocation,
                        containingType,
                        ref snapshot,
                        out var scalarFragment,
                        out var scalarStaticMethods,
                        scalarUnsafeAccessorCollector)
                    &&scalarFragment is Scalar
                    &&ScalarFragmentSupportsInlineEnumeration(scalarFragment)) {
                    uniqueNumber=snapshot;
                    var bodyExpressionCollector = new List<string>();
                    var renderedBodyExpression = RenderExpressionForGeneratedMethod(
                        lambdaBodyExpr,
                        semanticModel,
                        containingType,
                        bodyExpressionCollector,
                        hoistReplacementMap);
                    var scalarExpressionCollector = new List<string>();
                    var renderedScalarExpression = RenderExpressionForGeneratedMethod(
                        scalarInvocation,
                        semanticModel,
                        containingType,
                        scalarExpressionCollector,
                        hoistReplacementMap);
                    fragment=new InlinedScalarFunc(scalarFragment) {
                        ReturnTypeName=lambdaSpec.ReturnTypeName,
                        ParameterTypeNames=lambdaSpec.ParameterTypeNames,
                        ParameterNames=lambdaSpec.ParameterNames,
                        CanGenerateMethod=false,
                        CanInlineAssign=true,
                        RequiresStatementInlining=true,
                        InlineBody=string.Empty,
                        BodyExpression=renderedBodyExpression,
                        ScalarExpression=renderedScalarExpression,
                        ScalarReturnTypeName=semanticModel.GetTypeInfo(scalarInvocation).Type?.ToDisplayString(完全修飾形式)??scalarFragment.GetType().Name,
                        CapturedLocals=capturedLocals,
                        HoistedLocals=hoistedLocals,
                        BodyUnsafeAccessorCodes=bodyAccessorCodes
                            .Concat(scalarUnsafeAccessorCollector)
                            .Concat(bodyExpressionCollector)
                            .Concat(scalarExpressionCollector)
                            .Concat(scalarFragment.CollectAllUnsafeAccessorCodes())
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        ScalarStaticMethods=scalarStaticMethods,
                        OuterParameterNames=lambdaSpec.ParameterNames,
                        ContainsAnchor=AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
                    };
                    return true;
                }

                // シーケンスを返す LINQ は SelectMany などの collection selector として有効なので、
                // スカラー Fragment 化できない場合は通常ラムダとして残す。
            }

            // キャプチャなし（this 含む）なら実質 static → struct ゼロコピーでメソッド生成可能。
            var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
                ||(!capturesThis&&capturedLocals.Length==0);
            fragment=new LambdaFunc {
                ReturnTypeName=lambdaSpec.ReturnTypeName,
                ParameterTypeNames=lambdaSpec.ParameterTypeNames,
                ParameterNames=lambdaSpec.ParameterNames,
                CanGenerateMethod=canGenerateMethod,
                CanInlineAssign=true,
                InlineBody=body,
                CapturedLocals=capturedLocals,
                HoistedLocals=hoistedLocals,
                BodyUnsafeAccessorCodes=bodyAccessorCodes,
                ContainsAnchor=AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
            };
            return true;
        }

        if(expression is AnonymousMethodExpressionSyntax anonymousMethod) {
            // delegate(){ ...; return x; ... }（try-catch 含む複数文）も BlockFunc で文インライン化する。
            if(anonymousMethod.Block is BlockSyntax anonymousBlock
                &&!IsSingleReturnBlock(anonymousBlock)
                &&TryBuildBlockFunc(anonymousBlock,GetAnonymousMethodParameters(anonymousMethod),delegateType,semanticModel,containingType,out var anonymousBlockFunc)) {
                fragment=anonymousBlockFunc;
                return true;
            }

            if(!TryCreateAnonymousMethodFuncFragment(anonymousMethod,delegateType,out var anonymousSpec,out var bodyExpression)) {
                // delegate 式が匿名型を返す場合も AnonymousDelegateFuncFragment を生成する。
                if(delegateType is INamedTypeSymbol { TypeArguments.Length:>0 } anonFuncType
                    &&anonFuncType.TypeArguments.Any(ContainsAnonymousType)
                    &&semanticModel is not null&&containingType is not null) {
                    var anonReturnTypeName = ReplaceAnonymousTypesWithTResult(anonFuncType.TypeArguments[anonFuncType.TypeArguments.Length-1]);
                    var anonParamTypeNames = anonFuncType.TypeArguments.Take(anonFuncType.TypeArguments.Length-1)
                        .Select(t => t.ToDisplayString(完全修飾形式)).ToArray();
                    var anonParamNames = GetAnonymousMethodParameters(anonymousMethod).ToArray();
                    var anonDelegateTypeName = $"global::System.Func<{string.Join(", ",anonParamTypeNames.Append(anonReturnTypeName))}>";
                    var anonFieldName = $"_anonDel{uniqueNumber++}";
                    var anonCtorCollector = new List<string>();
                    var anonCtorArg = RenderExpressionForGeneratedMethod(anonymousMethod,semanticModel,containingType,anonCtorCollector);
                    fragment=new AnonymousDelegateFuncFragment(anonDelegateTypeName,anonFieldName,anonCtorArg) {
                        ReturnTypeName=anonReturnTypeName,
                        ParameterTypeNames=anonParamTypeNames,
                        ParameterNames=anonParamNames,
                        CanGenerateMethod=false,
                        BodyUnsafeAccessorCodes=anonCtorCollector
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        ContainsAnchor=AnchorAnalysis.ContainsAnchor(anonymousMethod,semanticModel),
                    };
                    return true;
                }
                throw new InvalidOperationException($"delegate 式をインライン展開できませんでした。LINQ delegate: '{NormalizeExpressionText(anonymousMethod.ToString())}'。理由: 本体が単一 return 文ではないか、delegate 型を解決できませんでした。");
            }

            var body = anonymousSpec.Body;
            var capturedLocals = Array.Empty<(string Name,string TypeName)>();
            var hoistedLocals = Array.Empty<HoistedLocal>();
            var bodyAccessorCodes = Array.Empty<string>();
            var capturesThis = false;
            IReadOnlyCollection<ExpressionSyntax> hoistedExpressions = Array.Empty<ExpressionSyntax>();
            IReadOnlyDictionary<string,string>? hoistReplacementMap = null;
            if(semanticModel is not null&&containingType is not null) {
                if(TryHoistInvariantSubexpressions(
                    bodyExpression,
                    anonymousSpec.ParameterNames,
                    semanticModel,
                    containingType,
                    ref uniqueNumber,
                    out hoistReplacementMap,
                    out hoistedLocals,
                    out var hoistAccessorCodes,
                    out hoistedExpressions)) {
                    bodyAccessorCodes=hoistAccessorCodes;
                }
                var bodyCollector = new List<string>();
                body=RenderExpressionForGeneratedMethod(bodyExpression,semanticModel,containingType,bodyCollector,hoistReplacementMap);
                bodyAccessorCodes=bodyAccessorCodes.Concat(bodyCollector).ToArray();

                var dataFlow = semanticModel.AnalyzeDataFlow(bodyExpression);
                if(dataFlow?.Succeeded==true) {
                    var paramSet = new HashSet<string>(anonymousSpec.ParameterNames,StringComparer.Ordinal);
                    var thisSymbol = dataFlow.DataFlowsIn.FirstOrDefault(sym => sym.Name=="this") as IParameterSymbol;
                    capturesThis=thisSymbol is not null
                        &&SymbolReferencedOutsideHoistedExpressions(thisSymbol,bodyExpression,hoistedExpressions,semanticModel);
                    capturedLocals=dataFlow.DataFlowsIn
                        .Where(sym => (sym is ILocalSymbol||sym is IParameterSymbol)&&!paramSet.Contains(sym.Name)&&sym.Name!="this")
                        .Where(sym => SymbolReferencedOutsideHoistedExpressions(sym,bodyExpression,hoistedExpressions,semanticModel))
                        .Select(sym => (sym.Name,sym is ILocalSymbol loc
                            ? loc.Type.ToDisplayString(完全修飾形式)
                            : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                        .ToArray();
                }
            }

            fragment=new LambdaFunc {
                ReturnTypeName=anonymousSpec.ReturnTypeName,
                ParameterTypeNames=anonymousSpec.ParameterTypeNames,
                ParameterNames=anonymousSpec.ParameterNames,
                CanGenerateMethod=!capturesThis&&capturedLocals.Length==0,
                CanInlineAssign=true,
                InlineBody=body,
                CapturedLocals=capturedLocals,
                HoistedLocals=hoistedLocals,
                BodyUnsafeAccessorCodes=bodyAccessorCodes,
                ContainsAnchor=AnchorAnalysis.ContainsAnchor(bodyExpression,semanticModel),
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
    /// source generator の生成コードを検証する。
    /// source generator の生成コードを検証する。
    /// source generator の生成コードを検証する。
    /// </summary>
    private bool TryBuildRegistryMappedLambdaFunc(
        LambdaExpressionSyntax lambda,
        ITypeSymbol delegateType,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType,
        out FuncFragment fragment) {
        fragment=null!;
        var registry = this.state.AnonymousRegistry;
        if(registry is null||semanticModel is null||containingType is null) {
            return false;
        }
        if(delegateType is not INamedTypeSymbol funcType
            ||funcType.TypeArguments.Length==0
            ||!funcType.TypeArguments.Any(ContainsAnonymousType)
            ||!IsInlineDelegateType(funcType)) {
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
        var returnTypeSym = funcType.TypeArguments[funcType.TypeArguments.Length-1];

        var strippedBody = StripParentheses(body);
        if(ContainsAnonymousType(returnTypeSym)&&strippedBody is not AnonymousObjectCreationExpressionSyntax) {
            // identity lambda (p=>p) は引数の struct record をそのまま返せるため通過させる。
            // それ以外（本体ルートが匿名生成式でも identity でもない）は struct record
            // ファクトリへ変換できないため、呼び出し元の TryBuildAnonExitDelegateFuncFragment へ委ねる。
            if(strippedBody is not IdentifierNameSyntax identityId||!parameters.Contains(identityId.Identifier.ValueText)) {
                return false;
            }
        }

        var parameterTypeNames = paramTypeSyms.Select(registry.ResolveClosedName).ToArray();
        var returnTypeName = registry.ResolveClosedName(returnTypeSym);

        var bodyCollector = new List<string>();
        var bodyText = strippedBody is AnonymousObjectCreationExpressionSyntax anonCreate
            ? registry.RenderAnonymousCreation(anonCreate,semanticModel,
                expr => RenderExpressionForGeneratedMethod(expr,semanticModel,containingType,bodyCollector))
            : RenderExpressionForGeneratedMethod(body,semanticModel,containingType,bodyCollector);

        var capturedLocals = Array.Empty<(string Name,string TypeName)>();
        var capturesThis = false;
        var dataFlow = semanticModel.AnalyzeDataFlow(body);
        if(dataFlow?.Succeeded==true) {
            var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
            var thisSymbol = dataFlow.DataFlowsIn.FirstOrDefault(sym => sym.Name=="this") as IParameterSymbol;
            capturesThis=thisSymbol is not null;
            capturedLocals=dataFlow.DataFlowsIn
                .Where(sym => (sym is ILocalSymbol||sym is IParameterSymbol)&&!paramSet.Contains(sym.Name)&&sym.Name!="this")
                .Select(sym => (sym.Name,sym is ILocalSymbol loc
                    ? loc.Type.ToDisplayString(完全修飾形式)
                    : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                .ToArray();
            // ラムダ内の this. は Fragment struct 内では @this として参照されるため、
            // 実際の暗黙 this パラメーターの型（ラムダの宣言元インスタンスの型）でキャプチャする。
            if(thisSymbol is not null) {
                capturedLocals=capturedLocals
                    .Append(("@this",thisSymbol.Type.ToDisplayString(完全修飾形式)))
                    .ToArray();
            }
        }

        var canGenerateMethod = lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
            ||(!capturesThis&&capturedLocals.Length==0);

        fragment=new LambdaFunc {
            ReturnTypeName=returnTypeName,
            ParameterTypeNames=parameterTypeNames,
            ParameterNames=parameters.ToArray(),
            CanGenerateMethod=canGenerateMethod,
            CanInlineAssign=true,
            InlineBody=bodyText,
            CapturedLocals=capturedLocals,
            BodyUnsafeAccessorCodes=bodyCollector.ToArray(),
            ContainsAnchor=AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
        };
        return true;
    }

    /// <summary>
    /// source generator の生成コードを検証する。
    /// <see cref="AnonymousDelegateFuncFragment"/> として構築する。
    /// <br/>対象: 引数が struct record に置換済みで、戻り値は実際の匿名型を返す必要がある「出口ステップ」。
    /// <br/>例1: GroupBy key selector <c>p=&gt;p</c> (identity) → <c>(struct_rec p)=&gt;new{p.field,...}</c>
    /// <br/>例2: GroupBy element selector <c>p=&gt;new{{x=p}}</c> → <c>(struct_rec p)=&gt;new{{x=new{{p.field,...}}}}</c>
    /// </summary>
    private bool TryBuildAnonExitDelegateFuncFragment(
        LambdaExpressionSyntax lambda,
        ITypeSymbol delegateType,
        ref int uniqueNumber,
        out FuncFragment fragment,
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType) {
        fragment=null!;
        var registry = this.state.AnonymousRegistry;
        if(registry is null||semanticModel is null||containingType is null) {
            return false;
        }
        if(delegateType is not INamedTypeSymbol funcType
            ||funcType.TypeArguments.Length==0
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }
        var paramTypeSyms = funcType.TypeArguments.Take(funcType.TypeArguments.Length-1).ToArray();
        var returnTypeSym = funcType.TypeArguments[funcType.TypeArguments.Length-1];
        // このパスは: 戻り値が匿名型の identity lambda (p=>p) など、
        // TryBuildRegistryMappedLambdaFunc が struct record ファクトリへ変換できなかった場合に限定する。
        if(!ContainsAnonymousType(returnTypeSym)) {
            return false;
        }
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }
        var parameters = GetLambdaParameters(lambda);
        if(parameters.Count!=paramTypeSyms.Length) {
            return false;
        }
        var strippedBody = StripParentheses(body);
        // identity: p=>p → 戻り値の匿名型全体を struct record のメンバーから再構成する。
        if(strippedBody is not IdentifierNameSyntax identId||!parameters.Contains(identId.Identifier.ValueText)) {
            return false;
        }
        var bodyCollector = new List<string>();
        var bodyText = registry.ReconstructAnonymousCreation(returnTypeSym,identId.Identifier.ValueText);
        var paramTypeNames = paramTypeSyms.Select(registry.ResolveClosedName).ToArray();
        // キャプチャローカルを収集する。
        var capturedLocals = Array.Empty<(string Name,string TypeName)>();
        var dataFlow = semanticModel.AnalyzeDataFlow(body);
        if(dataFlow?.Succeeded==true) {
            var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
            capturedLocals=dataFlow.DataFlowsIn
                .Where(sym => (sym is ILocalSymbol||sym is IParameterSymbol)&&!paramSet.Contains(sym.Name)&&sym.Name!="this")
                .Select(sym => (sym.Name,sym is ILocalSymbol loc
                    ? loc.Type.ToDisplayString(完全修飾形式)
                    : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                .ToArray();
        }
        // 引数を struct record 型で明示したラムダ式を組み立てて、デリゲートとして格納する。
        var paramDecls = parameters.Zip(paramTypeNames,(n,t) => $"{t} {n}");
        var delegateLambda = $"({string.Join(",",paramDecls)})=>{bodyText}";
        var allTypeNames = paramTypeNames.Append("TResult");
        var delegateTypeName2 = $"global::System.Func<{string.Join(",",allTypeNames)}>";
        var delegateFieldName = $"_anonExitDel{uniqueNumber++}";
        fragment=new AnonymousDelegateFuncFragment(delegateTypeName2,delegateFieldName,delegateLambda) {
            ReturnTypeName="TResult",
            ParameterTypeNames=paramTypeNames,
            ParameterNames=parameters.ToArray(),
            CanGenerateMethod=false,
            CapturedLocals=capturedLocals,
            BodyUnsafeAccessorCodes=bodyCollector.ToArray(),
            ContainsAnchor=AnchorAnalysis.ContainsAnchor(lambda,semanticModel),
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
    private bool TryGetInlineableDelegateVariableInitializer(
        ExpressionSyntax expression,
        SemanticModel? semanticModel,
        out ExpressionSyntax initializerExpression) {
        initializerExpression=null!;
        if(semanticModel is null
            ||expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax)) {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if(symbol is not (ILocalSymbol or IFieldSymbol)
            ||symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax {
                Initializer.Value: LambdaExpressionSyntax or AnonymousMethodExpressionSyntax
            } variableDeclarator) {
            return false;
        }

        initializerExpression=variableDeclarator.Initializer.Value;
        return true;
    }

    private InvalidOperationException LambdaInlineException(
        LambdaExpressionSyntax lambda,
        SemanticModel? semanticModel,
        string reason,
        InvocationExpressionSyntax? linqInvocation = null) {
        var linqText = linqInvocation is not null
            ? NormalizeExpressionText(linqInvocation.ToString())
            : semanticModel is not null&&TryFindSystemLinqInvocation(lambda,semanticModel,out var foundInvocation,out _)
                ? NormalizeExpressionText(foundInvocation.ToString())
                : NormalizeExpressionText(lambda.ToString());
        return new InvalidOperationException($"ラムダ式をインライン展開できませんでした。LINQ: '{linqText}'。理由: {reason}");
    }

    // スカラー subquery を InlinedScalarFunc（using-enumerator 文インライン）で展開できるかを判定する。
    // Sum/ToList/ToArray/ToDictionary など直接実行のみの PipelineTerminal は enumerator 式を作れず
    // GetEnumeratorExpression が NotSupportedException を投げるため、その場合は InlinedScalarFunc 化を諦め、
    // 呼び出し元で生のラムダ本体テキスト（実行時 LINQ-to-objects）として残す（Method 版と同じ挙動）。
    private bool ScalarFragmentSupportsInlineEnumeration(Fragment scalarFragment) {
        try {
            _=scalarFragment.GetEnumeratorExpression;
            return true;
        } catch(NotSupportedException) {
            return false;
        }
    }
    private bool TryFindSystemLinqInvocation(
        SyntaxNode node,
        SemanticModel semanticModel,
        out InvocationExpressionSyntax invocation,
        out IMethodSymbol method) {
        foreach(var candidate in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()) {
            if(semanticModel.GetSymbolInfo(candidate).Symbol is IMethodSymbol candidateMethod
                &&SystemLinqEnumerableメソッド判定(candidateMethod)) {
                invocation=candidate;
                method=candidateMethod;
                return true;
            }
        }

        invocation=null!;
        method=null!;
        return false;
    }

    internal static List<string> 未対応Enumerableメソッド一覧取得(Linq傍受対象型 target) {
        // ローカルメソッド全体を再構成する前に、Fragment クラス自体が存在しない Enumerable メソッド名を列挙する。
        // 「System.Linq.Enumerable を含むメソッドは全部 intercept する」が前提なので、
        // 未対応メソッドは target から落とすのではなく、生成 interceptor から例外で見えるようにする。
        return target.LinqAnalysis.UnsupportedMethods.ToList();
    }

    internal static bool Enumerableメソッドがフラグメント対応済みか(string methodName)
       => methodName=="Any"||methodName=="TryGetNonEnumeratedCount"||IsKnownEnumerableRootMethod(methodName)||OperatorRegistry.IsKnownEnumerableMethodName(methodName);

    internal static bool IsKnownEnumerableRootMethod(string methodName) => OperatorRegistry.IsRootMethodName(methodName);

    internal static bool IsKnownEnumerableRootMethod(IMethodSymbol method) =>
        method.ContainingType.ToDisplayString()=="System.Linq.Enumerable"
        &&OperatorRegistry.IsRootMethodName(method.Name);

    private Enumerable CreateRootEnumerableFragment(string sourceExpression,string elementType,string enumeratorTypeName,bool useEnumerableInterface,int 番号) {
        return new Root(
            番号,
            sourceExpression,
            elementType,
            enumeratorTypeName,
            useEnumerableInterface);
    }

    private bool TryBuildKnownEnumerableRoot(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap = null) {
        fragment=null!;
        if(expression is not InvocationExpressionSyntax invocation
            ||semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        if(TryBuildKnownEnumerableRootFromSystemInvocation(
            semanticModel,
            invocation,
            method.ReducedFrom??method,
            containingType,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            replacementMap)) {
            return true;
        }

        return TryBuildKnownEnumerableRootFromOrdinaryMethod(
            semanticModel,
            invocation,
            method,
            containingType,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            replacementMap);
    }

    private bool TryBuildKnownEnumerableRootFromSystemInvocation(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
        if(method.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            ||!OperatorRegistry.IsRootMethodName(method.Name)) {
            return false;
        }

        var context = new RootFactoryContext(
            semanticModel,
            invocation,
            method,
            containingType,
            uniqueNumber,
            unsafeAccessorCollector,
            replacementMap,
            this);
        if(!OperatorRegistry.TryCreateRoot(context,out fragment)) {
            return false;
        }
        uniqueNumber=context.UniqueNumber;
        return true;
    }
    private bool TryBuildKnownEnumerableRootFromOrdinaryMethod(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? outerReplacementMap = null) {
        fragment=null!;
        var definition = method.ReducedFrom??method;
        if(definition.ContainingType.Name=="UnorderedGuard") {
            return false;
        }
        if(definition.MethodKind!=MethodKind.Ordinary
            ||!CanInlineOrdinaryEnumerableRootMethod(definition)
            ||definition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax
            ||invocation.ArgumentList.Arguments.Any(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None))
            ||!TryGetSingleExpressionBody(methodSyntax,out var bodyExpression)) {
            return false;
        }

        List<string> actualArguments;
        if(definition.IsExtensionMethod
            &&invocation.Expression is MemberAccessExpressionSyntax extensionMemberAccess
            &&invocation.ArgumentList.Arguments.Count<definition.Parameters.Length) {
            if(!TryCreateExtensionActualArgumentList(
                definition,
                invocation,
                extensionMemberAccess,
                semanticModel,
                containingType,
                unsafeAccessorCollector,
                outerReplacementMap,
                out actualArguments)) {
                return false;
            }
        } else if(!TryCreateOrdinaryActualArgumentList(
            method,
            invocation,
            semanticModel,
            containingType,
            unsafeAccessorCollector,
            outerReplacementMap,
            out actualArguments)) {
            return false;
        }

        var bodySemanticModel = semanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        var replacementMap = BuildInlineEnumerableRootReplacementMap(
            definition,
            invocation,
            methodSyntax,
            bodyExpression,
            bodySemanticModel,
            semanticModel,
            containingType,
            actualArguments,
            unsafeAccessorCollector);

        if(TryBuildTransparentParameterEnumerableRoot(
            semanticModel,
            invocation,
            definition,
            containingType,
            bodyExpression,
            bodySemanticModel,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            outerReplacementMap)) {
            return true;
        }

        if(bodyExpression is InvocationExpressionSyntax bodyInvocation
            &&bodySemanticModel.GetSymbolInfo(bodyInvocation).Symbol is IMethodSymbol bodyMethod
            &&bodyMethod.ContainingType.ToDisplayString()=="System.Linq.Enumerable") {
            return TryBuildKnownEnumerableRootFromSystemInvocation(
                bodySemanticModel,
                bodyInvocation,
                bodyMethod,
                definition.ContainingType,
                ref uniqueNumber,
                out fragment,
                unsafeAccessorCollector,
                replacementMap);
        }

        if(bodyExpression is CollectionExpressionSyntax collectionExpression) {
            return TryBuildKnownEnumerableRootFromCollectionExpression(
                bodySemanticModel,
                collectionExpression,
                definition,
                definition.ContainingType,
                ref uniqueNumber,
                out fragment,
                unsafeAccessorCollector,
                replacementMap);
        }

        return false;
    }

    private bool CanInlineOrdinaryEnumerableRootMethod(IMethodSymbol method) {
        if(method.IsAbstract) {
            return false;
        }

        if(method.IsVirtual||method.IsOverride) {
            return method.IsSealed||method.ContainingType.IsSealed;
        }

        return true;
    }

    private bool TryBuildTransparentParameterEnumerableRoot(
        SemanticModel invocationSemanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodDefinition,
        INamedTypeSymbol invocationContainingType,
        ExpressionSyntax bodyExpression,
        SemanticModel bodySemanticModel,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
        bodyExpression=StripParenthesesForRootInlining(bodyExpression);
        if(bodyExpression is not IdentifierNameSyntax identifier
            ||bodySemanticModel.GetSymbolInfo(identifier).Symbol is not IParameterSymbol parameter) {
            return false;
        }

        var parameterIndex = -1;
        for(var i = 0;i<methodDefinition.Parameters.Length;i++) {
            if(SymbolEqualityComparer.Default.Equals(methodDefinition.Parameters[i],parameter)
                ||methodDefinition.Parameters[i].Name==parameter.Name) {
                parameterIndex=i;
                break;
            }
        }
        if(parameterIndex<0
            ||!TryGetInvocationArgumentExpression(methodDefinition,invocation,parameterIndex,out var sourceExpression)) {
            return false;
        }

        return TryBuildEnumerableRootFromExpressionWithReplacement(
            invocationSemanticModel,
            sourceExpression,
            invocationContainingType,
            ref uniqueNumber,
            out fragment,
            unsafeAccessorCollector,
            replacementMap);
    }

    internal static bool TryGetInvocationArgumentExpression(
        IMethodSymbol methodDefinition,
        InvocationExpressionSyntax invocation,
        int parameterIndex,
        out ExpressionSyntax expression) {
        expression=null!;
        if(parameterIndex<0||parameterIndex>=methodDefinition.Parameters.Length) {
            return false;
        }

        var hasReducedExtensionReceiver =
            methodDefinition.IsExtensionMethod
            &&invocation.Expression is MemberAccessExpressionSyntax
            &&invocation.ArgumentList.Arguments.Count<methodDefinition.Parameters.Length;
        if(hasReducedExtensionReceiver
            &&parameterIndex==0
            &&invocation.Expression is MemberAccessExpressionSyntax extensionMemberAccess) {
            expression=extensionMemberAccess.Expression;
            return true;
        }

        var nextParameterIndex = hasReducedExtensionReceiver ? 1 : 0;
        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i = 0;i<methodDefinition.Parameters.Length;i++) {
                    if(methodDefinition.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                targetParameterIndex=nextParameterIndex++;
            }

            if(targetParameterIndex==parameterIndex) {
                expression=argument.Expression;
                return true;
            }
        }

        return false;
    }

    private ExpressionSyntax StripParenthesesForRootInlining(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }

        return expression;
    }

    private bool TryGetEnumerableElementTypeForRootInlining(ITypeSymbol? type,out ITypeSymbol elementType,out bool useEnumerableInterface) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType)&&namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                useEnumerableInterface=true;
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(candidate => IsIEnumerableType(candidate)&&candidate.TypeArguments.Length==1);
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

    private (string? typeName,bool isDuckTyping) GetConcreteEnumeratorTypeForRootInlining(ITypeSymbol? sourceType,ITypeSymbol elementType,bool useEnumerableInterface) {
        if(useEnumerableInterface||sourceType is not INamedTypeSymbol namedSource) {
            return (null,false);
        }

        var getEnumerator = namedSource.GetMembers("GetEnumerator")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Parameters.IsEmpty
                &&m.DeclaredAccessibility==Accessibility.Public
                &&m.MethodKind!=MethodKind.ExplicitInterfaceImplementation
                &&!m.IsStatic);
        if(getEnumerator is null
            ||getEnumerator.ReturnType is not INamedTypeSymbol returnType
            ||returnType.IsAbstract
            ||returnType.TypeKind==TypeKind.Interface) {
            return (null,false);
        }

        var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var implementsIEnumerator = returnType.AllInterfaces.Any(i =>
            i.ContainingNamespace?.ToDisplayString()=="System.Collections.Generic"
            &&i.MetadataName=="IEnumerator`1"
            &&i.TypeArguments.Length==1
            &&SymbolEqualityComparer.Default.Equals(i.TypeArguments[0],elementType));
        return (typeName,isDuckTyping: !implementsIEnumerator);
    }

    private string RenderExpressionForRootInlining(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        var text = RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);
        return ApplyExpressionReplacementMap(text,replacementMap);
    }

    internal bool TryBuildEnumerableRootFromExpressionWithReplacement(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
        expression=StripParenthesesForRootInlining(expression);
        if(TryBuildKnownEnumerableRoot(semanticModel,expression,containingType,ref uniqueNumber,out fragment,unsafeAccessorCollector,replacementMap)) {
            return true;
        }

        var typeInfo = semanticModel.GetTypeInfo(expression);
        var expressionType = typeInfo.Type??typeInfo.ConvertedType;
        if(!TryGetEnumerableElementTypeForRootInlining(expressionType,out var elementType,out var useEnumerableInterface)) {
            if(IsNonGenericIEnumerableType(expressionType)) {
                fragment=new NonGenericRoot(
                    uniqueNumber++,
                    RenderExpressionForRootInlining(expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap)) {
                    SourceContainsAnchor=AnchorAnalysis.ContainsAnchor(expression,semanticModel),
                };
                return true;
            }

            return false;
        }

        var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var (concreteEnumeratorTypeName,isDuckTyping)=GetConcreteEnumeratorTypeForRootInlining(expressionType,elementType,useEnumerableInterface);
        var enumeratorTypeName = concreteEnumeratorTypeName
            ??$"global::System.Collections.Generic.IEnumerator<{elementTypeName}>";
        var effectiveUseEnumerableInterface = useEnumerableInterface||concreteEnumeratorTypeName is null;
        var sourceExpression = RenderExpressionForRootInlining(expression,semanticModel,containingType,unsafeAccessorCollector,replacementMap);
        var number = uniqueNumber++;
        fragment=CreateSourceRoot(
            expressionType,
            number,
            sourceExpression,
            elementTypeName,
            enumeratorTypeName,
            concreteEnumeratorTypeName,
            isDuckTyping,
            effectiveUseEnumerableInterface);
        fragment.SourceContainsAnchor=AnchorAnalysis.ContainsAnchor(expression,semanticModel);
        return true;
    }

    private Enumerable CreateSourceRoot(
        ITypeSymbol? expressionType,
        int number,
        string sourceExpression,
        string elementTypeName,
        string enumeratorTypeName,
        string? concreteEnumeratorTypeName,
        bool isDuckTyping,
        bool useEnumerableInterface) {
        if(expressionType is IArrayTypeSymbol) {
            return new ArrayRoot(number,sourceExpression,elementTypeName);
        }
        if(IsListType(expressionType)) {
            return new ListRoot(number,sourceExpression,elementTypeName);
        }
        if(IsTsavoriteParallelSetType(expressionType,out _)) {
            return new TsavoriteParallelSetRoot(number,sourceExpression,elementTypeName,enumeratorTypeName);
        }
        if(IsTsavoriteParallelHashSetType(expressionType,out _)) {
            return new TsavoriteParallelHashSetRoot(number,sourceExpression,elementTypeName,enumeratorTypeName);
        }
        if(IsTsavoriteConcreteSetType(expressionType,out _)) {
            return new TsavoriteSetRoot(number,sourceExpression,elementTypeName,enumeratorTypeName);
        }
        if(IsTsavoriteConcreteHashSetType(expressionType,out _)) {
            return new TsavoriteHashSetRoot(number,sourceExpression,elementTypeName,enumeratorTypeName);
        }
        if(IsPersistedConcreteListType(expressionType,out _)) {
            return new PersistedListRoot(number,sourceExpression,elementTypeName);
        }
        if(IsPersistedConcreteSetType(expressionType,out _)) {
            return new PersistedSetRoot(number,sourceExpression,elementTypeName,enumeratorTypeName);
        }
        if(isDuckTyping&&concreteEnumeratorTypeName is not null) {
            return new DuckTypingRoot(number,sourceExpression,elementTypeName,concreteEnumeratorTypeName);
        }
        return CreateRootEnumerableFragment(sourceExpression,elementTypeName,enumeratorTypeName,useEnumerableInterface,number);
    }

    private bool TryBuildKnownEnumerableRootFromCollectionExpression(
        SemanticModel semanticModel,
        CollectionExpressionSyntax collectionExpression,
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Enumerable fragment,
        List<string>? unsafeAccessorCollector,
        IReadOnlyDictionary<string,string>? replacementMap) {
        fragment=null!;
        if(!TryGetCollectionExpressionElementType(method.ReturnType,out var elementType)) {
            return false;
        }

        var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if(collectionExpression.Elements.Count==0) {
            fragment=new EmptyRoot(uniqueNumber++,elementTypeName);
            return true;
        }

        if(collectionExpression.Elements.Count==1
            &&collectionExpression.Elements[0] is ExpressionElementSyntax { Expression: var elementExpression }) {
            fragment=new RepeatRoot(
                uniqueNumber++,
                RenderExpressionForGeneratedMethod(elementExpression,semanticModel,containingType,unsafeAccessorCollector,replacementMap),
                "1",
                elementTypeName);
            return true;
        }

        return false;
    }

    private bool TryGetCollectionExpressionElementType(ITypeSymbol type,out ITypeSymbol elementType) {
        switch(type) {
            case IArrayTypeSymbol arrayType:
                elementType=arrayType.ElementType;
                return true;
            case INamedTypeSymbol namedType when IsIEnumerableType(namedType)&&namedType.TypeArguments.Length==1:
                elementType=namedType.TypeArguments[0];
                return true;
            case INamedTypeSymbol namedType:
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(candidate => IsIEnumerableType(candidate)&&candidate.TypeArguments.Length==1);
                if(enumerableInterface is not null) {
                    elementType=enumerableInterface.TypeArguments[0];
                    return true;
                }
                break;
        }

        elementType=null!;
        return false;
    }

    private bool TryGetSingleExpressionBody(MethodDeclarationSyntax methodSyntax,out ExpressionSyntax expression) {
        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }) {
            expression=expressionBody;
            return true;
        }

        if(methodSyntax.Body is { } bodySyntax
            &&bodySyntax.Statements.Count==1
            &&bodySyntax.Statements[0] is ReturnStatementSyntax { Expression: { } returnExpression }) {
            expression=returnExpression;
            return true;
        }

        expression=null!;
        return false;
    }

    private Dictionary<string,string> BuildInlineEnumerableRootReplacementMap(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax methodSyntax,
        ExpressionSyntax bodyExpression,
        SemanticModel bodySemanticModel,
        SemanticModel invocationSemanticModel,
        INamedTypeSymbol invocationContainingType,
        IReadOnlyList<string> actualArguments,
        List<string>? unsafeAccessorCollector) {
        var replacementMap = new Dictionary<string,string>(StringComparer.Ordinal);
        for(var i = 0;i<method.Parameters.Length&&i<actualArguments.Count;i++) {
            replacementMap[method.Parameters[i].Name]=actualArguments[i];
        }

        var receiverText = GetInlineEnumerableRootReceiverText(
            invocation,
            invocationSemanticModel,
            invocationContainingType,
            unsafeAccessorCollector);
        foreach(var identifier in bodyExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            if(identifier.Parent is MemberAccessExpressionSyntax { Name: var name }&&name==identifier) {
                continue;
            }

            if(bodySemanticModel.GetSymbolInfo(identifier).Symbol is IFieldSymbol field
                &&!field.IsStatic
                &&!CanAccessFromGeneratedCode(field)) {
                replacementMap[identifier.Identifier.ValueText]=RenderFieldAccessorCall(field,receiverText,unsafeAccessorCollector);
            }
        }

        foreach(var memberAccess in bodyExpression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()) {
            if(bodySemanticModel.GetSymbolInfo(memberAccess).Symbol is IFieldSymbol field
                &&!field.IsStatic
                &&!CanAccessFromGeneratedCode(field)) {
                var memberText = NormalizeExpressionText(memberAccess.ToString());
                replacementMap[memberText]=RenderFieldAccessorCall(field,receiverText,unsafeAccessorCollector);
            }
        }

        return replacementMap;
    }

    private string GetInlineEnumerableRootReceiverText(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string>? unsafeAccessorCollector) {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,containingType,unsafeAccessorCollector)
            : "@this";
    }

    private string RenderFieldAccessorCall(IFieldSymbol field,string receiverText,List<string>? unsafeAccessorCollector) {
        var accessorName = $"__Field_{SanitizeIdentifier(field.ContainingType.Name)}_{SanitizeIdentifier(field.Name)}";
        var targetTypeName = field.ContainingType.ToDisplayString(完全修飾形式);
        var fieldTypeName = field.Type.ToDisplayString(完全修飾形式);
        var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = \"{field.Name}\")]\nprivate extern ref {fieldTypeName} {accessorName}({targetTypeName} @this);";
        unsafeAccessorCollector?.Add(accessorCode);
        return $"{accessorName}({receiverText})";
    }

    // ブロックが「単一の値付き return 文だけ」かどうか。これに該当するものは式インライン（LambdaFunc）で扱う。
    private bool IsSingleReturnBlock(BlockSyntax block) =>
        block.Statements.Count==1&&block.Statements[0] is ReturnStatementSyntax { Expression: not null };

    /// <summary><c>delegate(params) {}</c> のパラメータ名リストを返す。引数リストがない場合は空リスト。</summary>
    private List<string> GetAnonymousMethodParameters(AnonymousMethodExpressionSyntax anonymousMethod) =>
        anonymousMethod.ParameterList?.Parameters
            .Select(parameter => parameter.Identifier.ValueText)
            .ToList()??new List<string>();

    // 複数文ブロック本体（try-catch 含む）を文インライン展開する BlockFunc を構築する。
    // return 文は __Tsavorite_DEST__ への代入 + goto __Tsavorite_LABEL__ へ書き換える。
    private bool TryBuildBlockFunc(
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
        var bodyAccessorCodes = new List<string>();
        var rewritten = (BlockSyntax)new ReturnToResultRewriter(semanticModel,containingType,bodyAccessorCodes).Visit(block)!;
        var bodyText = string.Join(
            "\n",
            rewritten.Statements.Select(statement => semanticModel is not null&&containingType is not null
                ? RenderStatementForGeneratedMethod(statement,semanticModel,containingType,bodyAccessorCodes)
                : NormalizeGeneratedExpressionText(statement.ToString())));

        var capturedLocals = Array.Empty<(string Name,string TypeName)>();
        if(semanticModel is not null&&block.Statements.Count>0) {
            var dataFlow = semanticModel.AnalyzeDataFlow(block.Statements[0],block.Statements[block.Statements.Count-1]);
            if(dataFlow?.Succeeded==true) {
                var paramSet = new HashSet<string>(parameters,StringComparer.Ordinal);
                capturedLocals=dataFlow.DataFlowsIn
                    .Where(sym => (sym is ILocalSymbol||sym is IParameterSymbol)&&!paramSet.Contains(sym.Name)&&sym.Name!="this")
                    .Select(sym => (sym.Name,sym is ILocalSymbol loc
                        ? loc.Type.ToDisplayString(完全修飾形式)
                        : ((IParameterSymbol)sym).Type.ToDisplayString(完全修飾形式)))
                    .Distinct()
                    .ToArray();
            }
        }

        fragment=new BlockFunc {
            ReturnTypeName=typeArguments[typeArguments.Length-1],
            ParameterTypeNames=parameterTypeNames,
            ParameterNames=parameters.ToArray(),
            CanGenerateMethod=false,
            CanInlineAssign=true,
            RequiresStatementInlining=true,
            BlockBody=bodyText,
            CapturedLocals=capturedLocals,
            BodyUnsafeAccessorCodes=bodyAccessorCodes
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };
        return true;
    }

    // ブロック本体内の return 文を __Tsavorite_DEST__ = (expr); goto __Tsavorite_LABEL__; へ書き換える。
    // ネストした lambda / 匿名メソッド / local function の return は対象外（中へ降りない）。
    private sealed class ReturnToResultRewriter(
        SemanticModel? semanticModel,
        INamedTypeSymbol? containingType,
        List<string>? unsafeAccessorCollector):CSharpSyntaxRewriter {
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;
        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;
        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => node;
        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => node;
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
            => this.TryRewriteGeneratedExpression(node,out var rewritten)
                ? rewritten
                : base.VisitInvocationExpression(node);
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            => this.TryRewriteGeneratedExpression(node,out var rewritten)
                ? rewritten
                : base.VisitMemberAccessExpression(node);
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
            => this.TryRewriteGeneratedExpression(node,out var rewritten)
                ? rewritten
                : base.VisitIdentifierName(node);
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node) {
            var returnExpression = node.Expression is { } expr
                ? semanticModel is not null&&containingType is not null
                    ? RenderExpressionForGeneratedMethod(expr,semanticModel,containingType,unsafeAccessorCollector)
                    : NormalizeGeneratedExpressionText(expr.ToString())
                : null;
            var statementText = returnExpression is not null
                ? $"{{ __Tsavorite_DEST__ = ({returnExpression}); goto __Tsavorite_LABEL__; }}"
                : "{ goto __Tsavorite_LABEL__; }";
            return SyntaxFactory.ParseStatement(statementText);
        }

        private bool TryRewriteGeneratedExpression(ExpressionSyntax expression,out SyntaxNode? rewritten) {
            rewritten=null;
            if(semanticModel is null
                ||containingType is null
                ||!ReferenceEquals(expression.SyntaxTree,semanticModel.SyntaxTree)) {
                return false;
            }

            var rendered = RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessorCollector);
            if(rendered==NormalizeGeneratedExpressionText(expression.ToString())) {
                return false;
            }

            rewritten=SyntaxFactory.ParseExpression(rendered).WithTriviaFrom(expression);
            return true;
        }
    }

    /// <summary>
    /// source generator の生成コードを検証する。
    /// 本体が式本体か単一 <c>return</c> 文の場合のみ成功する。
    /// source generator の生成コードを検証する。
    /// </summary>
    private bool TryCreateLambdaFuncFragment(LambdaExpressionSyntax lambda,ITypeSymbol delegateType,out LambdaSpec fragment) {
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
            typeArguments[typeArguments.Length-1],
            parameterTypeNames,
            parameters.ToArray(),
            NormalizeExpressionText(body.ToString()));
        return true;
    }

    /// <summary>
    /// <c>delegate(params) { return expr; }</c> の単一 return 本体から <see cref="LambdaSpec"/> を抽出する。
    /// 複数文ブロックや戻り値なし本体は失敗する（<see cref="TryBuildBlockFunc"/> の対象になる）。
    /// </summary>
    private bool TryCreateAnonymousMethodFuncFragment(
        AnonymousMethodExpressionSyntax anonymousMethod,
        ITypeSymbol delegateType,
        out LambdaSpec fragment,
        out ExpressionSyntax body) {
        fragment=null!;
        body=null!;

        if(anonymousMethod.Block.Statements.Count!=1
            ||anonymousMethod.Block.Statements[0] is not ReturnStatementSyntax { Expression: { } returnExpression }) {
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
            .Select(parameter => parameter.Identifier.ValueText)
            .ToArray()??new string[0];
        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Length!=parameterTypeNames.Length) {
            return false;
        }

        body=returnExpression;
        fragment=new LambdaSpec(
            typeArguments[typeArguments.Length-1],
            parameterTypeNames,
            parameters,
            NormalizeExpressionText(returnExpression.ToString()));
        return true;
    }

    internal static bool IsInlineDelegateType(INamedTypeSymbol namedType) {
        // SelectMany では selector が IEnumerable<T> を返すため、
        // 戻り値がシーケンスでも FuncFragment 化できるようにしておく。
        // 実際にどの Fragment で受け入れるかは後段の候補選択側で決まる。
        return namedType.ContainingNamespace.ToDisplayString()=="System"
            &&namedType.MetadataName is "Func`2" or "Func`3";
    }

    /// <summary>型が <c>System.Collections.Generic.IEnumerable&lt;T&gt;</c> そのものかを判定する。</summary>
    internal static bool IsIEnumerableType(INamedTypeSymbol namedType)
       => namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            &&namedType.MetadataName=="IEnumerable`1";

    /// <summary>型またはその実装インターフェースに非ジェネリック <c>System.Collections.IEnumerable</c> が含まれるかを判定する。</summary>
    internal static bool IsNonGenericIEnumerableType(ITypeSymbol? type)
       => type is INamedTypeSymbol namedType
            &&(IsNonGenericIEnumerableType(namedType)
                ||namedType.AllInterfaces.Any(IsNonGenericIEnumerableType));

    /// <summary>型が <c>System.Collections.IEnumerable</c>（非ジェネリック）そのものかを判定する。</summary>
    internal static bool IsNonGenericIEnumerableType(INamedTypeSymbol namedType)
       => namedType.ContainingNamespace.ToDisplayString()=="System.Collections"
            &&namedType.MetadataName=="IEnumerable";

    internal static bool ContainsAnonymousType(ITypeSymbol type) {
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
    private string ReplaceAnonymousTypesWithTResult(ITypeSymbol type) {
        if(type.IsAnonymousType) return "TResult";
        if(type is INamedTypeSymbol named&&named.TypeArguments.Any(ContainsAnonymousType)) {
            var openName = OpenGenericTypeName(named);
            var args = string.Join(", ",named.TypeArguments.Select(a =>
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

    // メソッド自身の型パラメーター（ownParams）以外の開いた型パラメーターが型に含まれるか検査する。
    // メソッドレベルのジェネリックパラメーターのみを持つ型は生成シグネチャに出せるため通過させる。
    private static bool ContainsExternalTypeParameter(ITypeSymbol type,ImmutableArray<ITypeParameterSymbol> ownParams) {
        if(ownParams.IsDefaultOrEmpty) {
            return ContainsTypeParameter(type);
        }
        var ownNames = new HashSet<string>(ownParams.Select(p => p.Name),StringComparer.Ordinal);
        return ContainsExternalTypeParameterCore(type,ownNames);
    }

    private static bool ContainsExternalTypeParameterCore(ITypeSymbol type,HashSet<string> ownNames) => type switch {
        ITypeParameterSymbol tp => !ownNames.Contains(tp.Name),
        IArrayTypeSymbol arr => ContainsExternalTypeParameterCore(arr.ElementType,ownNames),
        INamedTypeSymbol named => named.TypeArguments.Any(a => ContainsExternalTypeParameterCore(a,ownNames)),
        _ => false,
    };

    internal static bool ContainsGeneratedTypeAccessibilityRisk(ITypeSymbol type) {
        // 生成クラスから見えない型がどこかに混ざると署名生成時点でコンパイル不能になる。
        return type switch {
            IArrayTypeSymbol arrayType => ContainsGeneratedTypeAccessibilityRisk(arrayType.ElementType),
            IPointerTypeSymbol pointerType => ContainsGeneratedTypeAccessibilityRisk(pointerType.PointedAtType),
            INamedTypeSymbol namedType => NamedTypeAccessibilityRisk(namedType)||namedType.TypeArguments.Any(ContainsGeneratedTypeAccessibilityRisk),
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

        return namedType.ContainingType is not null&&NamedTypeAccessibilityRisk(namedType.ContainingType);
    }

    /// <summary>
    /// ラムダの本体を式として取り出す。
    /// 式本体ラムダ（<c>x =&gt; expr</c>）はそのまま、単一 <c>return</c> 文ブロック（<c>x =&gt; { return expr; }</c>）は
    /// return の式を返す。それ以外は false。
    /// </summary>
    private bool TryGetLambdaExpressionBody(LambdaExpressionSyntax lambda,out ExpressionSyntax body) {
        body=null!;
        if(lambda.Body is ExpressionSyntax expressionBody) {
            body=expressionBody;
            return true;
        }
        var blockSyntax=lambda.Body as BlockSyntax;
        if(blockSyntax is not null) {
            var statements=blockSyntax.Statements.ToArray();
            if(statements.Length==1
                &&statements.First() is ReturnStatementSyntax { Expression: { } returnExpression }) {
                body=returnExpression;
                return true;
            }
        }
        return false;
    }
    /// <summary>ラムダ式のパラメータ名リストを返す。単純ラムダは 1 要素、括弧ラムダは全パラメータ名。</summary>
    private List<string> GetLambdaParameters(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simpleLambda) {
            return new List<string> { simpleLambda.Parameter.Identifier.ValueText };
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda) {
            return parenthesizedLambda.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText).ToList();
        }
        return new List<string>();
    }
}
}
