namespace UI;

public sealed class BenchmarkTabProgressLogicTests{
    [Fact]
    public void Calculate_UsesSelectedCellsAsDenominator(){
        var progress = BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
                new BenchmarkTabProgressCell("Where","Right",false),
                new BenchmarkTabProgressCell("Select","Left",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Completed",100),
                new BenchmarkElementMeasurement("Right","Where",4096,"Completed",100),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(1,progress.Completed);
        Assert.Equal(2,progress.Total);
        Assert.Equal("1/2",progress.FractionText);
        Assert.Equal("[####----]",progress.GaugeText);
    }

    [Fact]
    public void Calculate_TreatsFailedCellsAsTerminal(){
        var progress = BenchmarkTabProgressLogic.Calculate(
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
        var progress = BenchmarkTabProgressLogic.Calculate(
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
}
