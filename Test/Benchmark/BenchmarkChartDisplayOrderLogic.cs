using UI;

namespace Test.Benchmark;

public sealed class BenchmarkChartDisplayOrderLogic{
    [Fact]
    public void CreateTransposedSourceOrder_ReversesGridOrderForTopToBottomDisplay(){
        var gridOrder = new[] {
            "AggregateBy_keySelector",
            "Aggregate_func",
            "Aggregate_seed_func",
        };

        var chartSourceOrder = global::UI.BenchmarkChartDisplayOrderLogic.CreateTransposedSourceOrder(gridOrder);
        var chartTopToBottomOrder = chartSourceOrder.Reverse().ToArray();

        Assert.Equal(["Aggregate_seed_func","Aggregate_func","AggregateBy_keySelector"],chartSourceOrder);
        Assert.Equal(gridOrder,chartTopToBottomOrder);
    }
}
