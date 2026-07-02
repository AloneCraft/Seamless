using UI;

namespace Test.Benchmark;

public sealed class BenchmarkTabProgressLogic{
    [Fact]
    public void CreateCellsFromSelectedCases_UsesRunSelectionSnapshotForSourceTab(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);

        var cells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases(
            [
                Case("System.Array.FLinq","System.Array","Where","Count=4096"),
                Case("System.Array.ZLinq","System.Array","Where","Count=4096"),
                Case("System.List.FLinq","System.Collections.Generic.List","Where","Count=4096"),
                Case("System.Array.FLinq","System.Array","Where","Count=4096"),
            ],
            sourceTab);

        Assert.Equal(2,cells.Count);
        Assert.All(cells,static cell => Assert.True(cell.IsSelected));
        Assert.Contains(cells,static cell => cell.Project=="System.Array.FLinq");
        Assert.Contains(cells,static cell => cell.Project=="System.Array.ZLinq");
        Assert.Single(cells.Select(static cell => cell.PairKey).Distinct(StringComparer.OrdinalIgnoreCase));
        Assert.All(cells,static cell => Assert.False(string.IsNullOrWhiteSpace(cell.PairKey)));
    }

    [Fact]
    public void Calculate_UsesSelectedCellsAsDenominator(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
                new BenchmarkTabProgressCell("Where","Right",false),
                new BenchmarkTabProgressCell("Select","Left",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Completed",1_000_000),
                new BenchmarkElementMeasurement("Right","Where",4096,"Completed",100),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(1,progress.Completed);
        Assert.Equal(2,progress.Total);
        Assert.Equal("1/2",progress.FractionText);
        Assert.Equal(0.5,progress.Fraction);
    }

    [Fact]
    public void Calculate_DoesNotCompleteShortCompletedCellsWithoutPairThreshold(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
                new BenchmarkTabProgressCell("Where","Right",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Completed",100),
                new BenchmarkElementMeasurement("Right","Where",4096,"Completed",200),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(0,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_TreatsFailedCellsAsTerminal(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Failed",null),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(1,progress.Completed);
        Assert.Equal(1,progress.Total);
    }

    [Fact]
    public void Calculate_TreatsAdaptiveThresholdStopAsTerminalForSelectedPairCells(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
                new BenchmarkTabProgressCell("Where","Right",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Right","Where",4096,"Completed",100),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(2,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_TreatsRightThresholdAsPairCompletionWhenPairKeyMatches(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
                new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            ],
            [
                new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100),
                new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(2,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_DoesNotCompleteOtherRowWithDifferentPairKey(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
                new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-2"),
            ],
            [
                new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100),
                new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(1,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_DoesNotCompleteDifferentScalableTypeWithSameMethod(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Add","Scalable.Persisted.List","Scalable.Persisted.Collections.List.ListString",true),
            ],
            [
                new BenchmarkElementMeasurement("Scalable.Persisted.List","Add","Scalable.Persisted.Collections.List.ListInt32",4096,"Completed",100),
            ],
            "Scalable.Persisted.List",
            "Scalable.Persisted.List.Previous",
            8192,
            1_000_000);

        Assert.Equal(0,progress.Completed);
        Assert.Equal(1,progress.Total);
    }

    [Fact]
    public void Calculate_DoesNotCompleteDifferentArgumentKeyWithSameMethodAndType(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Add","Left","Type","Count=2",true),
                new BenchmarkTabProgressCell("Add","Right","Type","Count=2",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Add","Type","Count=1",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Right","Add","Type","Count=1",4096,"Completed",100),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(0,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    private static BenchmarkCase Case(string project,string type,string method,string argumentKey) =>
        new(project,"Test.Assembly",type,method,argumentKey,[]);
}



