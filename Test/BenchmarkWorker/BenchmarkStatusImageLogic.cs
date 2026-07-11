using UI;

namespace Test.BenchmarkWorker;

public sealed class BenchmarkStatusImageLogic{
    [Fact]
    public void ProjectStatusUsesProgressCheckForMeasuredValueBeforeFinalCompletion(){
        var kind = global::UI.BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
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
        var kind = global::UI.BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
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
        var kind = global::UI.BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
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
    public void ProjectStatusUsesRunningBeforeCompletedWithoutMeasurement(){
        var kind = global::UI.BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
            HasCase:true,
            IsRunning:true,
            IsFailed:false,
            HasMeasuredValue:false,
            IsCompletedWithoutMeasurement:true,
            IsSelected:true,
            IsRowFinalCompleted:false));

        Assert.Equal(BenchmarkStatusImageKind.Running,kind);
        Assert.Equal(BenchmarkStatusImageNames.CpuRunning,BenchmarkStatusImageNames.FromKind(kind));
    }

    [Fact]
    public void ProjectStatusUsesRunningBeforeMeasuredCompletion(){
        var kind = global::UI.BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
            HasCase:true,
            IsRunning:true,
            IsFailed:false,
            HasMeasuredValue:true,
            IsCompletedWithoutMeasurement:false,
            IsSelected:true,
            IsRowFinalCompleted:true));

        Assert.Equal(BenchmarkStatusImageKind.Running,kind);
        Assert.Equal(BenchmarkStatusImageNames.CpuRunning,BenchmarkStatusImageNames.FromKind(kind));
    }

    [Fact]
    public void RowCompletionUsesThresholdOrMaxElementCompletion(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Aggregate","","",4096,"Completed",900_000,900_000),
            new BenchmarkElementMeasurement("Right","Aggregate","","",4096,"Completed",1_500_000,1_500_000),
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
            new BenchmarkElementMeasurement("Left","Aggregate","","",8192,"Completed",900_000,900_000),
            new BenchmarkElementMeasurement("Right","Aggregate","","",8192,"Completed",950_000,950_000),
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
    public void RowCompletionUsesPairKeyWhenRightSideReachesThreshold(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000,2_000_000),
        };

        Assert.True(BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            4096,
            1_000_000));

        Assert.False(BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","Count=2",true,"row-1"),
            8192,
            1_000_000));
    }

    [Fact]
    public void RowCompletionDoesNotCompleteWhenBothSidesStayBelowThreshold(){
        var measurements = new[] {
            new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",200,200),
        };

        Assert.False(BenchmarkRowCompletionLogic.IsFinalCompleted(
            measurements,
            new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
            new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            8192,
            1_000_000));
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
    public void FailureTooltipUsesFirstNonEmptyReasonLine(){
        var tooltip = BenchmarkFailureDisplayLogic.CreateTooltipReason(
            "\r\nSystem.MissingMethodException: Method 'CaptureBenchmarkMethodResult' not found.\r\n   at Sample.Type.Method()");

        Assert.Equal("\u7406\u7531: System.MissingMethodException: Method 'CaptureBenchmarkMethodResult' not found.",tooltip);
    }

    [Fact]
    public void FailureTooltipFallsBackWhenReasonIsEmpty(){
        var tooltip = BenchmarkFailureDisplayLogic.CreateTooltipReason(" \r\n ");

        Assert.Equal("\u7406\u7531: "+BenchmarkFailureDisplayLogic.DefaultFailureReason,tooltip);
    }

    [Fact]
    public void FailureLogKeepsFullReasonText(){
        var reason = "System.InvalidOperationException: boom\r\n   at Sample.Type.Method()";
        var log = BenchmarkFailureDisplayLogic.CreateLogText(
            "Scalable.Tsavorite.Dictionary",
            "Add",
            65536,
            0,
            "artifact-folder",
            reason,
            "Benchmark.Scalable.Persisted.Collections.Dictionary.DictionaryStringInt32");

        Assert.Contains("Benchmark failed.",log,StringComparison.Ordinal);
        Assert.Contains("Project=Scalable.Tsavorite.Dictionary",log,StringComparison.Ordinal);
        Assert.Contains("Type=Benchmark.Scalable.Persisted.Collections.Dictionary.DictionaryStringInt32",log,StringComparison.Ordinal);
        Assert.Contains("N=65536",log,StringComparison.Ordinal);
        Assert.Contains("CPU=0",log,StringComparison.Ordinal);
        Assert.Contains("Artifact=artifact-folder",log,StringComparison.Ordinal);
        Assert.Contains(reason,log,StringComparison.Ordinal);
    }

    [Fact]
    public void CpuGridWaitingCellUsesIconWithoutText(){
        Assert.Equal(string.Empty,BenchmarkCpuStatusImageLogic.GetCellStatusText(JobStatus.Pending.ToString(),string.Empty));
        Assert.Equal("Count",BenchmarkCpuStatusImageLogic.GetCellStatusText("Running","Count"));
        Assert.Equal(BenchmarkStatusImageNames.CpuIdle,BenchmarkCpuStatusImageLogic.GetImageSource(JobStatus.Pending.ToString()));
    }

}


