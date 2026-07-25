namespace Seamless.FLinq.Linq.Fragments;

// 隣接する段の併合と打ち消し。
internal static partial class PipelineBuilder {
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
}
