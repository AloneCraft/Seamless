namespace Seamless.FLinq;

internal static partial class PipelineBuilder {
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

        stages=LinqOptimizationExecutor.NormalizePipeline(stages,services.JoinStaticMethods,services.Configuration,services.Trace);
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
            services.ApplyRenderContext(fragment);
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
            services.ApplyRenderContext(fragment);
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
        services.ApplyRenderContext(fragment);
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
}
