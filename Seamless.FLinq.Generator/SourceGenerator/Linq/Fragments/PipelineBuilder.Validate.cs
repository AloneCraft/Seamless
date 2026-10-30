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
        var isScalarTerminal = terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Sum or PipelineTerminalKind.Average
            or PipelineTerminalKind.Any or PipelineTerminalKind.All or PipelineTerminalKind.Contains
            or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault
            or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault;

        var isSliceFusionTerminal = isScalarTerminal || terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList;

        var leadingSliceCount = 0;
        if(isSliceFusionTerminal) {
            for(var i = 0; i < stages.Count; i++) {
                var k = stages[i].Kind;
                if(k is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast or PipelineStageKind.Skip or PipelineStageKind.Take or PipelineStageKind.SkipLast or PipelineStageKind.TakeLast) {
                    leadingSliceCount++;
                    continue;
                }
                break;
            }
        }

        var nonLeadingSkipLastCount = 0;
        var nonLeadingTakeLastCount = 0;
        for(var i = leadingSliceCount; i < stages.Count; i++) {
            if(stages[i].Kind==PipelineStageKind.SkipLast) nonLeadingSkipLastCount++;
            if(stages[i].Kind==PipelineStageKind.TakeLast) nonLeadingTakeLastCount++;
        }
        var takeRangeStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.TakeRange);
        var bufferingStageCount = reverseStageCount+nonLeadingSkipLastCount+nonLeadingTakeLastCount+takeRangeStageCount+defaultIfEmptyStageCount;
        var isMultiBinaryFused = CanFuseBinarySetMulti(stages,terminalKind);
        if(selectManyStageCount>1) return false;
        if(groupByStageCount>1) return false;
        if(bufferingStageCount>1) return false;
        if(binarySetStageCount>1&&!isMultiBinaryFused) return false;
        if(groupByStageCount>0&&(selectManyStageCount>0||binarySetStageCount>0||concatLikeStageCount>0)) return false;
        if(bufferingStageCount>0&&(selectManyStageCount>0||binarySetStageCount>0||concatLikeStageCount>0||orderStageCount>0||groupByStageCount>0)) return false;
        if(binarySetStageCount>0&&selectManyStageCount>0) return false;
        if(concatLikeStageCount>1) return false;
        if(!HasSafeConcatLikeOrder(stages,terminalKind)) return false;
        if(concatLikeStageCount>0&&(selectManyStageCount>0||(binarySetStageCount>0&&!isMultiBinaryFused))) return false;
        if(orderStageCount>0&&(selectManyStageCount>0||concatLikeStageCount>0)) return false;
        if(binarySetStageCount>0&&orderStageCount>0&&!CanFuseBinarySetThenOrder(stages)&&!CanFuseOrderThenBinarySet(stages,terminalKind)) return false;
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

    // Append / Prepend / Concat / Union / UnionBy は、前後に別の段があると融合の出力が System.Linq とずれる
    // (追加した要素に後ろの Select や Take が掛からない、Where の前後で連結の位置がずれる、など)。
    // 2026-10-22 の全組み合わせ検証(Benchmark.Dynamic)で見つかった形を融合しない。
    //  - Append / Prepend は最後の段のときだけ融合する。
    //  - Concat / Union / UnionBy の前に Where / TakeWhile / SkipWhile / Select / Distinct / DistinctBy があるときは融合しない。
    //  - Concat / Union / UnionBy の後ろに Where / Skip / Take / TakeWhile / SkipWhile があるときは融合しない。
    private static bool HasSafeConcatLikeOrder(IReadOnlyList<PipelineStage> stages,PipelineTerminalKind? terminalKind) {
        for(var i = 0;i<stages.Count;i++) {
            var kind = stages[i].Kind;
            if(kind is PipelineStageKind.Append or PipelineStageKind.Prepend) {
                for(var j = i+1;j<stages.Count;j++) {
                    if(stages[j].Kind is not PipelineStageKind.AsEnumerable) {
                        return false;
                    }
                }
            } else if(terminalKind is not null&&kind is PipelineStageKind.Concat or PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                // 終端のない融合(Consume で流す形。Range の Concat / Union の後ろに添字なしの Where / Select を流す専用の展開)は対象外。
                // 対象は、ToArray / Count / Sum / First などの終端がついた融合で、全組み合わせの検証で結果がずれた形。
                for(var j = 0;j<i;j++) {
                    if(stages[j].Kind is PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile
                        or PipelineStageKind.Select or PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                        return false;
                    }
                }
                for(var j = i+1;j<stages.Count;j++) {
                    if(stages[j].Kind is PipelineStageKind.Where or PipelineStageKind.Skip or PipelineStageKind.Take
                        or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile) {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static bool CanIgnoreOrderForTerminalKind(PipelineTerminalKind? terminalKind) =>
        terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.Contains;

    private static bool CanFuseOrderThenBinarySet(IReadOnlyList<PipelineStage> stages,PipelineTerminalKind? terminalKind) {
        if(!CanIgnoreOrderForTerminalKind(terminalKind)) {
            return false;
        }
        var setIndex = -1;
        var orderIndex = -1;
        for(var i = 0;i<stages.Count;i++) {
            if(IsBinarySetStage(stages[i].Kind)) {
                setIndex=i;
            } else if(IsPrimaryOrderStage(stages[i].Kind)) {
                orderIndex=i;
            }
        }
        if(setIndex<0||orderIndex<0||orderIndex>setIndex) {
            return false;
        }
        for(var i = 0;i<setIndex;i++) {
            if(stages[i].Kind is PipelineStageKind.Skip or PipelineStageKind.Take or PipelineStageKind.SkipLast or PipelineStageKind.TakeLast) {
                return false;
            }
        }
        // この融合は並べ替えを捨てる。途中で止まる終端(Any / Contains)で、止まるまでにラムダ(ExceptBy のキー選択子など)を評価するなら、
        // 並べ替えた順で評価しないと System.Linq と別の要素に評価するので、融合しない(要素ごとに System.Linq に合わせる方針)。
        var lastOrderIndex = orderIndex;
        while(lastOrderIndex+1<stages.Count&&stages[lastOrderIndex+1].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) {
            lastOrderIndex++;
        }
        var demand = DemandAnalysis.Analyze(stages,new TerminalDemand(terminalKind,false,false));
        return demand.After[lastOrderIndex].AllowsDroppingOrder;
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

    private static bool CanFuseBinarySetMulti(IReadOnlyList<PipelineStage> stages,PipelineTerminalKind? terminalKind) {
        if(terminalKind is not (PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Sum or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList)) {
            return false;
        }
        var binaryCount = 0;
        var firstBinaryIndex = -1;
        var secondBinaryIndex = -1;
        PipelineStage? firstBinary = null;
        PipelineStage? secondBinary = null;
        for(var i = 0;i<stages.Count;i++) {
            var st = stages[i];
            if(IsBinarySetStage(st.Kind)||st.Kind==PipelineStageKind.Concat) {
                if(binaryCount==0) {
                    firstBinary=st;
                    firstBinaryIndex=i;
                } else if(binaryCount==1) {
                    secondBinary=st;
                    secondBinaryIndex=i;
                }
                binaryCount++;
            }
        }
        if(binaryCount!=2||firstBinary is null||secondBinary is null) {
            return false;
        }
        if(firstBinary.Kind is not (PipelineStageKind.Union or PipelineStageKind.UnionBy or PipelineStageKind.Concat)) {
            return false;
        }
        if(secondBinary.Kind is not (PipelineStageKind.Intersect or PipelineStageKind.IntersectBy)) {
            return false;
        }
        for(var i = 0;i<firstBinaryIndex;i++) {
            var st = stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast or PipelineStageKind.Skip or PipelineStageKind.Take or PipelineStageKind.SkipLast or PipelineStageKind.TakeLast) {
                continue;
            }
            if(st.Kind is PipelineStageKind.Select or PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile&&!st.UsesIndex) {
                continue;
            }
            return false;
        }
        for(var i = firstBinaryIndex+1;i<secondBinaryIndex;i++) {
            var st = stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                continue;
            }
            return false;
        }
        for(var i = secondBinaryIndex+1;i<stages.Count;i++) {
            var st = stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                continue;
            }
            if(st.Kind is PipelineStageKind.Select or PipelineStageKind.Where&&!st.UsesIndex) {
                continue;
            }
            return false;
        }
        return true;
    }

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
