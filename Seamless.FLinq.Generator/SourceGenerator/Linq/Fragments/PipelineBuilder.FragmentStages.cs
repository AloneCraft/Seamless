namespace Seamless.FLinq.Generator;

// 既存 Fragment へ委譲する段の構築。
internal static partial class PipelineBuilder {
    private static bool TryAppendFragmentBackedStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        ref Enumerable sourceFragment,
        ref string sourceStaticMethods,
        List<PipelineStage> stages,
        ref string currentElementType,
        List<string>? unsafeAccessorCollector) {
        var methodName = (method.ReducedFrom??method).Name;
        if(methodName is not ("Chunk" or "Zip" or "AggregateBy" or "CountBy" or "Join" or "GroupJoin")) {
            return false;
        }
        if(stages.Count>0) {
            if(!ValidateStageCombination(stages,null,allowSingleStage: true)) {
                return false;
            }
            var pipeline = new PipelineEnumerable(uniqueNumber++,sourceFragment,stages.ToArray(),currentElementType);
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,pipeline.StructMethod);
            sourceFragment=pipeline;
            stages.Clear();
        }
        if(!TryBuildFragmentBackedStage(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out var nextSource,out var nextStaticMethods,ref currentElementType)) {
            throw Unsupported($"method '{methodName}' could not be built as a Fragment-backed Chain IR stage.");
        }
        sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,nextStaticMethods);
        sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,nextSource.StructMethod);
        sourceFragment=nextSource;
        return true;
    }

    private static bool TryBuildFragmentBackedStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        nextSource=null!;
        staticMethods=string.Empty;
        var methodName = (method.ReducedFrom??method).Name;
        switch(methodName) {
            case "Chunk":
                return TryBuildChunkFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "Zip":
                return TryBuildZipFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
            case "AggregateBy":
                return TryBuildAggregateByFragment(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "CountBy":
                return TryBuildCountByFragment(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "Index":
                return TryBuildIndexFragment(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "Join":
                return TryBuildJoinFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
            case "GroupJoin":
                return TryBuildGroupJoinFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
            default:
                return false;
        }
    }

    private static bool TryBuildChunkFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length==0
            ||method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
            return false;
        }
        nextSource=new Chunk(uniqueNumber++,sourceFragment,services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression));
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildZipFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        nextSource=null!;
        staticMethods=string.Empty;
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length<1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out var secondStaticMethods)) {
            return false;
        }
        staticMethods=secondStaticMethods;
        if(invocation.ArgumentList.Arguments.Count==1) {
            nextSource=new Zip(uniqueNumber++,sourceFragment,secondSource);
            currentElementType=nextSource.ElementType;
            return true;
        }
        // 3-sequence Zip: Zip(second, third) — parameters[1] is IEnumerable<T3>, not Func
        if(method.Parameters.Length>=2
            &&method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 1 }) {
            if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[1].Expression,ref uniqueNumber,out var thirdSource,out var thirdStaticMethods)) {
                return false;
            }
            staticMethods=services.JoinStaticMethods(staticMethods,thirdStaticMethods);
            nextSource=new Zip3(uniqueNumber++,sourceFragment,secondSource,thirdSource);
            currentElementType=nextSource.ElementType;
            return true;
        }
        if(method.Parameters.Length<2
            ||method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out var resultSelector)
            ||!services.CanUseFunc(resultSelector)) {
            return false;
        }
        nextSource=new Zip_resultSelector(uniqueNumber++,sourceFragment,secondSource,resultSelector);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildAggregateByFragment(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=3
            ||method.Parameters.Length<3
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }
            ||method.Parameters[2].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)
            ||!services.BuildFunc(invocation.ArgumentList.Arguments[2].Expression,method.Parameters[2].Type,ref uniqueNumber,out var func)
            ||!services.CanUseFunc(func)
            ||!services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            ||services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        var resultElementTypeName = resultElementType.ToDisplayString(services.FullyQualifiedFormat);
        nextSource=new AggregateBy(uniqueNumber++,sourceFragment,keySelector,services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression),func,resultElementTypeName);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildCountByFragment(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length<1
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)
            ||!services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            ||services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        nextSource=new CountBy(uniqueNumber++,sourceFragment,keySelector);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildIndexFragment(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=0
            ||!services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            ||services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        nextSource=new Index(uniqueNumber++,sourceFragment);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildJoinFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        return TryBuildJoinLikeFragment(false,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
    }

    private static bool TryBuildGroupJoinFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        return TryBuildJoinLikeFragment(true,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
    }

    private static bool TryBuildJoinLikeFragment(
        bool groupJoin,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        nextSource=null!;
        staticMethods=string.Empty;
        if(invocation.ArgumentList.Arguments.Count is not (4 or 5)
            ||method.Parameters.Length<4
            ||method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }
            ||method.Parameters[2].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }
            ||method.Parameters[3].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var innerSource,out var innerStaticMethods)) {
            return false;
        }
        staticMethods=innerStaticMethods;
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out var outerKeySelector)
            ||!services.CanUseFunc(outerKeySelector)
            ||!services.BuildFunc(invocation.ArgumentList.Arguments[2].Expression,method.Parameters[2].Type,ref uniqueNumber,out var innerKeySelector)
            ||!services.CanUseFunc(innerKeySelector)
            ||!services.BuildFunc(invocation.ArgumentList.Arguments[3].Expression,method.Parameters[3].Type,ref uniqueNumber,out var resultSelector)
            ||!services.CanUseFunc(resultSelector)) {
            return false;
        }
        var comparer = "null";
        if(invocation.ArgumentList.Arguments.Count==5) {
            if(method.Parameters.Length<5) {
                return false;
            }
            comparer=services.RenderExpression(invocation.ArgumentList.Arguments[4].Expression);
        }
        if(groupJoin) {
            nextSource=new GroupJoin(uniqueNumber++,sourceFragment,innerSource,outerKeySelector,innerKeySelector,resultSelector,comparer);
        } else {
            nextSource=new Join(uniqueNumber++,sourceFragment,innerSource,outerKeySelector,innerKeySelector,resultSelector,comparer);
        }
        currentElementType=nextSource.ElementType;
        return true;
    }
}
