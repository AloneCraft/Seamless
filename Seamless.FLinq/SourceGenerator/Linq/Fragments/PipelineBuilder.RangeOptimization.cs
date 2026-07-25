namespace Seamless.FLinq.Linq.Fragments;

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
}
