using Seamless.FLinq.Generator;

namespace Test.SourceGenerator;

/// <summary>
/// 「下流が列のどこまで、どう使うか」の解析(<see cref="DemandAnalysis"/>)の単体テスト。
/// 段の並びと終端から逆向きに、各段の出口で使う範囲・値を使うか・順序に依存するか・止まるまでのラムダの数を求める。
/// </summary>
public sealed class DemandAnalysisTests {
    private static LambdaFunc Lambda(string body,bool anchor = false) => new() {
        ReturnTypeName="int",
        ParameterTypeNames=["int"],
        ParameterNames=["x"],
        CanInlineAssign=true,
        InlineBody=body,
        ContainsAnchor=anchor,
    };

    private static PipelineStage Stage(PipelineStageKind kind,string? value = null,FuncFragment? func = null) =>
        new(kind,func,value,"int","int");

    private static PipelineStage Select(bool anchor = false) => Stage(PipelineStageKind.Select,func: Lambda("x+1",anchor));
    private static PipelineStage Where() => Stage(PipelineStageKind.Where,func: Lambda("x>0"));
    private static PipelineStage Take(string count) => Stage(PipelineStageKind.Take,count);
    private static PipelineStage Skip(string count) => Stage(PipelineStageKind.Skip,count);
    private static PipelineStage OrderBy() => Stage(PipelineStageKind.OrderBy,func: Lambda("-x"));
    private static PipelineStage GroupBy() => Stage(PipelineStageKind.GroupBy,func: Lambda("x%3"));

    private static TerminalDemand Terminal(PipelineTerminalKind? kind,bool predicate = false) => new(kind,predicate,true);

    private static long Constant(CountExpr? count) {
        Assert.NotNull(count);
        Assert.True(count!.TryGetConstant(out var value));
        return value;
    }

    [Fact]
    public void FirstUsesOnlyTheFirstElement() {
        var result = DemandAnalysis.Analyze([Select()],Terminal(PipelineTerminalKind.First));
        Assert.Equal(DemandWindowKind.Prefix,result.Source.Window.Kind);
        Assert.Equal(1,Constant(result.Source.Window.End));
    }

    [Fact]
    public void TakeAfterSelectGivesAPrefixToTheGroupBy() {
        // GroupBy(k).Select(agg).Take(5).ToArray(): Select の出口は先頭 5 個。
        var result = DemandAnalysis.Analyze([GroupBy(),Select(),Take("5")],Terminal(PipelineTerminalKind.ToArray));
        Assert.Equal(DemandWindowKind.Prefix,result.After[1].Window.Kind);
        Assert.Equal(5,Constant(result.After[1].Window.End));
        // GroupBy はキーを全要素で計算する。
        Assert.True(result.Source.Window.IsAll);
    }

    [Fact]
    public void RuntimeTakeIsKeptAsAnExpression() {
        var result = DemandAnalysis.Analyze([Select(),Take("limit")],Terminal(PipelineTerminalKind.ToArray));
        var end = result.Source.Window.End;
        Assert.NotNull(end);
        Assert.False(end!.TryGetConstant(out _));
        Assert.Equal("global::System.Math.Max(0,(limit))",end.Render());
    }

    [Fact]
    public void SkipThenFirstIsARangeAndTakeOfTakeIsTheMinimum() {
        var range = DemandAnalysis.Analyze([Skip("2")],Terminal(PipelineTerminalKind.First));
        Assert.Equal(DemandWindowKind.Range,range.Source.Window.Kind);
        Assert.Equal(2,Constant(range.Source.Window.Start));
        Assert.Equal(3,Constant(range.Source.Window.End));
        var min = DemandAnalysis.Analyze([Take("10"),Take("3")],Terminal(PipelineTerminalKind.ToList));
        Assert.Equal(3,Constant(min.Source.Window.End));
    }

    [Fact]
    public void ReverseTurnsFirstIntoLast() {
        var result = DemandAnalysis.Analyze([Select(),Stage(PipelineStageKind.Reverse)],Terminal(PipelineTerminalKind.FirstOrDefault));
        Assert.Equal(DemandWindowKind.Suffix,result.After[0].Window.Kind);
        Assert.Equal(DemandWindowKind.Suffix,result.Source.Window.Kind);
    }

    [Fact]
    public void WhereMakesTheWindowUnknownAndCountsTheLambda() {
        var result = DemandAnalysis.Analyze([OrderBy(),Where()],Terminal(PipelineTerminalKind.Any));
        Assert.Equal(DemandWindowKind.Unknown,result.After[0].Window.Kind);
        Assert.Equal(1,result.After[0].LambdasBeforeStop);
        // OrderBy の後ろで Where が途中で止まるので、OrderBy は消せない(System.Linq と別の要素に述語を評価する)。
        Assert.False(result.After[0].AllowsDroppingOrder);
        // 並べ替えは全要素の鍵を計算する。
        Assert.True(result.Source.Window.IsAll);
    }

    [Fact]
    public void OrderBeforeCountCanBeDropped() {
        var result = DemandAnalysis.Analyze([OrderBy(),Select()],Terminal(PipelineTerminalKind.Count));
        Assert.True(result.After[0].AllowsDroppingOrder);
        Assert.False(result.After[0].ValuesUsed);
    }

    [Fact]
    public void OrderBeforeAnyPredicateCannotBeDropped() {
        var result = DemandAnalysis.Analyze([OrderBy()],Terminal(PipelineTerminalKind.Any,predicate: true));
        Assert.False(result.After[0].AllowsDroppingOrder);
        var plain = DemandAnalysis.Analyze([OrderBy()],Terminal(PipelineTerminalKind.Any));
        Assert.True(plain.After[0].AllowsDroppingOrder);
    }

    [Fact]
    public void SelectValuesAreUnusedBeforeAnyUnlessAnchored() {
        Assert.False(DemandAnalysis.Analyze([Select()],Terminal(PipelineTerminalKind.Any)).Source.ValuesUsed);
        Assert.True(DemandAnalysis.Analyze([Select(anchor: true)],Terminal(PipelineTerminalKind.Any)).Source.ValuesUsed);
    }

    [Fact]
    public void FirstIsOrderSensitiveButCountIsNot() {
        Assert.True(DemandAnalysis.Analyze([Select()],Terminal(PipelineTerminalKind.First)).Source.OrderSensitive);
        Assert.False(DemandAnalysis.Analyze([Select()],Terminal(PipelineTerminalKind.Count)).Source.OrderSensitive);
        Assert.True(DemandAnalysis.Analyze([Take("3")],Terminal(PipelineTerminalKind.Count)).Source.OrderSensitive);
    }

    [Fact]
    public void PrependConsumesOneLessFromTheSource() {
        var result = DemandAnalysis.Analyze([Select(),Stage(PipelineStageKind.Prepend,"0"),Take("3")],Terminal(PipelineTerminalKind.ToArray));
        Assert.Equal(2,Constant(result.After[0].Window.End));
    }

    [Fact]
    public void TakeWhileStopsEarlyEvenIfEverythingDownstreamIsUsed() {
        // GroupBy(k).Select(agg).TakeWhile(p).ToArray(): TakeWhile が止まった後のグループの集約は評価しない(融合してはいけない)。
        var result = DemandAnalysis.Analyze([GroupBy(),Select(),Stage(PipelineStageKind.TakeWhile,func: Lambda("x>0"))],Terminal(PipelineTerminalKind.ToArray));
        Assert.Equal(DemandWindowKind.Unknown,result.After[1].Window.Kind);
    }

    [Fact]
    public void NoTerminalIsUnknown() {
        var result = DemandAnalysis.Analyze([Select()],Terminal(null));
        Assert.Equal(DemandWindowKind.Unknown,result.Source.Window.Kind);
        Assert.True(result.Source.ValuesUsed);
    }
}
