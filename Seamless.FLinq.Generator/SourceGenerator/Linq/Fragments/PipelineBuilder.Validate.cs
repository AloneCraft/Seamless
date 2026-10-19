namespace Seamless.FLinq.Generator;

// 段の組み合わせ検証と補助宣言の出力。
internal static partial class PipelineBuilder {
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
        if(groupByStageCount>0&&orderStageCount>0&&!CanFuseGroupByThenOrder(stages)) return false;
        if(orderStageCount>0) {
            var orderIndexes = stages.Select(static (stage,index) => (stage,index))
                .Where(static pair => IsOrderStage(pair.stage.Kind))
                .Select(static pair => pair.index)
                .ToArray();
            if(stages[orderIndexes[0]].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) return false;
            // OrderBy を重ねると、後の OrderBy が主キーで、前の順序は「キーが等しいときの並び」として残る(LINQ は安定ソート)。
            // 後の OrderBy だけで並べると、キーが等しい要素の順が変わるので最適化しない。
            if(stages.Count(static stage => IsPrimaryOrderStage(stage.Kind))>1) return false;
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
            .DefaultIfEmpty(-1).First();
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

    // GroupBy の後に順序段がある場合は、グループから後続の中間段を通した結果を順序段のバッファへ積み、ソート後に残りの段を流す。
    // そのため、順序段が GroupBy より前にあるとグループ内の要素順が失われるため拒否し、GroupBy と順序段の間は逐次適用できる段だけを許可する。
    private static bool CanFuseGroupByThenOrder(IReadOnlyList<PipelineStage> stages) {
        var groupIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.GroupBy)
            .Select(static pair => pair.index)
            .Single();
        if(stages.Take(groupIndex).Any(static stage => IsOrderStage(stage.Kind))) {
            return false;
        }
        var orderIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => IsPrimaryOrderStage(pair.stage.Kind))
            .Select(static pair => pair.index)
            .DefaultIfEmpty(-1).First();
        if(orderIndex<=groupIndex) {
            return false;
        }
        for(var index = groupIndex+1;index<orderIndex;index++) {
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
            if(stage.AggregatePlan is { } plan) {
                foreach(var item in plan.Items) {
                    AddFuncUnsafeAccessors(item.InnerFunc,unsafeAccessorCollector);
                }
            }
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
