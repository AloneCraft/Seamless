namespace Seamless.FLinq.Linq.Fragments;

internal static class PipelineBuilder {
    public static bool TryBuild(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        PipelineBuildServices services,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(!TryExtract(semanticModel,invocation,out var sourceExpression,out var invocations)) {
            return false;
        }
        if(invocations.Count<1) {
            return false;
        }
        if(invocations.Count==1) {
            // 単段でも Range 上で消せる演算は Pipeline 側で受ける。
            // 単段 Where/Select 等は従来どおり Fragment 辞書経路に委ねる。
            if(semanticModel.GetSymbolInfo(invocations[0]).Symbol is not IMethodSymbol m0) {
                return false;
            }
            var n0 = (m0.ReducedFrom??m0).Name;
            if(n0 is not ("Where" or "Select" or "Append" or "Prepend" or "DefaultIfEmpty" or "Order" or "OrderDescending" or "OrderBy" or "OrderByDescending" or "Distinct" or "DistinctBy" or "Union" or "Except" or "Intersect" or "UnionBy" or "ExceptBy" or "IntersectBy")) {
                return false;
            }
        }

        var snapshot = uniqueNumber;
        if(!services.BuildEnumerable(sourceExpression,ref snapshot,out var sourceFragment,out var sourceStaticMethods)) {
            return false;
        }

        var stages = new List<PipelineStage>();
        var currentElementType = sourceFragment.ElementType;
        PipelineTerminalKind? terminalKind = null;
        string? terminalResultType = null;
        FuncFragment? terminalFunc1 = null;
        FuncFragment? terminalFunc2 = null;
        string? terminalComparer = null;
        for(var index = 0;index<invocations.Count;index++) {
            var currentInvocation = invocations[index];
            if(semanticModel.GetSymbolInfo(currentInvocation).Symbol is not IMethodSymbol method) {
                return false;
            }
            var methodName = (method.ReducedFrom??method).Name;
            var isLast = index==invocations.Count-1;
            if(TryGetTerminalKind(methodName,out var currentTerminalKind)) {
                if(!isLast) {
                    return false;
                }
                if(!TryAppendTerminalArguments(currentTerminalKind,semanticModel,currentInvocation,method,services,ref snapshot,stages,ref currentElementType,out terminalFunc1,out terminalFunc2,out terminalComparer)) {
                    return false;
                }
                terminalKind=currentTerminalKind;
                terminalResultType=semanticModel.GetTypeInfo(currentInvocation).ConvertedType?.ToDisplayString(services.FullyQualifiedFormat)
                    ??semanticModel.GetTypeInfo(currentInvocation).Type?.ToDisplayString(services.FullyQualifiedFormat)
                    ??"object";
                continue;
            }
            if(TryAppendStage(semanticModel,currentInvocation,method,services,ref snapshot,stages,ref currentElementType)) {
                continue;
            }
            if(TryAppendFragmentBackedStage(
                semanticModel,
                currentInvocation,
                method,
                services,
                ref snapshot,
                ref sourceFragment,
                ref sourceStaticMethods,
                stages,
                ref currentElementType,
                unsafeAccessorCollector)) {
                continue;
            }
            return false;
        }

        stages=NormalizeStages(stages,services.JoinStaticMethods);
        if(TryOptimizeRangeSetStages(sourceFragment,stages,ref snapshot,out var optimizedSetSource)) {
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            if(!TryCreateOptimizedFragment(
                optimizedSetSource,
                currentElementType,
                terminalKind,
                terminalResultType,
                terminalFunc1,
                terminalFunc2,
                terminalComparer,
                ref snapshot,
                out fragment)) {
                return false;
            }
            uniqueNumber=snapshot;
            fragment.PropagateOuterTypeParameters(services.OuterTypeParameters);
            fragment.PropagateOuterTypeParameters(services.OuterTypeParameters);
        staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        }
        var allowSingleStage = invocations.Count==1
            &&stages.Count==1
            &&stages[0].Kind is PipelineStageKind.Where
                or PipelineStageKind.Select
                or PipelineStageKind.Append
                or PipelineStageKind.Prepend
                or PipelineStageKind.DefaultIfEmpty;
        if(!ValidateStageCombination(stages,terminalKind,allowSingleStage)) {
            return false;
        }

        if(TryOptimizeOrderedRange(sourceFragment,stages,ref snapshot,out var optimizedSource)) {
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            if(!TryCreateOptimizedFragment(
                optimizedSource,
                currentElementType,
                terminalKind,
                terminalResultType,
                terminalFunc1,
                terminalFunc2,
                terminalComparer,
                ref snapshot,
                out fragment)) {
                return false;
            }
            uniqueNumber=snapshot;
            fragment.PropagateOuterTypeParameters(services.OuterTypeParameters);
            fragment.PropagateOuterTypeParameters(services.OuterTypeParameters);
        staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        }

        var pipeline = new PipelineEnumerable(snapshot++,sourceFragment,stages.ToArray(),currentElementType);
        AddUnsafeAccessors(stages,unsafeAccessorCollector);
        if(terminalKind is { } terminal) {
            if(terminalResultType is null) {
                return false;
            }
            if(ProviderIterateTerminal.TryCreate(snapshot,pipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer,out var providerIterateTerminal)) {
                fragment=providerIterateTerminal;
                snapshot++;
            } else {
                fragment=new PipelineTerminal(snapshot++,pipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer);
            }
        } else {
            fragment=pipeline;
        }
        uniqueNumber=snapshot;
        fragment.PropagateOuterTypeParameters(services.OuterTypeParameters);
        staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
        // 安全網: 匿名型→record struct 置換で取りこぼした生の匿名型表示（<anonymous type: ...>）が
        // 生成コードに残ると不正な C# になるため、その場合はフュージョンを諦めて従来の例外スタブへフォールバックする。
        if(staticMethods.IndexOf("<anonymous type",StringComparison.Ordinal)>=0
            ||currentElementType.IndexOf("<anonymous type",StringComparison.Ordinal)>=0) {
            return false;
        }
        return true;
    }

    private static bool TryCreateOptimizedFragment(
        Enumerable optimizedSource,
        string currentElementType,
        PipelineTerminalKind? terminalKind,
        string? terminalResultType,
        FuncFragment? terminalFunc1,
        FuncFragment? terminalFunc2,
        string? terminalComparer,
        ref int uniqueNumber,
        out Fragment fragment) {
        if(terminalKind is not { } terminal) {
            fragment=optimizedSource;
            return true;
        }
        if(terminalResultType is null) {
            fragment=null!;
            return false;
        }

        var terminalPipeline = new PipelineEnumerable(uniqueNumber++,optimizedSource,Array.Empty<PipelineStage>(),currentElementType);
        fragment=new PipelineTerminal(uniqueNumber++,terminalPipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer);
        return true;
    }

    private static bool TryOptimizeOrderedRange(
        Enumerable source,
        IReadOnlyList<PipelineStage> stages,
        ref int uniqueNumber,
        out Enumerable optimizedSource) {
        optimizedSource=null!;
        if(source is not RangeRoot range
            ||stages.Count==0
            ||!stages.All(IsIdentityOrderStage)) {
            return false;
        }

        var primaryOrders = stages
            .Where(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)
            .ToArray();
        if(primaryOrders.Length==0) {
            return false;
        }

        optimizedSource=primaryOrders[^1].Descending
            ? new DescendingRangeRoot(uniqueNumber++,range.StartExpression,range.CountExpression)
            : range;
        return true;
    }

    private static bool IsIdentityOrderStage(PipelineStage stage) {
        if(stage.Kind is not (PipelineStageKind.OrderBy
            or PipelineStageKind.OrderByDescending
            or PipelineStageKind.ThenBy
            or PipelineStageKind.ThenByDescending)) {
            return false;
        }

        if(!string.Equals(stage.Value,"null",StringComparison.Ordinal)) {
            return false;
        }

        return stage.Func is null||stage.Func.IsIdentity;
    }

    private static bool TryOptimizeRangeSetStages(
        Enumerable source,
        IReadOnlyList<PipelineStage> stages,
        ref int uniqueNumber,
        out Enumerable optimizedSource) {
        optimizedSource=null!;
        if(source is not RangeRoot range||stages.Count==0) {
            return false;
        }

        Enumerable current = range;
        var changed = false;
        foreach(var stage in stages) {
            if(IsRangeDistinctNoOp(stage,current)) {
                changed=true;
                continue;
            }

            if(current is RangeRoot unionRange
                &&IsRangeUnionNoOp(stage,unionRange)) {
                changed=true;
                continue;
            }

            if(TryCreateRangeExcept(stage,current,uniqueNumber,out var exceptRange)) {
                uniqueNumber++;
                current=exceptRange;
                changed=true;
                continue;
            }

            if(current is RangeRoot intersectRange
                &&IsRangeIntersect(stage,intersectRange,out var second)) {
                current=CreateRangeIntersection(uniqueNumber++,intersectRange,second);
                changed=true;
                continue;
            }

            return false;
        }

        optimizedSource=current;
        return changed;
    }

    private static bool IsRangeDistinctNoOp(PipelineStage stage,Enumerable current) =>
        !stage.ContainsAnchor
        &&stage.Value=="null"
        &&current.ElementType=="int"
        &&(stage.Kind==PipelineStageKind.Distinct
            ||(stage.Kind==PipelineStageKind.DistinctBy
                &&stage.Func is { IsIdentity: true }
                &&stage.KeyType==current.ElementType));

    private static bool IsRangeIntersect(PipelineStage stage,RangeRoot current,out RangeRoot second) {
        second=null!;
        if(!TryGetIdentityRangeSetStage(stage,current.ElementType,out var secondRange)
            ||stage.Kind is not (PipelineStageKind.Intersect or PipelineStageKind.IntersectBy)) {
            return false;
        }

        second=secondRange;
        return true;
    }

    private static bool IsRangeUnionNoOp(PipelineStage stage,RangeRoot current) {
        if(!TryGetIdentityRangeSetStage(stage,current.ElementType,out var second)
            ||stage.Kind is not (PipelineStageKind.Union or PipelineStageKind.UnionBy)) {
            return false;
        }

        return IsSameRange(current,second)
            ||IsOneBasedTrailingHalfRange(current,second);
    }

    private static bool TryCreateRangeExcept(PipelineStage stage,Enumerable current,int number,out RangeExceptRoot range) {
        range=null!;
        if(!TryGetIdentityRangeSetStage(stage,current.ElementType,out var second)
            ||stage.Kind is not (PipelineStageKind.Except or PipelineStageKind.ExceptBy)) {
            return false;
        }

        range=current switch {
            RangeRoot currentRange => new RangeExceptRoot(number,currentRange,[second]),
            RangeExceptRoot existing => existing.AddExclusion(number,second),
            _ => null!
        };
        return range is not null;
    }

    private static bool TryGetIdentityRangeSetStage(PipelineStage stage,string currentElementType,out RangeRoot second) {
        second=null!;
        if(stage.ContainsAnchor
            ||stage.Value!="null"
            ||currentElementType!="int"
            ||stage.Kind is not (PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy)
            ||stage.SecondSource is not RangeRoot secondRange) {
            return false;
        }

        if(stage.Kind is PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy
            &&(stage.Func is not { IsIdentity: true }||stage.KeyType!=currentElementType)) {
            return false;
        }

        second=secondRange;
        return true;
    }

    private static RangeRoot CreateRangeIntersection(int number,RangeRoot left,RangeRoot right) {
        var start = MaxExpression(left.StartExpression,right.StartExpression);
        var end = MinLongExpression(
            ExclusiveEndExpression(left.StartExpression,left.CountExpression),
            ExclusiveEndExpression(right.StartExpression,right.CountExpression));
        var count = $"(int)global::System.Math.Max(0L,{end}-(long)({start}))";
        return new RangeRoot(number,start,count);
    }

    private static string MaxExpression(string left,string right) =>
        $"global::System.Math.Max(({left}),({right}))";

    private static string MinLongExpression(string left,string right) =>
        $"global::System.Math.Min({left},{right})";

    private static string ExclusiveEndExpression(string start,string count) =>
        $"((long)({start})+({count}))";

    private static bool IsSameRange(RangeRoot left,RangeRoot right) =>
        string.Equals(NormalizeRangeExpression(left.StartExpression),NormalizeRangeExpression(right.StartExpression),StringComparison.Ordinal)
        &&string.Equals(NormalizeRangeExpression(left.CountExpression),NormalizeRangeExpression(right.CountExpression),StringComparison.Ordinal);

    private static bool IsOneBasedTrailingHalfRange(RangeRoot current,RangeRoot second) {
        var currentStart = NormalizeRangeExpression(current.StartExpression);
        if(currentStart!="1") {
            return false;
        }

        var currentCount = NormalizeRangeExpression(current.CountExpression);
        var secondStart = NormalizeRangeExpression(second.StartExpression);
        var secondCount = NormalizeRangeExpression(second.CountExpression);
        return IsHalfExpression(currentCount,secondCount)
            &&IsAddOneExpression(secondCount,secondStart);
    }

    private static bool IsHalfExpression(string sourceCount,string candidate) =>
        string.Equals(candidate,$"{sourceCount}/2",StringComparison.Ordinal)
        ||string.Equals(candidate,$"({sourceCount})/2",StringComparison.Ordinal);

    private static bool IsAddOneExpression(string source,string candidate) =>
        string.Equals(candidate,$"{source}+1",StringComparison.Ordinal)
        ||string.Equals(candidate,$"({source})+1",StringComparison.Ordinal);

    private static string NormalizeRangeExpression(string expression) =>
        new(expression.Where(static c => !char.IsWhiteSpace(c)).ToArray());

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
        if(methodName is not ("Chunk" or "Zip" or "AggregateBy" or "Join" or "GroupJoin")) {
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

    private static bool TryExtract(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out ExpressionSyntax sourceExpression,
        out List<InvocationExpressionSyntax> invocations) {
        sourceExpression=null!;
        invocations= [];
        ExpressionSyntax current = invocation;
        while(current is InvocationExpressionSyntax currentInvocation
            &&currentInvocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            if(semanticModel.GetSymbolInfo(currentInvocation).Symbol is not IMethodSymbol method
                ||method.ReducedFrom is null) {
                break;
            }
            var containingType = (method.ReducedFrom??method).ContainingType.ToDisplayString();
            if(containingType!="System.Linq.Enumerable"
                &&containingType!="Seamless.Linq.Enumerable") {
                break;
            }
            invocations.Add(currentInvocation);
            current=RemoveParentheses(receiverExpression);
        }

        if(invocations.Count==0) {
            return false;
        }
        invocations.Reverse();
        sourceExpression=RemoveParentheses(current);
        return true;
    }

    private static bool TryGetTerminalKind(string methodName,out PipelineTerminalKind terminalKind) {
        switch(methodName) {
            case "ToList":
                terminalKind=PipelineTerminalKind.ToList;
                return true;
            case "ToArray":
                terminalKind=PipelineTerminalKind.ToArray;
                return true;
            case "Count":
                terminalKind=PipelineTerminalKind.Count;
                return true;
            case "Any":
                terminalKind=PipelineTerminalKind.Any;
                return true;
            case "First":
                terminalKind=PipelineTerminalKind.First;
                return true;
            case "ToDictionary":
                terminalKind=PipelineTerminalKind.ToDictionary;
                return true;
            case "Sum":
                terminalKind=PipelineTerminalKind.Sum;
                return true;
            default:
                terminalKind=default;
                return false;
        }
    }

    private static bool TryAppendTerminalArguments(
        PipelineTerminalKind terminalKind,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType,
        out FuncFragment? func1,
        out FuncFragment? func2,
        out string? comparerValue) {
        func1=null;
        func2=null;
        comparerValue=null;
        if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray) {
            return invocation.ArgumentList.Arguments.Count==0;
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary) {
            return TryAppendToDictionaryTerminalArguments(semanticModel,invocation,method,services,ref uniqueNumber,out func1,out func2,out comparerValue);
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            // Sum(selector): add Select stage, then Sum() is applied to selected values.
            if(invocation.ArgumentList.Arguments.Count==0) {
                return true;
            }
            if(invocation.ArgumentList.Arguments.Count!=1
                ||method.Parameters.Length==0
                ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } selectorType) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var sumSelector)
                ||!services.CanUseFunc(sumSelector)) {
                return false;
            }
            var sumSelectElementType = sumSelector.ReturnTypeName;
            stages.Add(new PipelineStage(PipelineStageKind.Select,sumSelector,null,currentElementType,sumSelectElementType));
            currentElementType=sumSelectElementType;
            return true;
        }
        if(invocation.ArgumentList.Arguments.Count==0) {
            return true;
        }
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length==0
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var predicate)
            ||!services.CanUseFunc(predicate)) {
            return false;
        }
        stages.Add(new PipelineStage(PipelineStageKind.Where,predicate,null,currentElementType,currentElementType));
        return true;
    }

    private static bool TryAppendToDictionaryTerminalArguments(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        out FuncFragment? func1,
        out FuncFragment? func2,
        out string? comparerValue) {
        func1=null;
        func2=null;
        comparerValue=null;
        if(invocation.ArgumentList.Arguments.Count==0||method.Parameters.Length==0) {
            return false;
        }
        // First arg is always keySelector Func<TSource,TKey>
        if(method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } keySelectorType) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,keySelectorType,ref uniqueNumber,out func1)
            ||!services.CanUseFunc(func1)) {
            return false;
        }
        if(invocation.ArgumentList.Arguments.Count==1) {
            return true;
        }
        // Second arg: elementSelector Func<TSource,TElement> or IEqualityComparer<TKey>
        if(invocation.ArgumentList.Arguments.Count>=2&&method.Parameters.Length>=2) {
            if(method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 } elemSelectorType) {
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,elemSelectorType,ref uniqueNumber,out func2)
                    ||!services.CanUseFunc(func2)) {
                    return false;
                }
                if(invocation.ArgumentList.Arguments.Count==3&&method.Parameters.Length>=3) {
                    comparerValue=services.RenderExpression(invocation.ArgumentList.Arguments[2].Expression);
                }
                return true;
            }
            // Second arg is comparer
            comparerValue=services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression);
            return true;
        }
        return true;
    }

    private static bool TryAppendStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        var methodName = (method.ReducedFrom??method).Name;
        switch(methodName) {
            case "Where":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } predicateType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,predicateType,ref uniqueNumber,out var predicate)) {
                    return false;
                }
                if(!services.CanUseFunc(predicate)) {
                    return false;
                }
                stages.Add(new PipelineStage(PipelineStageKind.Where,predicate,null,currentElementType,currentElementType,usesIndex: predicateType.TypeArguments.Length==3));
                return true;
            case "Select":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } selectorType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var selector)) {
                    return false;
                }
                if(!services.CanUseFunc(selector)) {
                    return false;
                }
                var selectElementType = selector.ReturnTypeName;
                stages.Add(new PipelineStage(PipelineStageKind.Select,selector,null,currentElementType,selectElementType,usesIndex: selectorType.TypeArguments.Length==3));
                currentElementType=selectElementType;
                return true;
            case "SelectMany":
                return TryAppendSelectManyStage(semanticModel,invocation,method,services,ref uniqueNumber,stages,ref currentElementType);
            case "Distinct":
                return TryAppendDistinctStage(invocation,method,services,stages,currentElementType);
            case "DistinctBy":
                return TryAppendDistinctByStage(invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Union":
                return TryAppendBinarySetStage(PipelineStageKind.Union,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Except":
                return TryAppendBinarySetStage(PipelineStageKind.Except,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Intersect":
                return TryAppendBinarySetStage(PipelineStageKind.Intersect,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "UnionBy":
                return TryAppendBinarySetByStage(PipelineStageKind.UnionBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ExceptBy":
                return TryAppendBinarySetByStage(PipelineStageKind.ExceptBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "IntersectBy":
                return TryAppendBinarySetByStage(PipelineStageKind.IntersectBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Concat":
                return TryAppendConcatStage(invocation,services,ref uniqueNumber,stages,currentElementType);
            case "Append":
                return TryAppendElementStage(PipelineStageKind.Append,invocation,services,stages,currentElementType);
            case "Prepend":
                return TryAppendElementStage(PipelineStageKind.Prepend,invocation,services,stages,currentElementType);
            case "OrderBy":
                return TryAppendOrderStage(PipelineStageKind.OrderBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "OrderByDescending":
                return TryAppendOrderStage(PipelineStageKind.OrderByDescending,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ThenBy":
                return TryAppendOrderStage(PipelineStageKind.ThenBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ThenByDescending":
                return TryAppendOrderStage(PipelineStageKind.ThenByDescending,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "GroupBy":
                return TryAppendGroupByStage(invocation,method,services,ref uniqueNumber,stages,ref currentElementType);
            case "Reverse":
                stages.Add(new PipelineStage(PipelineStageKind.Reverse,null,null,currentElementType,currentElementType));
                return true;
            case "DefaultIfEmpty":
                return TryAppendDefaultIfEmptyStage(invocation,services,stages,currentElementType);
            case "AsEnumerable":
                stages.Add(new PipelineStage(PipelineStageKind.AsEnumerable,null,null,currentElementType,currentElementType));
                return true;
            case "Skip":
                return TryAppendCountStage(PipelineStageKind.Skip,invocation,method,services,stages,currentElementType);
            case "TakeWhile":
                return TryAppendPredicateStage(PipelineStageKind.TakeWhile,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "SkipWhile":
                return TryAppendPredicateStage(PipelineStageKind.SkipWhile,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Cast":
                return TryAppendTypeStage(PipelineStageKind.Cast,method,services,stages,ref currentElementType);
            case "OfType":
                return TryAppendTypeStage(PipelineStageKind.OfType,method,services,stages,ref currentElementType);
            case "Order":
            case "OrderDescending": {
                var descending = methodName=="OrderDescending";
                var kind = descending ? PipelineStageKind.OrderByDescending : PipelineStageKind.OrderBy;
                var comparer = "null";
                var containsAnchor = false;
                if(invocation.ArgumentList.Arguments.Count==1
                    &&method.Parameters.Length>=1) {
                    var comparerExpression = invocation.ArgumentList.Arguments[0].Expression;
                    comparer=services.RenderExpression(comparerExpression);
                    containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
                } else if(invocation.ArgumentList.Arguments.Count>1) {
                    return false;
                }
                // Func=null signals identity key (element itself)
                stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,keyType: currentElementType,descending: descending,containsAnchor: containsAnchor));
                return true;
            }
            case "SkipLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                var skipLastExpression = invocation.ArgumentList.Arguments[0].Expression;
                stages.Add(new PipelineStage(PipelineStageKind.SkipLast,null,services.RenderExpression(skipLastExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(skipLastExpression,null)));
                return true;
            case "TakeLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                var takeLastExpression = invocation.ArgumentList.Arguments[0].Expression;
                stages.Add(new PipelineStage(PipelineStageKind.TakeLast,null,services.RenderExpression(takeLastExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(takeLastExpression,null)));
                return true;
            case "Take":
                if(invocation.ArgumentList.Arguments.Count!=1||method.Parameters.Length==0) {
                    return false;
                }
                if(method.Parameters[0].Type.SpecialType==SpecialType.System_Int32) {
                    return TryAppendCountStage(PipelineStageKind.Take,invocation,method,services,stages,currentElementType);
                }
                // Range overload
                if(method.Parameters[0].Type.ToDisplayString()=="global::System.Range"
                    ||method.Parameters[0].Type.ToDisplayString()=="System.Range") {
                    var takeRangeExpression = invocation.ArgumentList.Arguments[0].Expression;
                    stages.Add(new PipelineStage(PipelineStageKind.TakeRange,null,services.RenderExpression(takeRangeExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(takeRangeExpression,null)));
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static bool TryAppendCountStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length==0
            ||method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
            return false;
        }
        var countExpression = invocation.ArgumentList.Arguments[0].Expression;
        var count = services.RenderExpression(countExpression);
        stages.Add(new PipelineStage(kind,null,count,currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(countExpression,null)));
        return true;
    }

    private static bool TryAppendPredicateStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length==0
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } predicateType) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,predicateType,ref uniqueNumber,out var predicate)
            ||!services.CanUseFunc(predicate)) {
            return false;
        }
        stages.Add(new PipelineStage(kind,predicate,null,currentElementType,currentElementType,usesIndex: predicateType.TypeArguments.Length==3));
        return true;
    }

    private static bool TryAppendTypeStage(
        PipelineStageKind kind,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        ref string currentElementType) {
        if(!services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            ||services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        var outputType = resultElementType.ToDisplayString(services.FullyQualifiedFormat);
        stages.Add(new PipelineStage(kind,null,null,currentElementType,outputType));
        currentElementType=outputType;
        return true;
    }

    private static bool TryAppendSelectManyStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        // 匿名型要素は型名を生成コードに書けず ToList 等の終端で破綻するため、
        // SelectMany インライン化（record struct 置換 + 出口 Select）のフォールバック経路へ委ねる。
        if(method.TypeArguments.Any(services.ContainsAnonymousType)) {
            return false;
        }
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length!=invocation.ArgumentList.Arguments.Count
            ||method.Parameters[0].Type is not INamedTypeSymbol collectionSelectorType) {
            return false;
        }
        var typeArgumentLength = collectionSelectorType.TypeArguments.Length;
        if(typeArgumentLength is not (2 or 3)) {
            return false;
        }
        var usesIndex = typeArgumentLength==3;
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var collectionSelector)
            ||!services.CanUseFunc(collectionSelector)
            ||collectionSelector.RequiresStatementInlining) {
            return false;
        }
        if(!TryGetSequenceElementType(collectionSelector.ReturnTypeName,out var innerElementType)) {
            return false;
        }
        var innerIsArray = invocation.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax { Body: ExpressionSyntax collectionSelectorBodyExpr }
            &&semanticModel.GetTypeInfo(collectionSelectorBodyExpr).Type is IArrayTypeSymbol;

        FuncFragment? resultSelector = null;
        var outputType = innerElementType;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out resultSelector)
                ||!services.CanUseFunc(resultSelector)) {
                return false;
            }
            outputType=resultSelector.ReturnTypeName;
        }

        stages.Add(new PipelineStage(
            PipelineStageKind.SelectMany,
            collectionSelector,
            null,
            currentElementType,
            outputType,
            secondFunc: resultSelector,
            innerElementType: innerElementType,
            keyType: null,
            usesIndex: usesIndex,
            innerIsArray: innerIsArray));
        currentElementType=outputType;
        return true;
    }

    private static bool TryGetSequenceElementType(string sequenceTypeName,out string elementTypeName) {
        elementTypeName=string.Empty;
        if(sequenceTypeName.EndsWith("[]",StringComparison.Ordinal)) {
            elementTypeName=sequenceTypeName[..^2];
            return true;
        }
        const string enumerablePrefix = "global::System.Collections.Generic.IEnumerable<";
        if(sequenceTypeName.StartsWith(enumerablePrefix,StringComparison.Ordinal)
            &&sequenceTypeName.EndsWith(">",StringComparison.Ordinal)) {
            elementTypeName=sequenceTypeName[enumerablePrefix.Length..^1];
            return true;
        }
        return false;
    }

    private static bool TryAppendDistinctStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count>1) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==1) {
            if(method.Parameters.Length<1) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[0].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.Distinct,null,comparer,currentElementType,currentElementType,keyType: currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendDistinctByStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length<1
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DistinctBy,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendBinarySetStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length<1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendBinarySetByStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (2 or 3)
            ||method.Parameters.Length<2
            ||method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==3) {
            if(method.Parameters.Length<3) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[2].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: keySelector.ReturnTypeName,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendConcatStage(
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        stages.Add(new PipelineStage(PipelineStageKind.Concat,null,null,currentElementType,currentElementType,secondSource: secondSource));
        return true;
    }

    private static bool TryAppendElementStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }
        var elementExpression = invocation.ArgumentList.Arguments[0].Expression;
        var element = services.RenderExpression(elementExpression);
        stages.Add(new PipelineStage(kind,null,element,currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(elementExpression,null)));
        return true;
    }

    private static bool TryAppendDefaultIfEmptyStage(
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count>1) {
            return false;
        }
        var defaultValue = "default!";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==1) {
            var defaultValueExpression = invocation.ArgumentList.Arguments[0].Expression;
            defaultValue=services.RenderExpression(defaultValueExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(defaultValueExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DefaultIfEmpty,null,defaultValue,currentElementType,currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendOrderStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length<1
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
            &&!stages.Any(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        var descending = kind is PipelineStageKind.OrderByDescending or PipelineStageKind.ThenByDescending;
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName,descending: descending,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendGroupByStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is <1 or >4
            ||method.Parameters.Length<1
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        FuncFragment? elementSelector = null;
        FuncFragment? resultSelector = null;
        var comparer = "null";
        var containsAnchor = false;
        for(var index = 1;index<invocation.ArgumentList.Arguments.Count;index++) {
            if(method.Parameters.Length<=index) {
                return false;
            }
            if(method.Parameters[index].Type is INamedTypeSymbol { TypeArguments.Length: 2 } elementFuncType) {
                if(elementSelector is not null) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[index].Expression,elementFuncType,ref uniqueNumber,out elementSelector)
                    ||!services.CanUseFunc(elementSelector)) {
                    return false;
                }
            } else if(method.Parameters[index].Type is INamedTypeSymbol { TypeArguments.Length: 3 } resultFuncType) {
                if(resultSelector is not null) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[index].Expression,resultFuncType,ref uniqueNumber,out resultSelector)
                    ||!services.CanUseFunc(resultSelector)) {
                    return false;
                }
            } else {
                var comparerExpression = invocation.ArgumentList.Arguments[index].Expression;
                comparer=services.RenderExpression(comparerExpression);
                containsAnchor|=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
            }
        }
        var groupElementType = elementSelector?.ReturnTypeName??currentElementType;
        var outputType = resultSelector?.ReturnTypeName??$"global::System.Linq.IGrouping<{keySelector.ReturnTypeName},{groupElementType}>";
        stages.Add(new PipelineStage(
            PipelineStageKind.GroupBy,
            keySelector,
            comparer,
            currentElementType,
            outputType,
            secondFunc: elementSelector,
            thirdFunc: resultSelector,
            innerElementType: groupElementType,
            keyType: keySelector.ReturnTypeName,
            containsAnchor: containsAnchor));
        currentElementType=outputType;
        return true;
    }

    private static List<PipelineStage> NormalizeStages(List<PipelineStage> stages,Func<string,string,string> joinStaticMethods) {
        if(stages.Count<=1) {
            return stages;
        }
        stages=MergeDistinctStages(stages);
        stages=MergeDistinctByStages(stages);
        stages=MergeTakeStages(stages);
        stages=MergeSkipStages(stages);
        stages=CancelReverseStages(stages);
        stages=MergeWhereStages(stages,joinStaticMethods);
        return stages;
    }

    private static List<PipelineStage> MergeDistinctStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Distinct
                &&!stage.ContainsAnchor
                &&normalized.Count>0
                &&normalized[^1].Kind==PipelineStageKind.Distinct
                &&!normalized[^1].ContainsAnchor
                &&string.Equals(normalized[^1].Value,stage.Value,StringComparison.Ordinal)) {
                // 同じ Distinct の連続は結果集合が変わらないので、生成前に 1 段へ畳む。
                // ここで runtime の分岐を増やさず、set の再構築も避ける。
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> MergeDistinctByStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.DistinctBy
                &&!stage.ContainsAnchor
                &&normalized.Count>0
                &&normalized[^1].Kind==PipelineStageKind.DistinctBy
                &&!normalized[^1].ContainsAnchor
                &&string.Equals(normalized[^1].Value,stage.Value,StringComparison.Ordinal)
                &&normalized[^1].KeyType==stage.KeyType
                &&normalized[^1].Func is { } leftFunc
                &&stage.Func is { } rightFunc
                &&DistinctByKeySelectorEquals(leftFunc,rightFunc)) {
                // 同じ keySelector/comparer の DistinctBy も冪等なので、ここで重ねない。
                // key 投影と HashSet 構築の重複を防ぐための compile-time 正規化。
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static bool DistinctByKeySelectorEquals(FuncFragment left,FuncFragment right) {
        if(left.RequiresStatementInlining
            ||right.RequiresStatementInlining
            ||left.ContainsAnchor
            ||right.ContainsAnchor
            ||left.ParameterNames.Length!=right.ParameterNames.Length
            ||left.ParameterTypeNames.Length!=right.ParameterTypeNames.Length
            ||left.ReturnTypeName!=right.ReturnTypeName) {
            return false;
        }
        for(var index = 0;index<left.ParameterTypeNames.Length;index++) {
            if(left.ParameterTypeNames[index]!=right.ParameterTypeNames[index]) {
                return false;
            }
        }
        try {
            return string.Equals(left.RenderAsLambda(),right.RenderAsLambda(),StringComparison.Ordinal);
        } catch(InvalidOperationException) {
            return false;
        }
    }

    private static List<PipelineStage> MergeTakeStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Take
                &&!stage.ContainsAnchor
                &&normalized.Count>0
                &&normalized[^1].Kind==PipelineStageKind.Take
                &&!normalized[^1].ContainsAnchor) {
                var previous = normalized[^1];
                normalized[^1]=new PipelineStage(
                    PipelineStageKind.Take,
                    null,
                    MinCountExpression(previous.Value!,stage.Value!),
                    previous.InputType,
                    previous.OutputType);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> MergeSkipStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Skip
                &&!stage.ContainsAnchor
                &&normalized.Count>0
                &&normalized[^1].Kind==PipelineStageKind.Skip
                &&!normalized[^1].ContainsAnchor) {
                var previous = normalized[^1];
                normalized[^1]=new PipelineStage(
                    PipelineStageKind.Skip,
                    null,
                    AddCountExpression(previous.Value!,stage.Value!),
                    previous.InputType,
                    previous.OutputType);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> CancelReverseStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Reverse
                &&normalized.Count>0
                &&normalized[^1].Kind==PipelineStageKind.Reverse) {
                normalized.RemoveAt(normalized.Count-1);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> MergeWhereStages(List<PipelineStage> stages,Func<string,string,string> joinStaticMethods) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Where
                &&!stage.UsesIndex
                &&normalized.Count>0
                &&normalized[^1].Kind==PipelineStageKind.Where
                &&!normalized[^1].UsesIndex
                &&TryMergeWhereFunc(normalized[^1].Func!,stage.Func!,joinStaticMethods,out var mergedFunc)) {
                var previous = normalized[^1];
                normalized[^1]=new PipelineStage(
                    PipelineStageKind.Where,
                    mergedFunc,
                    null,
                    previous.InputType,
                    previous.OutputType);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static bool TryMergeWhereFunc(FuncFragment left,FuncFragment right,Func<string,string,string> joinStaticMethods,out FuncFragment merged) {
        merged=null!;
        if(left.RequiresStatementInlining
            ||right.RequiresStatementInlining
            ||!left.CanInlineAssign
            ||!right.CanInlineAssign
            ||left.ParameterTypeNames.Length!=right.ParameterTypeNames.Length
            ||left.ParameterNames.Length!=right.ParameterNames.Length
            ||left.ParameterTypeNames.Length!=1
            ||left.ReturnTypeName!="bool"
            ||right.ReturnTypeName!="bool") {
            return false;
        }
        for(var index = 0;index<left.ParameterTypeNames.Length;index++) {
            if(left.ParameterTypeNames[index]!=right.ParameterTypeNames[index]) {
                return false;
            }
        }
        var parameterNames = left.ParameterNames.ToArray();
        try {
            var leftExpr = left.RenderInlineExpression(parameterNames);
            var rightExpr = right.RenderInlineExpression(parameterNames);
            merged=new LambdaFunc {
                ReturnTypeName="bool",
                ParameterTypeNames=left.ParameterTypeNames.ToArray(),
                ParameterNames=parameterNames,
                CanGenerateMethod=left.CanGenerateMethod&&right.CanGenerateMethod,
                CanInlineAssign=true,
                InlineBody=$"({leftExpr})&&({rightExpr})",
                CapturedLocals=left.CapturedLocals.Concat(right.CapturedLocals).Distinct().ToArray(),
                HoistedLocals=left.HoistedLocals.Concat(right.HoistedLocals).Distinct().ToArray(),
                BodyUnsafeAccessorCodes=left.BodyUnsafeAccessorCodes.Concat(right.BodyUnsafeAccessorCodes).Distinct().ToArray(),
                SupportingMemberSources=joinStaticMethods(left.SupportingMemberSources,right.SupportingMemberSources),
                ContainsAnchor=left.ContainsAnchor||right.ContainsAnchor
            };
            return true;
        } catch(InvalidOperationException) {
            return false;
        }
    }

    private static string MinCountExpression(string left,string right) =>
        $"global::System.Math.Min(global::System.Math.Max(0,{left}),global::System.Math.Max(0,{right}))";

    private static string AddCountExpression(string left,string right) =>
        $"global::System.Math.Max(0,{left})+global::System.Math.Max(0,{right})";

    private static bool ValidateStageCombination(IReadOnlyList<PipelineStage> stages,PipelineTerminalKind? terminalKind,bool allowSingleStage = false) {
        var selectManyStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.SelectMany);
        var binarySetStageCount = stages.Count(static stage => IsBinarySetStage(stage.Kind));
        var concatLikeStageCount = stages.Count(static stage => stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend);
        var orderStageCount = stages.Count(static stage => IsOrderStage(stage.Kind));
        var groupByStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.GroupBy);
        var reverseStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.Reverse);
        var defaultIfEmptyStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.DefaultIfEmpty);
        var skipLastStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.SkipLast);
        var takeLastStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.TakeLast);
        var takeRangeStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.TakeRange);
        var bufferingStageCount = reverseStageCount+skipLastStageCount+takeLastStageCount+takeRangeStageCount+defaultIfEmptyStageCount;
        if(selectManyStageCount>1) return false;
        if(groupByStageCount>1) return false;
        if(bufferingStageCount>1) return false;
        if(binarySetStageCount>1) return false;
        if(groupByStageCount>0&&(selectManyStageCount>0||binarySetStageCount>0||concatLikeStageCount>0)) return false;
        if(bufferingStageCount>0&&(selectManyStageCount>0||binarySetStageCount>0||concatLikeStageCount>0||orderStageCount>0||groupByStageCount>0)) return false;
        if(binarySetStageCount>0&&selectManyStageCount>0) return false;
        if(concatLikeStageCount>1) return false;
        if(concatLikeStageCount>0&&(selectManyStageCount>0||binarySetStageCount>0)) return false;
        if(orderStageCount>0&&(selectManyStageCount>0||concatLikeStageCount>0)) return false;
        if(binarySetStageCount>0&&orderStageCount>0&&!CanFuseBinarySetThenOrder(stages)) return false;
        if(orderStageCount>0) {
            var orderIndexes = stages.Select(static (stage,index) => (stage,index))
                .Where(static pair => IsOrderStage(pair.stage.Kind))
                .Select(static pair => pair.index)
                .ToArray();
            if(stages[orderIndexes[0]].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) return false;
            for(var i = 1;i<orderIndexes.Length;i++) {
                if(orderIndexes[i]!=orderIndexes[i-1]+1) return false;
            }
        }
        // 単段でも順序ステージ（Order/OrderDescending）は実バッファ＋ソートを伴うので許可する。
        if(stages.Count<2&&!allowSingleStage&&terminalKind is null&&orderStageCount==0) return false;
        return true;
    }

    private static bool CanFuseBinarySetThenOrder(IReadOnlyList<PipelineStage> stages) {
        var setIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => IsBinarySetStage(pair.stage.Kind))
            .Select(static pair => pair.index)
            .Single();
        if(stages.Take(setIndex).Any(static stage => IsOrderStage(stage.Kind))) {
            return false;
        }
        var orderIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => IsPrimaryOrderStage(pair.stage.Kind))
            .Select(static pair => pair.index)
            .FirstOrDefault(-1);
        if(orderIndex<=setIndex) {
            return false;
        }
        for(var index = setIndex+1;index<orderIndex;index++) {
            if(!CanRunBetweenBinarySetAndOrder(stages[index].Kind)) {
                return false;
            }
        }
        return true;
    }

    private static bool CanRunBetweenBinarySetAndOrder(PipelineStageKind kind) =>
        kind is PipelineStageKind.Where
            or PipelineStageKind.Select
            or PipelineStageKind.Take
            or PipelineStageKind.Skip
            or PipelineStageKind.TakeWhile
            or PipelineStageKind.SkipWhile
            or PipelineStageKind.Cast
            or PipelineStageKind.OfType
            or PipelineStageKind.Distinct
            or PipelineStageKind.DistinctBy
            or PipelineStageKind.AsEnumerable;

    private static bool IsBinarySetStage(PipelineStageKind kind) =>
        kind is PipelineStageKind.Union
            or PipelineStageKind.Except
            or PipelineStageKind.Intersect
            or PipelineStageKind.UnionBy
            or PipelineStageKind.ExceptBy
            or PipelineStageKind.IntersectBy;

    private static bool IsOrderStage(PipelineStageKind kind) =>
        kind is PipelineStageKind.OrderBy
            or PipelineStageKind.OrderByDescending
            or PipelineStageKind.ThenBy
            or PipelineStageKind.ThenByDescending;

    private static bool IsPrimaryOrderStage(PipelineStageKind kind) =>
        kind is PipelineStageKind.OrderBy
            or PipelineStageKind.OrderByDescending;

    private static void AddUnsafeAccessors(IEnumerable<PipelineStage> stages,List<string>? unsafeAccessorCollector) {
        if(unsafeAccessorCollector is null) {
            return;
        }
        foreach(var stage in stages) {
            AddFuncUnsafeAccessors(stage.Func,unsafeAccessorCollector);
            AddFuncUnsafeAccessors(stage.SecondFunc,unsafeAccessorCollector);
            AddFuncUnsafeAccessors(stage.ThirdFunc,unsafeAccessorCollector);
            if(stage.SecondSource is { } secondSource) {
                unsafeAccessorCollector.AddRange(secondSource.CollectAllUnsafeAccessorCodes());
            }
        }
    }

    private static void AddFuncUnsafeAccessors(FuncFragment? func,List<string> unsafeAccessorCollector) {
        if(func is null) {
            return;
        }
        unsafeAccessorCollector.AddRange(func.BodyUnsafeAccessorCodes);
        if(func is InlinedScalarFunc inlinedScalarFunc) {
            unsafeAccessorCollector.AddRange(inlinedScalarFunc.ScalarFragment.CollectAllUnsafeAccessorCodes());
        }
    }

    private static ExpressionSyntax RemoveParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }

    private static NotSupportedException Unsupported(string reason) =>
        new($"Chain IR is not implemented for this LINQ chain: {reason}");
}
