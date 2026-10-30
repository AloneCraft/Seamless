namespace Seamless.FLinq.Generator;

/*
DemandAnalysis は「下流が列のどこまで、どう使うか」を、段の並びと終端から逆向きに求める(docs/refactoring-designs/20261024-2208-flinq-demand-analysis.md)。
FLinq は System.Linq が評価しない要素に利用者のラムダを評価しない。先に計算する段(GroupBy の集約・並べ替えの鍵・集合の先読み)は、
ここで求めた範囲だけを計算し、範囲が正確に分からないときは引っぱられた分だけ計算する(System.Linq と同じ)。
- Window: 使う範囲。All(全部)/ None(使わない)/ Prefix(先頭 End 個)/ Range([Start,End)。End が null なら最後まで)/ Suffix(末尾 End 個)/ Unknown。
- ValuesUsed: 要素の値を使うか(Count()・Any() は使わない。値を使わない Select のラムダは評価しない)。
- OrderSensitive: この列の順序を入れ替えると結果が変わりうるか。
- LambdasBeforeStop: 途中で止まる消費(Window が All でない)のとき、止まるまでに要素ごとに評価するラムダの数(終端の述語を含む)。
  これが 0 でない列の手前で、並べ替え・Reverse・Distinct を消すと、System.Linq と別の要素にラムダを評価する。
*/
internal enum DemandWindowKind {
    All,
    None,
    Prefix,
    Range,
    Suffix,
    Unknown,
}

// 0 以上の個数を表す式。定数か、段の値(Take(n) の n など。実行時に決まる)を、飽和する足し算と最小値で組み合わせる。
internal abstract record CountExpr {
    public static CountExpr Constant(long value) => new ConstantCount(Math.Max(0,value));

    // 段の値の式(Take(n) の n など)。整数の定数として読めるときは定数にする。stage は値を持つ段(終端の引数なら null)。
    // 段は位置ではなく参照で持つ(正規化などで段の位置がずれても、描くときに探せる)。
    public static CountExpr FromStageValue(PipelineStage? stage,string? valueText) {
        if(valueText is not null&&long.TryParse(valueText.Trim(),out var constant)) {
            return Constant(constant);
        }
        return new StageValueCount(stage,valueText);
    }

    public static CountExpr Add(CountExpr left,CountExpr right) =>
        left is ConstantCount { Value: var a }&&right is ConstantCount { Value: var b }
            ? Constant(a>long.MaxValue-b ? long.MaxValue : a+b)
            : left is ConstantCount { Value: 0 } ? right
            : right is ConstantCount { Value: 0 } ? left
            : new SumCount(left,right);

    public static CountExpr Min(CountExpr left,CountExpr right) =>
        left is ConstantCount { Value: var a }&&right is ConstantCount { Value: var b }
            ? Constant(Math.Min(a,b))
            : left==right ? left
            : new MinCount(left,right);

    public bool TryGetConstant(out long value) {
        if(this is ConstantCount constant) {
            value=constant.Value;
            return true;
        }
        value=0;
        return false;
    }

    // C# の int の式にする。stageValue は段の値の葉を描く(既定は段の値の式をそのまま、負なら 0 に切り上げる)。
    public string Render(Func<PipelineStage?,string?,string>? stageValue = null) => this switch {
        ConstantCount constant => constant.Value>=int.MaxValue ? "int.MaxValue" : constant.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        StageValueCount leaf => stageValue is not null ? stageValue(leaf.Stage,leaf.ValueText) : $"global::System.Math.Max(0,({leaf.ValueText}))",
        SumCount sum => $"(int)global::System.Math.Min((long)({sum.Left.Render(stageValue)})+(long)({sum.Right.Render(stageValue)}),int.MaxValue)",
        MinCount min => $"global::System.Math.Min({min.Left.Render(stageValue)},{min.Right.Render(stageValue)})",
        _ => throw new InvalidOperationException(),
    };
}
internal sealed record ConstantCount(long Value):CountExpr;
internal sealed record StageValueCount(PipelineStage? Stage,string? ValueText):CountExpr;
internal sealed record SumCount(CountExpr Left,CountExpr Right):CountExpr;
internal sealed record MinCount(CountExpr Left,CountExpr Right):CountExpr;

internal readonly record struct DemandWindow(DemandWindowKind Kind,CountExpr? Start = null,CountExpr? End = null) {
    public static DemandWindow All => new(DemandWindowKind.All);
    public static DemandWindow None => new(DemandWindowKind.None);
    public static DemandWindow Unknown => new(DemandWindowKind.Unknown);
    public static DemandWindow Prefix(CountExpr count) => new(DemandWindowKind.Prefix,End: count);
    public static DemandWindow Suffix(CountExpr count) => new(DemandWindowKind.Suffix,End: count);
    public static DemandWindow Range(CountExpr start,CountExpr? end) =>
        start.TryGetConstant(out var s)&&s==0
            ? end is null ? All : Prefix(end)
            : new(DemandWindowKind.Range,start,end);
    public bool IsAll => this.Kind==DemandWindowKind.All;
}

internal readonly record struct Demand(DemandWindow Window,bool ValuesUsed,bool OrderSensitive,int LambdasBeforeStop) {
    // 並べ替え・Reverse を、この列の手前で消してよいか(要素ごとに System.Linq に合わせる。2026-10-24 の方針)。
    // 結果が順序に依存せず、かつ「全部を使う」か「止まるまでに評価するラムダが無い」とき。
    public bool AllowsDroppingOrder => !this.OrderSensitive&&(this.Window.IsAll||this.LambdasBeforeStop==0);
}

internal readonly record struct TerminalDemand(PipelineTerminalKind? Kind,bool HasPredicate,bool SumIgnoresOrder,string? IndexText = null);

internal sealed class DemandAnalysisResult(Demand source,Demand[] after) {
    // 最初の段に入る列(源)への要求。
    public Demand Source { get; } = source;
    // After[i]: i 番目の段が出す列への要求。
    public IReadOnlyList<Demand> After { get; } = after;
    public Demand Before(int index) => index==0 ? this.Source : this.After[index-1];
}

internal static class DemandAnalysis {
    // 構文書き換え前にも同じ Window の伝播を使う。列をローカル変数へ保存した場合や
    // 未対応の消費者は需要を確定できないので、全件消費とみなさない。
    internal static bool IsEntireSequenceConsumed(InvocationExpressionSyntax sequence,SemanticModel model,INamedTypeSymbol? enumerableType){
        var stages=new List<PipelineStage>();
        ExpressionSyntax current=sequence;
        while(true){
            while(current.Parent is ParenthesizedExpressionSyntax parentheses){
                current=parentheses;
            }
            if(current.Parent is not MemberAccessExpressionSyntax memberAccess
                ||memberAccess.Expression!=current
                ||memberAccess.Parent is not InvocationExpressionSyntax invocation
                ||!CarrierAnalysis.TryGetLinqSequenceCall(invocation,model,enumerableType,out var call)){
                return false;
            }
            if(Enum.TryParse<PipelineTerminalKind>(call.MethodName,out var terminal)){
                var hasPredicate=call.Arguments.Any(argument=>model.GetTypeInfo(argument.Expression).ConvertedType is INamedTypeSymbol{DelegateInvokeMethod:not null});
                var terminalDemand=new TerminalDemand(terminal,hasPredicate,false,call.Arguments.FirstOrDefault()?.Expression.ToString());
                return Analyze(stages,terminalDemand).Source.Window.IsAll;
            }
            if(!OperatorPipelineManifest.TryGet(call.MethodName,out var definition)){
                return false;
            }
            // Window はラムダの本体・入出力型を使わない。Take/Skip の範囲と添字つき Select
            // だけは消費範囲に影響するため、構文と delegate 型から持ち込む。
            var usesIndex=call.Arguments.Any(argument=>model.GetTypeInfo(argument.Expression).ConvertedType is INamedTypeSymbol{DelegateInvokeMethod.Parameters.Length:>1});
            var value=call.Arguments.FirstOrDefault()?.Expression.ToString();
            stages.Add(new PipelineStage(definition.Stage,null,value,string.Empty,string.Empty,usesIndex:usesIndex));
            current=invocation;
        }
    }

    public static DemandAnalysisResult Analyze(IReadOnlyList<PipelineStage> stages,TerminalDemand terminal) {
        var after = new Demand[stages.Count];
        var current = Seed(terminal);
        for(var index = stages.Count-1;index>=0;index--) {
            after[index]=current;
            current=Propagate(stages[index],index,current);
        }
        return new DemandAnalysisResult(current,after);
    }

    internal static Demand Seed(TerminalDemand terminal) {
        var predicate = terminal.HasPredicate ? 1 : 0;
        return terminal.Kind switch {
            PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary
                or PipelineTerminalKind.Aggregate or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy
                => new(DemandWindow.All,true,true,0),
            PipelineTerminalKind.ToHashSet or PipelineTerminalKind.Min or PipelineTerminalKind.Max
                => new(DemandWindow.All,true,false,0),
            PipelineTerminalKind.Sum or PipelineTerminalKind.Average
                => new(DemandWindow.All,true,!terminal.SumIgnoresOrder,0),
            PipelineTerminalKind.Count or PipelineTerminalKind.LongCount
                => new(DemandWindow.All,terminal.HasPredicate,false,0),
            PipelineTerminalKind.Any when !terminal.HasPredicate
                => new(DemandWindow.Prefix(CountExpr.Constant(1)),false,false,0),
            PipelineTerminalKind.Any or PipelineTerminalKind.All or PipelineTerminalKind.Contains
                => new(DemandWindow.Unknown,true,false,predicate),
            PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault when !terminal.HasPredicate
                => new(DemandWindow.Prefix(CountExpr.Constant(1)),true,true,0),
            PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault
                => new(DemandWindow.Unknown,true,true,predicate),
            PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault when !terminal.HasPredicate
                => new(DemandWindow.Suffix(CountExpr.Constant(1)),true,true,0),
            PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault
                => new(DemandWindow.All,true,true,0),
            PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault when terminal.IndexText is { } index
                => new(DemandWindow.Range(CountExpr.FromStageValue(null,index),CountExpr.Add(CountExpr.FromStageValue(null,index),CountExpr.Constant(1))),true,true,0),
            _ => new(DemandWindow.Unknown,true,true,0),
        };
    }

    private static Demand Propagate(PipelineStage stage,int index,Demand output) {
        var window = PropagateWindow(stage,index,output.Window);
        var valuesUsed = PropagateValuesUsed(stage,output);
        var orderSensitive = PropagateOrderSensitive(stage,output);
        var lambdas = window.IsAll
            ? 0
            : output.LambdasBeforeStop+(EvaluatesLambdaPerElement(stage,output) ? 1 : 0);
        return new Demand(window,valuesUsed,orderSensitive,lambdas);
    }

    private static DemandWindow PropagateWindow(PipelineStage stage,int index,DemandWindow output) {
        switch(stage.Kind) {
            case PipelineStageKind.Select when !stage.UsesIndex:
            case PipelineStageKind.Cast:
            case PipelineStageKind.AsEnumerable:
                return output;
            case PipelineStageKind.Take when !stage.UsesIndex&&stage.Value is not null:
                var take = CountExpr.FromStageValue(stage,stage.Value);
                return output.Kind switch {
                    DemandWindowKind.All => DemandWindow.Prefix(take),
                    DemandWindowKind.None => DemandWindow.None,
                    DemandWindowKind.Prefix => DemandWindow.Prefix(CountExpr.Min(output.End!,take)),
                    DemandWindowKind.Range => DemandWindow.Range(output.Start!,output.End is null ? take : CountExpr.Min(output.End,take)),
                    _ => DemandWindow.Unknown,
                };
            case PipelineStageKind.Skip when !stage.UsesIndex&&stage.Value is not null:
                var skip = CountExpr.FromStageValue(stage,stage.Value);
                return output.Kind switch {
                    DemandWindowKind.All => DemandWindow.Range(skip,null),
                    DemandWindowKind.None => DemandWindow.None,
                    DemandWindowKind.Prefix => DemandWindow.Range(skip,CountExpr.Add(skip,output.End!)),
                    DemandWindowKind.Range => DemandWindow.Range(CountExpr.Add(skip,output.Start!),output.End is null ? null : CountExpr.Add(skip,output.End)),
                    _ => DemandWindow.Unknown,
                };
            case PipelineStageKind.Reverse:
                return output.Kind switch {
                    DemandWindowKind.All or DemandWindowKind.None => output,
                    DemandWindowKind.Prefix => DemandWindow.Suffix(output.End!),
                    DemandWindowKind.Suffix => DemandWindow.Prefix(output.End!),
                    _ => DemandWindow.Unknown,
                };
            case PipelineStageKind.Append:
            case PipelineStageKind.Concat:
                // 前半(源)は先頭から読まれる。先頭 e 個を使うなら、源から読むのは高々 e 個(源が短ければ全部)。
                return output.Kind is DemandWindowKind.All or DemandWindowKind.None or DemandWindowKind.Prefix ? output : DemandWindow.Unknown;
            case PipelineStageKind.Prepend:
                return output.Kind switch {
                    DemandWindowKind.All or DemandWindowKind.None => output,
                    DemandWindowKind.Prefix when output.End!.TryGetConstant(out var e) => e<=1 ? DemandWindow.None : DemandWindow.Prefix(CountExpr.Constant(e-1)),
                    _ => DemandWindow.Unknown,
                };
            case PipelineStageKind.TakeWhile:
                // 条件が偽になった所で止まるので、下流が全部を使っても、源からどこまで読むかは分からない。
                return output.Kind==DemandWindowKind.None ? DemandWindow.None : DemandWindow.Unknown;
            case PipelineStageKind.OrderBy:
            case PipelineStageKind.OrderByDescending:
            case PipelineStageKind.ThenBy:
            case PipelineStageKind.ThenByDescending:
            case PipelineStageKind.GroupBy:
                // System.Linq は全要素の鍵(キー)を計算する。並べ替えの範囲・グループの範囲は、出口の要求(After)で見る。
                return output.Kind==DemandWindowKind.None ? DemandWindow.None : DemandWindow.All;
            default:
                // Where・TakeWhile・SkipWhile・Distinct・集合演算・SelectMany・TakeLast・SkipLast・DefaultIfEmpty など。
                return output.Kind is DemandWindowKind.All or DemandWindowKind.None ? output : DemandWindow.Unknown;
        }
    }

    private static bool PropagateValuesUsed(PipelineStage stage,Demand output) => stage.Kind switch {
        PipelineStageKind.Select when !stage.UsesIndex => output.ValuesUsed||stage.ContainsAnchor,
        PipelineStageKind.Take or PipelineStageKind.Skip or PipelineStageKind.TakeLast or PipelineStageKind.SkipLast
            or PipelineStageKind.Reverse or PipelineStageKind.AsEnumerable or PipelineStageKind.Append or PipelineStageKind.Prepend
            or PipelineStageKind.Concat or PipelineStageKind.DefaultIfEmpty
            => output.ValuesUsed,
        _ => true,
    };

    private static bool PropagateOrderSensitive(PipelineStage stage,Demand output) => stage.Kind switch {
        PipelineStageKind.Take or PipelineStageKind.Skip or PipelineStageKind.TakeLast or PipelineStageKind.SkipLast
            or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.TakeRange
            => true,
        PipelineStageKind.Select or PipelineStageKind.Where => stage.UsesIndex||output.OrderSensitive,
        // どの重複が残るかを比較子やキーが決めるので、値を使うなら順序に依存する。
        PipelineStageKind.DistinctBy or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy
            => output.OrderSensitive||output.ValuesUsed,
        PipelineStageKind.Distinct or PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect
            => output.OrderSensitive||output.ValuesUsed&&!ComparerExpressionSupport.IsDefaultEqualityComparer(stage.Value??"null",stage.OutputType),
        // グループの順・グループの中の順は、源の順で決まる。
        PipelineStageKind.GroupBy => output.OrderSensitive||output.ValuesUsed,
        _ => output.OrderSensitive,
    };

    // 要素ごとに利用者のラムダを評価する段か(値を使わない Select は評価しない)。
    private static bool EvaluatesLambdaPerElement(PipelineStage stage,Demand output) =>
        stage.Kind==PipelineStageKind.Select
            ? stage.Func is not null&&(output.ValuesUsed||stage.ContainsAnchor)
            : stage.Func is not null||stage.SecondFunc is not null;
}

// 段の性質(1 対 1 か、高々 1 個か、増えるか、溜めるか)。TakeGuard・TakeEntryGuard・DemandAnalysis が同じ表を使う。
internal enum StageCardinality {
    OneToOne,
    AtMostOne,
    Expanding,
    Buffering,
}

internal static class PipelineStageTraits {
    public static StageCardinality Cardinality(PipelineStage stage) => stage.Kind switch {
        PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable => StageCardinality.OneToOne,
        PipelineStageKind.Where or PipelineStageKind.OfType or PipelineStageKind.Take or PipelineStageKind.Skip
            or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.Distinct or PipelineStageKind.DistinctBy
            => StageCardinality.AtMostOne,
        PipelineStageKind.SelectMany or PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend
            or PipelineStageKind.Union or PipelineStageKind.UnionBy or PipelineStageKind.DefaultIfEmpty
            => StageCardinality.Expanding,
        _ => StageCardinality.Buffering,
    };

    // 1つの繰り返しの本体の中で、段が順に並ぶだけで描ける(1 対 1 か高々 1 個)。
    public static bool IsStreamingAtMostOne(PipelineStage stage) =>
        Cardinality(stage) is StageCardinality.OneToOne or StageCardinality.AtMostOne;

    // 要素ごとに利用者のラムダを呼ぶ段(添字つきの段は個別の数え方があるので含めない)。
    public static bool IsPlainLambdaStage(PipelineStage stage) =>
        stage.Func is not null
        &&!stage.UsesIndex
        &&stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.DistinctBy;
}
