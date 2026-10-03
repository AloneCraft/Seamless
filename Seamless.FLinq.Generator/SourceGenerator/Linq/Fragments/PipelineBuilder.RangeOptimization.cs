namespace Seamless.FLinq.Generator;

// Range ソース同士の段を畳む最適化。
internal static partial class PipelineBuilder {
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

        optimizedSource=primaryOrders[primaryOrders.Length-1].Descending
            ? new DescendingRangeRoot(uniqueNumber++,range.StartExpression,range.CountExpression)
            : range;
        return true;
    }

    // Range を x%C(C は正の定数)のキーで並べる段(続けて x 自身で ThenBy/ThenByDescending してもよい)は、
    // 並べ替えずに剰余類ごとに C 刻みで回す根に置き換える(RangeModuloOrderRoot)。
    private static bool TryOptimizeModuloOrderedRange(
        Enumerable source,
        IReadOnlyList<PipelineStage> stages,
        ref int uniqueNumber,
        out Enumerable optimizedSource) {
        optimizedSource=null!;
        if(source is not RangeRoot range
            ||source.TreeContainsAnchor()
            ||stages.Count is not (1 or 2)) {
            return false;
        }
        var primary = stages[0];
        if(primary.Kind is not (PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)
            ||!string.Equals(primary.Value,"null",StringComparison.Ordinal)
            ||primary.ContainsAnchor
            ||!RangeModuloOrderRoot.TryGetModulus(primary.Func,out var modulus)) {
            return false;
        }
        var descendingWithinKey = false;
        if(stages.Count==2) {
            var then = stages[1];
            if(then.Kind is not (PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending)
                ||!string.Equals(then.Value,"null",StringComparison.Ordinal)
                ||then.ContainsAnchor
                ||then.Func is not (null or { IsIdentity: true })) {
                return false;
            }
            descendingWithinKey=then.Kind==PipelineStageKind.ThenByDescending;
        }
        optimizedSource=new RangeModuloOrderRoot(uniqueNumber++,range,modulus,primary.Kind==PipelineStageKind.OrderByDescending,descendingWithinKey);
        return true;
    }

    // Range の DistinctBy(x => x%C) は、各符号の区間の先頭 C 個を流すだけの根に置き換える(RangeModuloDistinctRoot)。
    private static bool TryOptimizeModuloDistinctRange(
        Enumerable source,
        IReadOnlyList<PipelineStage> stages,
        ref int uniqueNumber,
        out Enumerable optimizedSource) {
        optimizedSource=null!;
        if(source is not RangeRoot range
            ||source.TreeContainsAnchor()
            ||stages.Count!=1
            ||stages[0].Kind!=PipelineStageKind.DistinctBy
            ||!string.Equals(stages[0].Value,"null",StringComparison.Ordinal)
            ||stages[0].ContainsAnchor
            ||!RangeModuloOrderRoot.TryGetModulus(stages[0].Func,out var modulus)) {
            return false;
        }
        optimizedSource=new RangeModuloDistinctRoot(uniqueNumber++,range,modulus);
        return true;
    }

    // Range の値は一意で昇順なので、Range に対して何もしない段を除く。
    // - Distinct(既定の比較子・恒等キーの DistinctBy)は、それまでの段が一意性を保つ(絞り込み・並べ替え)なら除く。
    // - 恒等キーの並べ替えは、それまでの段で既にその順に並んでいれば除く(続く ThenBy は主キーが一意なので結果に効かず、併せて除く)。
    //   先頭(まだ段を残していない)なら、Reverse と逆順の並べ替えは降順の Range(DescendingRangeRoot)に置き換えて除く。
    // - Skip(0) は除く。.Anchor のある段は残し、以降の順序と一意性は分からないものとして扱う。
    private static List<PipelineStage> StripRangeNoOpPrefix(ref Enumerable source,List<PipelineStage> stages,ref int uniqueNumber) {
        if(source is not (RangeRoot or DescendingRangeRoot)||source.TreeContainsAnchor()) {
            return stages;
        }
        var (start,count,descending) = source switch {
            RangeRoot ascendingRange => (ascendingRange.StartExpression,ascendingRange.CountExpression,false),
            DescendingRangeRoot descendingRange => (descendingRange.StartExpression,descendingRange.CountExpression,true),
            _ => throw new InvalidOperationException(),
        };
        var result = new List<PipelineStage>(stages.Count);
        var unique = true;
        var order = descending ? -1 : 1;
        var changed = false;
        for(var index = 0;index<stages.Count;index++) {
            var stage = stages[index];
            var atHead = result.Count==0;
            if(stage.ContainsAnchor||stage.InputType!="int") {
                result.Add(stage);
                unique=false;
                order=0;
                continue;
            }
            if(unique&&IsRangeDistinctNoOp(stage,source)
                ||stage.Kind==PipelineStageKind.Skip&&string.Equals(stage.Value?.Trim(),"0",StringComparison.Ordinal)) {
                changed=true;
                continue;
            }
            if(stage.Kind==PipelineStageKind.Reverse&&atHead) {
                descending=!descending;
                order=-order;
                changed=true;
                continue;
            }
            if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending
                &&IsIdentityOrderStage(stage)) {
                var wanted = stage.Kind==PipelineStageKind.OrderByDescending ? -1 : 1;
                if(order==wanted||atHead) {
                    if(atHead&&order!=wanted) {
                        descending=!descending;
                    }
                    order=wanted;
                    changed=true;
                    while(index+1<stages.Count
                        &&stages[index+1].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
                        &&!stages[index+1].ContainsAnchor) {
                        index++;
                    }
                    continue;
                }
                result.Add(stage);
                order=wanted;
                continue;
            }
            result.Add(stage);
            switch(stage.Kind) {
                case PipelineStageKind.Where:
                case PipelineStageKind.Take:
                case PipelineStageKind.Skip:
                case PipelineStageKind.TakeWhile:
                case PipelineStageKind.SkipWhile:
                case PipelineStageKind.SkipLast:
                case PipelineStageKind.TakeLast:
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.Distinct:
                case PipelineStageKind.DistinctBy:
                    break;
                case PipelineStageKind.Reverse:
                    order=-order;
                    break;
                case PipelineStageKind.OrderBy:
                case PipelineStageKind.OrderByDescending:
                case PipelineStageKind.ThenBy:
                case PipelineStageKind.ThenByDescending:
                    order=0;
                    break;
                default:
                    unique=false;
                    order=0;
                    break;
            }
        }
        if(!changed) {
            return stages;
        }
        var wasDescending = source is DescendingRangeRoot;
        if(descending!=wasDescending) {
            source=descending
                ? new DescendingRangeRoot(uniqueNumber++,start,count)
                : new RangeRoot(uniqueNumber++,start,count);
        }
        return result;
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
            RangeRoot currentRange => new RangeExceptRoot(number,currentRange,new[] { second }),
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
}
