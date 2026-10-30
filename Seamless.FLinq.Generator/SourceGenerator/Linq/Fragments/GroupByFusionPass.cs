namespace Seamless.FLinq.Generator;

/// <summary>
/// GroupBy の集約を融合するかを、段の並びと終端が揃ってから、下流の要求(<see cref="DemandAnalysis"/>)を見て決める。
/// 融合した集約は1回の走査で集約するので、System.Linq より多くのグループの集約を評価しうる。System.Linq は引っぱったグループの集約しか評価しない。
/// - 下流が全グループを使う → 上限なしで融合する。
/// - 先頭の n グループだけを使う(Take(n)・First・Any など。n は実行時の式でもよい)→ 上限 n で融合する(n を超えるグループの要素は集約しない)。
/// - それ以外(区間・末尾・途中で止まる Where の後など)→ 融合しない(グループごとに引っぱられた分だけ集約する。System.Linq と同じ)。
/// </summary>
internal static class GroupByFusionPass {
    public static List<PipelineStage> Apply(List<PipelineStage> stages,TerminalDemand terminal,OptimizationTraceCollector? trace=null) {
        if(!stages.Any(static stage => stage.AggregateCandidate is not null)) {
            return stages;
        }
        var demand = DemandAnalysis.Analyze(stages,terminal);
        var result = new List<PipelineStage>(stages.Count);
        for(var index = 0;index<stages.Count;index++) {
            var stage = stages[index];
            if(stage.Kind==PipelineStageKind.GroupBy
                &&stage.AggregateCandidate is { } resultSelectorPlan
                &&GroupByConsumptionPlan.Create(demand.After[index]) is {Fuse:true} resultConsumption) {
                resultSelectorPlan=resultSelectorPlan.WithConsumption(resultConsumption);
                Record(trace,index,resultConsumption);
                result.Add(new PipelineStage(
                    PipelineStageKind.GroupBy,
                    stage.Func,
                    stage.Value,
                    stage.InputType,
                    stage.OutputType,
                    secondFunc: stage.SecondFunc,
                    thirdFunc: stage.ThirdFunc,
                    innerElementType: stage.InnerElementType,
                    keyType: stage.KeyType,
                    containsAnchor: stage.ContainsAnchor,
                    aggregatePlan: resultSelectorPlan));
                continue;
            }
            if(stage.Kind==PipelineStageKind.GroupBy
                &&stage.ThirdFunc is null
                &&stage.AggregatePlan is null
                &&index+1<stages.Count
                &&stages[index+1] is { Kind: PipelineStageKind.Select, AggregateCandidate: { } selectPlan, Func: { } selectFunc }
                &&GroupByConsumptionPlan.Create(demand.After[index+1]) is {Fuse:true} selectConsumption) {
                selectPlan=selectPlan.WithConsumption(selectConsumption);
                Record(trace,index,selectConsumption);
                result.Add(new PipelineStage(
                    PipelineStageKind.GroupBy,
                    stage.Func,
                    stage.Value,
                    stage.InputType,
                    selectPlan.ResultType,
                    secondFunc: stage.SecondFunc,
                    thirdFunc: selectFunc,
                    innerElementType: stage.InnerElementType,
                    keyType: stage.KeyType,
                    containsAnchor: stage.ContainsAnchor||selectFunc.ContainsAnchor,
                    aggregatePlan: selectPlan));
                index++;
                continue;
            }
            result.Add(stage);
        }
        return result;
    }

    private static void Record(OptimizationTraceCollector? trace,int index,GroupByConsumptionPlan plan){
        if(trace is null){return;}
        var run=trace.BeginRun(OptimizationLayer.Pipeline);
        trace.RecordPipeline(run,"SemanticPlan","GroupByConsumptionPlan",1,1,1,index,"GroupBy aggregate candidate",plan.ToString());
        trace.EndRun(run,OptimizationLayer.Pipeline,1,OptimizationTermination.FixedPoint);
    }

}
