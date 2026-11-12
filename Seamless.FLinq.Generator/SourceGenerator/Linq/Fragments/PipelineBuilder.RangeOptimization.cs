namespace Seamless.FLinq.Generator;

// Range ソース同士の段を畳む最適化。
internal static partial class PipelineBuilder {
    private static bool TryBuildRangeUnionCount(
        SemanticModel model,InvocationExpressionSyntax invocation,ref int number,PipelineBuildServices services,out Fragment fragment){
        fragment=null!;
        if(!TryCollectRangeUnionInputs(model,invocation,out var inputs,out var resultType)){return false;}
        var ranges=inputs.Select(range=>(
            Start:services.RenderExpression(range.ArgumentList.Arguments[0].Expression),
            Count:services.RenderExpression(range.ArgumentList.Arguments[1].Expression))).ToArray();
        fragment=new RangeUnionCount(number++,ranges,resultType);
        return true;
    }

    internal static bool IsStaticRangeUnionCount(SemanticModel model,InvocationExpressionSyntax invocation,IMethodSymbol method)=>
        method.ReducedFrom is null&&TryCollectRangeUnionInputs(model,invocation,out _,out _);

    private static bool TryCollectRangeUnionInputs(SemanticModel model,InvocationExpressionSyntax invocation,
        out IReadOnlyList<InvocationExpressionSyntax> ranges,out string resultType){
        ranges=Array.Empty<InvocationExpressionSyntax>();
        resultType=string.Empty;
        if(model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol {Name:"Count" or "LongCount"} terminal
            ||(terminal.ReducedFrom??terminal).ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            ||invocation.ArgumentList.Arguments.Any(static argument=>argument.NameColon is not null)){
            return false;
        }
        ExpressionSyntax input;
        if(terminal.ReducedFrom is null){
            if(invocation.ArgumentList.Arguments.Count!=1){return false;}
            input=invocation.ArgumentList.Arguments[0].Expression;
        }else{
            if(invocation.ArgumentList.Arguments.Count!=0||invocation.Expression is not MemberAccessExpressionSyntax member){return false;}
            input=member.Expression;
        }
        if(RemoveParentheses(input) is not InvocationExpressionSyntax root
            ||model.GetSymbolInfo(root).Symbol is not IMethodSymbol {Name:"Union"}){
            return false;
        }
        var collected=new List<InvocationExpressionSyntax>();
        bool Collect(ExpressionSyntax expression,int depth){
            if(depth>=16||RemoveParentheses(expression) is not InvocationExpressionSyntax call
                ||model.GetSymbolInfo(call).Symbol is not IMethodSymbol method
                ||(method.ReducedFrom??method).ContainingType.ToDisplayString()!="System.Linq.Enumerable"
                ||call.ArgumentList.Arguments.Any(static argument=>argument.NameColon is not null)){
                return false;
            }
            var arguments=call.ArgumentList.Arguments;
            if(method.Name is "Union" or "Concat"){
                if(method.ReducedFrom is null){
                    return arguments.Count==2&&Collect(arguments[0].Expression,depth+1)&&Collect(arguments[1].Expression,depth+1);
                }
                return arguments.Count==1&&call.Expression is MemberAccessExpressionSyntax union
                    &&Collect(union.Expression,depth+1)&&Collect(arguments[0].Expression,depth+1);
            }
            if(method.Name!="Range"||arguments.Count!=2||collected.Count==16){return false;}
            collected.Add(call);
            return true;
        }
        // 正規化された左右のUnion木を、入力の評価・検証の順序を保って収集する。
        if(!Collect(input,0)||collected.Count<2){return false;}
        ranges=collected;
        resultType=terminal.Name=="Count"?"int":"long";
        return true;
    }
    private sealed class RangeUnionCount(int number,IReadOnlyList<(string Start,string Count)> ranges,string resultType):Scalar(number),DirectExecutionFragment{
        public override string StructMethod=>string.Empty;
        public override string GetEnumeratorStruct=>string.Empty;
        public override string GetTryGetNextBody=>string.Empty;

        public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName){
            var validate=$"__validateUnionRange{number}";
            var count=$"__countUnionRanges{number}";
            var parameters=string.Join(",",ranges.Select((_,index)=>$"(long Start,long End) range{index}"));
            var items=string.Join(",",ranges.Select((_,index)=>$"range{index}"));
            var arguments=string.Join(",",ranges.Select(range=>$"{validate}({range.Start},{range.Count})"));
            return $$"""
                // 各Rangeを元の順序で評価・検証し、区間だけを整列して件数を求める。
                static (long Start,long End) {{validate}}(int start,int count){
                    var end=(long)start+count;
                    if(count<0||end>1L+int.MaxValue) throw new global::System.ArgumentOutOfRangeException("count");
                    return (start,end);
                }
                static long {{count}}({{parameters}}){
                    global::System.Span<(long Start,long End)> ranges=stackalloc (long Start,long End)[]{ {{items}} };
                    global::System.MemoryExtensions.Sort(ranges);
                    long coveredEnd=long.MinValue,total=0;
                    foreach(var range in ranges){
                        var start=global::System.Math.Max(coveredEnd,range.Start);
                        if(range.End>start) total+=range.End-start;
                        coveredEnd=global::System.Math.Max(coveredEnd,range.End);
                    }
                    return total;
                }
                {{actualTypeName}} {{actualVariableName}}=({{actualTypeName}})checked(({{resultType}}){{count}}({{arguments}}));
                """;
        }
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
            // Reverse は、それまでに残した段が添字を使わない Where/Select だけなら、それらと入れ替えられる(要素ごとの判定と写像は順に依らない)。
            if(stage.Kind==PipelineStageKind.Reverse
                &&result.All(static kept => kept.Kind is PipelineStageKind.Where or PipelineStageKind.Select&&!kept.UsesIndex)) {
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
        PipelineTerminalKind? terminal,
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
                &&TryCreateRangeUnion(stage,unionRange,terminal,uniqueNumber,out var united)) {
                uniqueNumber++;
                current=united;
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
                &&TryCreateRangeIntersection(stage,intersectRange,terminal,uniqueNumber,out var intersection)) {
                uniqueNumber+=intersection is RangeIntersectionRoot?2:1;
                current=intersection;
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

    private static bool TryCreateRangeIntersection(PipelineStage stage,RangeRoot left,PipelineTerminalKind? terminal,int number,out Enumerable result) {
        result=null!;
        if(!TryGetIdentityRangeSetStage(stage,left.ElementType,out var right)
            ||stage.Kind is not (PipelineStageKind.Intersect or PipelineStageKind.IntersectBy)) {
            return false;
        }
        if(!TryReadLiteralRange(left,out var leftStart,out var leftEnd)
            ||!TryReadLiteralRange(right,out var rightStart,out var rightEnd)) {
            if(terminal is not (PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount)) {
                return false;
            }
            result=new RangeIntersectionRoot(number,left,right);
            return true;
        }
        var start=Math.Max(leftStart,rightStart);
        var count=Math.Max(0L,Math.Min(leftEnd,rightEnd)-start);
        var culture=global::System.Globalization.CultureInfo.InvariantCulture;
        result=new RangeRoot(number,start.ToString(culture),count.ToString(culture));
        return true;
    }

    private static bool TryCreateRangeUnion(PipelineStage stage,RangeRoot left,PipelineTerminalKind? terminal,int number,out Enumerable result) {
        result=null!;
        if(!TryGetIdentityRangeSetStage(stage,left.ElementType,out var second)
            ||stage.Kind is not (PipelineStageKind.Union or PipelineStageKind.UnionBy)) {
            return false;
        }
        if(TryReadLiteralRange(left,out var leftStart,out var leftEnd)
            &&TryReadLiteralRange(second,out var rightStart,out var rightEnd)) {
            if(leftStart==leftEnd) {
                result=second;
                return true;
            }
            if(rightStart==rightEnd||(leftStart<=rightStart&&rightEnd<=leftEnd)) {
                result=left;
                return true;
            }
            // 左を先に出した後で右の未出部分が続く場合だけ、1本の区間にできる。
            var count=rightEnd-leftStart;
            if(rightStart>=leftStart&&rightStart<=leftEnd&&count<=int.MaxValue) {
                result=new RangeRoot(number,left.StartExpression,count.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
        }
        if(terminal is not (PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount)) {
            return false;
        }
        result=new RangeUnionRoot(number,left,second);
        return true;
    }

    private static bool TryReadLiteralRange(RangeRoot range,out int start,out long end) {
        // 同じ式が2回書かれていても、呼出しや引数検証を省略できるとは限らない。
        var culture=global::System.Globalization.CultureInfo.InvariantCulture;
        var style=global::System.Globalization.NumberStyles.Integer;
        end=0;
        if(!int.TryParse(range.StartExpression,style,culture,out start)
            ||!int.TryParse(range.CountExpression,style,culture,out var count)||count<0) {
            return false;
        }
        end=(long)start+count;
        return end<=1L+int.MaxValue;
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

}
