using UI;

namespace Test.Benchmark;

public sealed class BenchmarkRowCompletionLogic{
    [Fact]
    public void IsFinalCompleted_TreatsRightThresholdAsPairCompletion(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000,2_000_000),
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
            new BenchmarkElementMeasurement("Any.Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Any.Right","Any","System.Array.ZLinq","",4096,"Completed",200,200),
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
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","Count=1",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","Count=1",4096,"Completed",2_000_000,2_000_000),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","Count=2",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","Count=2",true,"row-1"),
            8192,
            1_000_000));
    }

    [Fact]
    public void IsTabFinalCompleted_TreatsThresholdStoppedPairAsCompleted(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",4_200_000_000,4_200_000_000),
        };
        var cells = new[] {
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
        };

        Assert.True(global::UI.BenchmarkRowCompletionLogic.IsTabFinalCompleted(
            measurements,
            cells,
            8192,
            4_096_000_000));
    }

    [Fact]
    public void IsTabFinalCompleted_DoesNotCompleteWhenSelectedCellHasNoTerminalMeasurement(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",200,200),
        };
        var cells = new[] {
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsTabFinalCompleted(
            measurements,
            cells,
            8192,
            4_096_000_000));
    }

    [Fact]
    public void IsTabFinalCompleted_DoesNotCompleteWhenAnyProvidedBenchmarkCellIsMissing(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",4_200_000_000,4_200_000_000),
        };
        var cells = new[] {
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Where","Left","System.Array.FLinq","",true,"row-2"),
            new BenchmarkTabProgressCell("Where","Right","System.Array.ZLinq","",true,"row-2"),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsTabFinalCompleted(
            measurements,
            cells,
            8192,
            4_096_000_000));
    }

    [Fact]
    public void IsLoopCountFinalCompleted_RequiresBothTotalsAtSameLoopCount(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4,"Completed",25,1_100_000),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4,"Completed",30,900_000),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",8,"Completed",30,1_200_000),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsLoopCountFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            1_000_000));
    }

    [Fact]
    public void IsLoopCountFinalCompleted_UsesTotalTimeEvenWhenDisplayedTimeIsNanoseconds(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",8,"Completed",25,1_100_000),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",8,"Completed",30,1_200_000),
        };

        Assert.True(global::UI.BenchmarkRowCompletionLogic.IsLoopCountFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            1_000_000));
    }

    [Fact]
    public void IsLoopCountFinalCompleted_ToHashSetReachesOneMillisecondAtFourCalls(){
        const int loopCount=4;
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","ToHashSet","System.Array.FLinq","",loopCount,"Completed",332_000,1_328_000),
            new BenchmarkElementMeasurement("Right","ToHashSet","System.Array.ZLinq","",loopCount,"Completed",114_000,456_000),
        };

        Assert.True(global::UI.BenchmarkRowCompletionLogic.IsLoopCountFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("ToHashSet","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("ToHashSet","Right","System.Array.ZLinq","",true,"row-1"),
            1_000_000));
    }

    [Fact]
    public void IsLoopCountFinalCompleted_DoesNotTreatFailureAsComplete(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",8,"Completed",25,1_100_000),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",8,"Failed",null,1_200_000),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsLoopCountFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            1_000_000));
    }

    [Fact]
    public void IsLoopCountFinalCompleted_DoesNotTreatLegacyMeasurementWithoutTotalAsComplete(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",8,"Completed",2_000_000),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",8,"Completed",2_000_000),
        };

        Assert.False(global::UI.BenchmarkRowCompletionLogic.IsLoopCountFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            1_000_000));
    }
}
