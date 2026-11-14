namespace Seamless.FLinq.Generator;

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

        if(TryBuildRangeUnionCount(semanticModel,invocation,ref uniqueNumber,services,out fragment)){
            return true;
        }

        if(!TryExtract(semanticModel,invocation,out var sourceExpression,out var invocations)) {
            return false;
        }
        if(TryBuildSetParallelTerminal(semanticModel,sourceExpression,invocations,services,ref uniqueNumber,out fragment)
            ||TryBuildSmallSideJoinArray(semanticModel,sourceExpression,invocations,services,ref uniqueNumber,out fragment)){
            return true;
        }
        if(invocations.Count<1) {
            return false;
        }
        // 検証を保つUnion連鎖を入力とした終端は、終端一段でもここで生成する。
        var hasPreservedUnionSource=invocations.Count==1&&IsPreservedSystemUnionSource(semanticModel,sourceExpression);
        if(invocations.Count==1&&!hasPreservedUnionSource) {
            // 単段でも Range 上で消せる演算は Pipeline 側で受ける。
            // 単段 Where/Select 等は従来どおり Fragment 辞書経路に委ねる。
            if(semanticModel.GetSymbolInfo(invocations[0]).Symbol is not IMethodSymbol m0) {
                return false;
            }
            var n0 = (m0.ReducedFrom??m0).Name;
            // SelectMany(x => Enumerable.Repeat(e,1)[,…]) は Select と同じなので単段でも受ける(射影融合で後ろの Select が結果選択子へ畳まれた形を含む)。
            if(n0 is not ("WithOrder" or "Where" or "Select" or "Append" or "Prepend" or "DefaultIfEmpty" or "Order" or "OrderDescending" or "OrderBy" or "OrderByDescending" or "Distinct" or "DistinctBy" or "Union" or "Except" or "Intersect" or "UnionBy" or "ExceptBy" or "IntersectBy")
                &&!(n0=="SelectMany"&&IsSingleRepeatSelectMany(semanticModel,invocations[0]))) {
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
        string? terminalValue = null;
        string? parallelSlots=null;
        object? parallelTarget=null;
        string? orderHint=null;
        for(var index = 0;index<invocations.Count;index++) {
            var currentInvocation = invocations[index];
            if(semanticModel.GetSymbolInfo(currentInvocation).Symbol is not IMethodSymbol method) {
                return false;
            }
            var methodName = (method.ReducedFrom??method).Name;
            var isLast = index==invocations.Count-1;
            if(IsOrderHint(method,"WithOrder")){
                if(orderHint is not null){
                    throw Unsupported("Use one WithOrder hint per query chain.");
                }
                var target=HintTarget(semanticModel,currentInvocation,"Parallel");
                if(target is not {Name:"Join" or "Union"}||target.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
                    ||currentInvocation.ArgumentList.Arguments.Count!=1){
                    throw Unsupported("Place WithOrder immediately after Join or Union, optionally with Parallel.");
                }
                var argument=currentInvocation.ArgumentList.Arguments[0].Expression;
                orderHint=semanticModel.GetConstantValue(argument) switch {
                    {HasValue:true,Value:0}=>"Auto",
                    {HasValue:true,Value:1}=>"Preserve",
                    {HasValue:true,Value:2}=>"AllowUnordered",
                    _=>throw Unsupported("WithOrder requires a constant QueryOrder value.")
                };
                continue;
            }
            if(IsOrderHint(method,"Parallel")){
                if(parallelSlots is not null){
                    throw Unsupported("Use one Parallel hint per query chain.");
                }
                if(index==0||isLast||currentInvocation.ArgumentList.Arguments.Count!=1
                    ||HintTarget(semanticModel,currentInvocation,"WithOrder") is not IMethodSymbol previous){
                    throw Unsupported("Place Parallel immediately after Join, GroupJoin or GroupBy and finish the query with a supported terminal.");
                }
                if(previous.Name is "Join" or "GroupJoin"&&stages.Count==0&&sourceFragment is JoinBase){
                    parallelTarget=sourceFragment;
                }else if(previous.Name=="GroupBy"&&stages.LastOrDefault() is {Kind:PipelineStageKind.GroupBy} group){
                    group.HasParallelHint=true;
                    parallelTarget=group;
                }else{
                    throw Unsupported("Place Parallel immediately after Join, GroupJoin or GroupBy.");
                }
                var argument=currentInvocation.ArgumentList.Arguments[0].Expression;
                parallelSlots=semanticModel.GetConstantValue(argument) is {HasValue:true,Value:1}?"1":services.RenderExpression(argument);
                continue;
            }
            if(TryGetTerminalKind(methodName,out var currentTerminalKind)) {
                if(!isLast) {
                    return false;
                }
                // 取引用の専用終端が整列を重ねて扱える形は、最後の組み立てと同じ判定で許し、ここで列を切らない。
                // 終端の関数がまだ無いので、引数の無い終端だけを対象にする(関数を持つ終端は従来どおり切る)。
                var allowTerminalOrderBlocks=currentInvocation.ArgumentList.Arguments.Count==0
                    &&PipelineTerminal.IsTransactionalOrderSource(sourceFragment)
                    &&new PipelineTerminal(snapshot+1,new PipelineEnumerable(snapshot,sourceFragment,stages.ToArray(),currentElementType),currentTerminalKind,null,null,null,null).UsesTransactionalOrdering;
                if(stages.Count>0&&!ValidateStageCombination(stages,currentTerminalKind,allowSingleStage:true,allowTransactionalOrderBlocks:allowTerminalOrderBlocks)){
                    if(!FlushPipelineStages(ref snapshot,ref sourceFragment,ref sourceStaticMethods,stages,currentElementType,services,unsafeAccessorCollector)){
                        return false;
                    }
                }
                if(!TryAppendTerminalArguments(currentTerminalKind,semanticModel,currentInvocation,method,services,ref snapshot,stages,ref currentElementType,out terminalFunc1,out terminalFunc2,out terminalComparer,out terminalValue)) {
                    return false;
                }
                terminalKind=currentTerminalKind;
                terminalResultType=semanticModel.GetTypeInfo(currentInvocation).ConvertedType?.ToDisplayString(services.FullyQualifiedFormat)
                    ??semanticModel.GetTypeInfo(currentInvocation).Type?.ToDisplayString(services.FullyQualifiedFormat)
                    ??"object";
                continue;
            }
            // 取引の行と、両側が取引の行の結合の入力は切り出さない。取引用の専用終端が各整列を元の順にそのまま繋ぎ、groupの所有と資源の予約を
            // 一つの操作で扱う(切り出すと前半が一般の列になり専用終端を使えない)。専用終端が扱えない形は、最後の組み立ての
            // 検査(allowTransactionalOrderBlocks)で複数の一次整列が弾かれる。
            if(methodName is "Order" or "OrderDescending" or "OrderBy" or "OrderByDescending"
                &&!PipelineTerminal.IsTransactionalOrderSource(sourceFragment)
                &&stages.Any(static stage=>IsPrimaryOrderStage(stage.Kind))){
                // 次の一次整列は別の安定整列として残す。ThenBy と同じ sorter には畳まない。
                stages=GroupByFusionPass.Apply(stages,new TerminalDemand(null,false,false),services.Trace);
                stages=LinqOptimizationExecutor.NormalizePipeline(stages,services.JoinStaticMethods,services.Configuration,services.Trace);
                stages=StreamGroupByKeysWhenResultIgnoresGroup(sourceFragment,stages);
                stages=StripRangeNoOpPrefix(ref sourceFragment,stages,ref snapshot);
                if(!ValidateStageCombination(stages,null,allowSingleStage:true)){return false;}
                var prefix=PipelineEnumerable.CreateWithOrderSourceDemand(snapshot++,sourceFragment,stages.ToArray(),currentElementType);
                AddUnsafeAccessors(stages,unsafeAccessorCollector);
                sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,prefix.StructMethod);
                sourceFragment=prefix;
                stages.Clear();
            }
            var stagesSnapshotCount = stages.Count;
            var elementSnapshot = currentElementType;
            if(TryAppendStage(semanticModel,currentInvocation,method,services,ref snapshot,stages,ref currentElementType)) {
                if(stages.Count>stagesSnapshotCount&&stages.Count>1) {
                    // 取引の入力では整列を重ねた並びを仮に許す。専用終端が扱えるかは、終端と最後の組み立てで判定する。
                    if(!ValidateStageCombination(stages,null,allowSingleStage:true,allowTransactionalOrderBlocks:PipelineTerminal.IsTransactionalOrderSource(sourceFragment))) {
                        var newStages = stages.Skip(stagesSnapshotCount).ToList();
                        stages.RemoveRange(stagesSnapshotCount,stages.Count-stagesSnapshotCount);
                        if(!FlushPipelineStages(ref snapshot,ref sourceFragment,ref sourceStaticMethods,stages,elementSnapshot,services,unsafeAccessorCollector)) {
                            return false;
                        }
                        stages.AddRange(newStages);
                    }
                }
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

        // GroupBy の集約を融合するかは、段の並びと終端が揃ってから、下流の要求(DemandAnalysis)を見て決める。
        // 正規化は Select を併合するので、その前に行う(候補を持つ Select が併合されると候補が失われる)。
        if(parallelSlots is not null&&terminalKind is null){
            throw Unsupported("Parallel requires a supported query terminal.");
        }
        stages=GroupByFusionPass.Apply(stages,new TerminalDemand(terminalKind,terminalFunc1 is not null,false,terminalValue),services.Trace,allowDeferredArrayCounts:terminalKind is null&&sourceFragment is ArrayRoot&&stages.Count==2);
        stages=LinqOptimizationExecutor.NormalizePipeline(stages,services.JoinStaticMethods,services.Configuration,services.Trace);
        if(parallelTarget is PipelineStage){
            // 集約融合が置き換えたGroupByへ再接続し、Lookupを消すキー専用化は避ける。
            parallelTarget=stages.Find(static stage=>stage.HasParallelHint);
        }else{
            stages=StreamGroupByKeysWhenResultIgnoresGroup(sourceFragment,stages);
        }
        stages=StripRangeNoOpPrefix(ref sourceFragment,stages,ref snapshot);
        if(stages.Count==0&&terminalKind is null) {
            // 何もしない段を除いて段が残らなければ、ソース(降順の Range を含む)をそのまま使う。
            fragment=sourceFragment;
            uniqueNumber=snapshot;
            services.ApplyRenderContext(fragment);
            staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        }
        if(TryOptimizeRangeSetStages(sourceFragment,stages,terminalKind,ref snapshot,out var optimizedSetSource)) {
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            if(!TryCreateOptimizedFragment(
                optimizedSetSource,
                currentElementType,
                terminalKind,
                terminalResultType,
                terminalFunc1,
                terminalFunc2,
                terminalComparer,
                terminalValue,
                ref snapshot,
                out fragment)) {
                return false;
            }
            uniqueNumber=snapshot;
            services.ApplyRenderContext(fragment);
        staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        }
        var allowSingleStage = stages.Count==1
            &&(stages[0] is {Kind:PipelineStageKind.GroupBy,AggregatePlan:not null}
                ||stages[0].Kind is PipelineStageKind.Where
                or PipelineStageKind.Select
                or PipelineStageKind.Append
                or PipelineStageKind.Prepend
                or PipelineStageKind.DefaultIfEmpty);
        // rendererと同じ判定で専用経路の接続を確認する。通常Array/Listや、取引用終端が
        // 処理できない形状の一般融合へ複数の整列ブロックを通さない。
        var allowTransactionalOrderBlocks=sourceFragment is TransactionRowsRoot or Join&&terminalKind is{} orderedTerminal
            &&new PipelineTerminal(snapshot+1,new PipelineEnumerable(snapshot,sourceFragment,stages.ToArray(),currentElementType),orderedTerminal,terminalFunc1,terminalFunc2,terminalComparer,terminalValue).UsesTransactionalOrdering;
        if(!ValidateStageCombination(stages,terminalKind,allowSingleStage,allowTransactionalOrderBlocks)) {
            return false;
        }

        if(TryOptimizeOrderedRange(sourceFragment,stages,ref snapshot,out var optimizedSource)
            ||TryOptimizeModuloOrderedRange(sourceFragment,stages,ref snapshot,out optimizedSource)
            ||TryOptimizeModuloDistinctRange(sourceFragment,stages,ref snapshot,out optimizedSource)) {
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            if(!TryCreateOptimizedFragment(
                optimizedSource,
                currentElementType,
                terminalKind,
                terminalResultType,
                terminalFunc1,
                terminalFunc2,
                terminalComparer,
                terminalValue,
                ref snapshot,
                out fragment)) {
                return false;
            }
            uniqueNumber=snapshot;
            services.ApplyRenderContext(fragment);
        staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        }

        if(stages.Count==0) {
            // AsEnumerable などの透過stageが正規化で消えた場合は、terminalを持っていても
            // PipelineEnumerable を作らない。通常のFragment経路がsource能力をそのまま使う。
            // ただし、sourceFragment が二入力/入れ子操作(Join, Zip, GroupJoin, SelectMany 等)で
            // ConsumeExecutionStatement による直接インライン展開が可能な場合は、
            // PipelineTerminal を作成してフラットな直接ループ展開を行わせる。
            if(terminalKind is not null) {
                if(!hasPreservedUnionSource&&sourceFragment is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase or CountBy or AggregateBy)) {
                    return false;
                }
            } else {
                fragment=sourceFragment;
                uniqueNumber=snapshot;
                services.ApplyRenderContext(fragment);
                staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
                return true;
            }
        }
        if(sourceFragment is Join plannedJoin&&terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount){
            plannedJoin.PlanConsumption(DemandAnalysis.Analyze(stages,new(terminalKind,terminalFunc1 is not null,false,terminalValue)).Source,services.Trace);
        }
        var pipeline=PipelineEnumerable.CreateWithOrderSourceDemand(snapshot++,sourceFragment,stages.ToArray(),currentElementType,parallelSlots,orderHint);
        AddUnsafeAccessors(stages,unsafeAccessorCollector);
        if(terminalKind is { } terminal) {
            if(terminalResultType is null) {
                return false;
            }
            if(parallelSlots is not null){
                var hinted=new PipelineTerminal(snapshot++,pipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer,terminalValue);
                services.ApplyRenderContext(hinted);
                if(!hinted.CanApplyParallelHint(parallelTarget)){
                    throw Unsupported("This Parallel hint requires a supported transactional Lookup plan.");
                }
                fragment=hinted;
            }else if(ProviderIterateTerminal.TryCreate(snapshot,pipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer,out var providerIterateTerminal,terminalValue)) {
                fragment=providerIterateTerminal;
                snapshot++;
            } else {
                fragment=new PipelineTerminal(snapshot++,pipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer,terminalValue);
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

    private static bool IsOrderHint(IMethodSymbol method,string name)=>
        method.Name==name&&method.ReducedFrom?.ContainingType.ToDisplayString()=="Seamless.Linq.Enumerable";

    private static IMethodSymbol? HintTarget(SemanticModel model,InvocationExpressionSyntax hint,string companion){
        if(hint.Expression is not MemberAccessExpressionSyntax member
            ||RemoveParentheses(member.Expression) is not InvocationExpressionSyntax target
            ||model.GetSymbolInfo(target).Symbol is not IMethodSymbol method){return null;}
        if(IsOrderHint(method,companion)&&target.Expression is MemberAccessExpressionSyntax companionMember
            &&RemoveParentheses(companionMember.Expression) is InvocationExpressionSyntax operation){
            if(model.GetSymbolInfo(operation).Symbol is not IMethodSymbol operationMethod){return null;}
            method=operationMethod;
        }
        return method?.ReducedFrom??method;
    }

    private static bool IsSingleRepeatSelectMany(SemanticModel semanticModel,InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count is 1 or 2
        &&invocation.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax { Body: InvocationExpressionSyntax repeatInvocation }
        &&semanticModel.GetSymbolInfo(repeatInvocation).Symbol is IMethodSymbol { Name: "Repeat" } repeat
        &&repeat.ContainingType?.ToDisplayString()=="System.Linq.Enumerable"
        &&repeatInvocation.ArgumentList.Arguments.Count==2
        &&semanticModel.GetConstantValue(repeatInvocation.ArgumentList.Arguments[1].Expression) is { HasValue: true,Value: 1 };

    private static bool TryCreateOptimizedFragment(
        Enumerable optimizedSource,
        string currentElementType,
        PipelineTerminalKind? terminalKind,
        string? terminalResultType,
        FuncFragment? terminalFunc1,
        FuncFragment? terminalFunc2,
        string? terminalComparer,
        string? terminalValue,
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

        if(terminal is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount&&terminalFunc1 is null){
            fragment=new Count(uniqueNumber++,optimizedSource,terminalResultType);
            return true;
        }

        var terminalPipeline = new PipelineEnumerable(uniqueNumber++,optimizedSource,Array.Empty<PipelineStage>(),currentElementType);
        fragment=new PipelineTerminal(uniqueNumber++,terminalPipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer,terminalValue);
        return true;
    }

    private static bool FlushPipelineStages(
        ref int snapshot,
        ref Enumerable sourceFragment,
        ref string sourceStaticMethods,
        List<PipelineStage> stages,
        string currentElementType,
        PipelineBuildServices services,
        List<string>? unsafeAccessorCollector) {
        if(stages.Count==0) return true;
        var normalizedStages = GroupByFusionPass.Apply(stages,new TerminalDemand(null,false,false,null),services.Trace,allowDeferredArrayCounts:false);
        normalizedStages = LinqOptimizationExecutor.NormalizePipeline(normalizedStages,services.JoinStaticMethods,services.Configuration,services.Trace);
        if(normalizedStages.Count==0) {
            // Reverse().Reverse() のように正規化で段がすべて消えたら、元の並びをそのまま使う(段の無いパイプラインは作らない)。
            stages.Clear();
            return true;
        }
        if(!ValidateStageCombination(normalizedStages,null,allowSingleStage:true)) {
            return false;
        }
        var flushElementType = normalizedStages.Count>0 ? normalizedStages[normalizedStages.Count-1].OutputType : currentElementType;
        var pipeline = new PipelineEnumerable(snapshot++,sourceFragment,normalizedStages.ToArray(),flushElementType);
        AddUnsafeAccessors(normalizedStages,unsafeAccessorCollector);
        sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,pipeline.StructMethod);
        sourceFragment=pipeline;
        stages.Clear();
        return true;
    }

    private static bool WouldViolateStageCombination(IReadOnlyList<PipelineStage> stages,string methodName) {
        if(stages.Count==0) return false;
        var nextKind = GetProbableStageKind(methodName);
        if(nextKind is null) return false;

        var hasGroupBy = stages.Any(static s => s.Kind==PipelineStageKind.GroupBy);
        if(hasGroupBy&&nextKind!=PipelineStageKind.Select) return true;

        var hasOrder = stages.Any(static s => IsOrderStage(s.Kind));
        if(hasOrder&&(nextKind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending
            || nextKind is PipelineStageKind.Reverse or PipelineStageKind.TakeLast or PipelineStageKind.SkipLast or PipelineStageKind.DefaultIfEmpty
            || nextKind is PipelineStageKind.GroupBy or PipelineStageKind.SelectMany
            || nextKind is PipelineStageKind.Append or PipelineStageKind.Prepend or PipelineStageKind.Concat)) {
            return true;
        }

        var hasBuffering = stages.Any(static s => s.Kind is PipelineStageKind.Reverse or PipelineStageKind.DefaultIfEmpty or PipelineStageKind.TakeLast or PipelineStageKind.SkipLast or PipelineStageKind.TakeRange);
        if(hasBuffering&&(nextKind is PipelineStageKind.Reverse or PipelineStageKind.DefaultIfEmpty or PipelineStageKind.TakeLast or PipelineStageKind.SkipLast or PipelineStageKind.TakeRange
            || IsOrderStage(nextKind.Value)
            || nextKind is PipelineStageKind.GroupBy or PipelineStageKind.SelectMany)) {
            return true;
        }

        var hasConcatLike = stages.Any(static s => s.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend);
        if(hasConcatLike&&(nextKind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend
            || IsOrderStage(nextKind.Value)
            || nextKind is PipelineStageKind.GroupBy or PipelineStageKind.SelectMany)) {
            return true;
        }

        var hasSelectMany = stages.Any(static s => s.Kind==PipelineStageKind.SelectMany);
        if(hasSelectMany&&(nextKind is PipelineStageKind.SelectMany or PipelineStageKind.GroupBy||IsOrderStage(nextKind.Value))) {
            return true;
        }

        var hasBinarySet = stages.Any(static s => IsBinarySetStage(s.Kind));
        if(hasBinarySet&&(IsBinarySetStage(nextKind.Value)||nextKind is PipelineStageKind.SelectMany or PipelineStageKind.GroupBy)) {
            return true;
        }

        return false;
    }

    private static PipelineStageKind? GetProbableStageKind(string methodName) => methodName switch {
        "Where" => PipelineStageKind.Where,
        "Select" => PipelineStageKind.Select,
        "SelectMany" => PipelineStageKind.SelectMany,
        "Take" => PipelineStageKind.Take,
        "Skip" => PipelineStageKind.Skip,
        "TakeWhile" => PipelineStageKind.TakeWhile,
        "SkipWhile" => PipelineStageKind.SkipWhile,
        "TakeLast" => PipelineStageKind.TakeLast,
        "SkipLast" => PipelineStageKind.SkipLast,
        "TakeRange" => PipelineStageKind.TakeRange,
        "Distinct" => PipelineStageKind.Distinct,
        "DistinctBy" => PipelineStageKind.DistinctBy,
        "Reverse" => PipelineStageKind.Reverse,
        "OrderBy" => PipelineStageKind.OrderBy,
        "OrderByDescending" => PipelineStageKind.OrderByDescending,
        "Order" => PipelineStageKind.OrderBy,
        "OrderDescending" => PipelineStageKind.OrderByDescending,
        "ThenBy" => PipelineStageKind.ThenBy,
        "ThenByDescending" => PipelineStageKind.ThenByDescending,
        "DefaultIfEmpty" => PipelineStageKind.DefaultIfEmpty,
        "Append" => PipelineStageKind.Append,
        "Prepend" => PipelineStageKind.Prepend,
        "Concat" => PipelineStageKind.Concat,
        "Union" => PipelineStageKind.Union,
        "UnionBy" => PipelineStageKind.UnionBy,
        "Intersect" => PipelineStageKind.Intersect,
        "IntersectBy" => PipelineStageKind.IntersectBy,
        "Except" => PipelineStageKind.Except,
        "ExceptBy" => PipelineStageKind.ExceptBy,
        "GroupBy" => PipelineStageKind.GroupBy,
        _ => null
    };
}
