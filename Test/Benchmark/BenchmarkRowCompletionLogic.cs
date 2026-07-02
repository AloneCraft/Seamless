using UI;

namespace Test.Benchmark;

public sealed class BenchmarkRowCompletionLogic{
    [Fact]
    public void IsFinalCompleted_TreatsRightThresholdAsPairCompletion(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000),
        };

        Assert.True(global::UI.BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            4096,
            1_000_000));
    }

    [Fact]
    public void IsFinalCompleted_DoesNotCompleteWhenBothSidesStayBelowThreshold(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Any.Left","Any","System.Array.FLinq","",4096,"Completed",100),
            new BenchmarkElementMeasurement("Any.Right","Any","System.Array.ZLinq","",4096,"Completed",200),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Any.Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Any.Right","System.Array.ZLinq","",true,"row-1"),
            8192,
            1_000_000));
    }

    [Fact]
    public void IsFinalCompleted_DoesNotCompleteDifferentArgument(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","Count=1",4096,"Completed",100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","Count=1",4096,"Completed",2_000_000),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","Count=2",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","Count=2",true,"row-1"),
            8192,
            1_000_000));
    }
}
