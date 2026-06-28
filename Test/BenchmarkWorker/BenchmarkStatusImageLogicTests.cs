using Benchmark;

namespace Test.BenchmarkWorker;

public sealed class BenchmarkStatusImageLogicTests{
    [Fact]
    public void ProjectStatusUsesProgressCheckForMeasuredValueBeforeFinalCompletion(){
        var kind = BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
            HasCase:true,
            IsRunning:false,
            IsFailed:false,
            HasMeasuredValue:true,
            IsCompletedWithoutMeasurement:false,
            IsSelected:true,
            IsRowFinalCompleted:false));

        Assert.Equal(BenchmarkStatusImageKind.Completed,kind);
        Assert.Equal(BenchmarkStatusImageNames.Completed,BenchmarkStatusImageNames.FromKind(kind));
    }

    [Fact]
    public void ProjectStatusUsesDoubleCheckOnlyForFinalCompletedRows(){
        var kind = BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
            HasCase:true,
            IsRunning:false,
            IsFailed:false,
            HasMeasuredValue:true,
            IsCompletedWithoutMeasurement:false,
            IsSelected:true,
            IsRowFinalCompleted:true));

        Assert.Equal(BenchmarkStatusImageKind.FinalCompleted,kind);
        Assert.Equal(BenchmarkStatusImageNames.FinalCompleted,BenchmarkStatusImageNames.FromKind(kind));
    }

    [Fact]
    public void ProjectStatusKeepsFailureSeparateFromFinalCompletion(){
        var kind = BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
            HasCase:true,
            IsRunning:false,
            IsFailed:true,
            HasMeasuredValue:true,
            IsCompletedWithoutMeasurement:false,
            IsSelected:true,
            IsRowFinalCompleted:true));

        Assert.Equal(BenchmarkStatusImageKind.Failed,kind);
    }

    [Fact]
    public void RowCompletionUsesThresholdOrMaxElementCompletion(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Aggregate",4096,"Completed",900_000),
            new BenchmarkElementMeasurement("Right","Aggregate",4096,"Completed",1_500_000),
        };

        Assert.True(BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            "Aggregate",
            "Left",
            "Right",
            8192,
            1_000_000));

        Assert.False(BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            "Aggregate",
            "Left",
            "Right",
            8192,
            2_000_000));

        measurements=[
            new BenchmarkElementMeasurement("Left","Aggregate",8192,"Completed",900_000),
            new BenchmarkElementMeasurement("Right","Aggregate",8192,"Completed",950_000),
        ];

        Assert.True(BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            "Aggregate",
            "Left",
            "Right",
            8192,
            2_000_000));
    }

    [Fact]
    public void CpuStatusUsesImagesWithoutStatusGlyphText(){
        Assert.Equal(BenchmarkStatusImageNames.CpuIdle,BenchmarkCpuStatusImageLogic.GetImageSource("Pending"));
        Assert.Equal(BenchmarkStatusImageNames.CpuRunning,BenchmarkCpuStatusImageLogic.GetImageSource(JobStatus.Running.ToString()));
        Assert.Equal(BenchmarkStatusImageNames.Completed,BenchmarkCpuStatusImageLogic.GetImageSource("Completed"));
        Assert.Equal(BenchmarkStatusImageNames.Failed,BenchmarkCpuStatusImageLogic.GetImageSource("Failed"));
        Assert.Equal(BenchmarkCpuCoreImageNames.Performance,BenchmarkCpuCoreImageNames.FromCoreKind(CpuCoreKind.Performance));
        Assert.Equal(BenchmarkCpuCoreImageNames.Efficiency,BenchmarkCpuCoreImageNames.FromCoreKind(CpuCoreKind.Efficiency));
        Assert.EndsWith(".png",BenchmarkStatusImageNames.Waiting,StringComparison.Ordinal);
        Assert.EndsWith(".png",BenchmarkCpuCoreImageNames.Performance,StringComparison.Ordinal);
    }

    [Fact]
    public void CpuGridWaitingCellUsesIconWithoutText(){
        Assert.Equal(string.Empty,BenchmarkCpuStatusImageLogic.GetCellStatusText("待機",string.Empty));
        Assert.Equal("Count",BenchmarkCpuStatusImageLogic.GetCellStatusText("Running","Count"));
        Assert.Equal(BenchmarkStatusImageNames.CpuIdle,BenchmarkCpuStatusImageLogic.GetImageSource("待機"));
    }
}
