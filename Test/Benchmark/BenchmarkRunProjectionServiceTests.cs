namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkRunProjectionServiceTests{
    [Fact]
    public void GetVisibleResults_KeepsUnscopedRowsAndSelectedElementCount(){
        var service = new global::UI.BenchmarkRunProjectionService();
        var results = new[] {
            Result("System.Array.FLinq","Aggregate",0,10),
            Result("System.Array.FLinq","Aggregate",16,20),
            Result("System.Array.FLinq","Aggregate",64,30),
        };

        var visible = service.GetVisibleResults(results,16);

        Assert.Equal([0,16],visible.Select(static item => item.ElementCount).ToArray());
    }

    [Fact]
    public void BuildComparisonRowsForElementCount_UsesCompletedResultsOnly(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var session = new global::UI.BenchmarkRunSession();
        var service = new global::UI.BenchmarkRunProjectionService();
        session.UpsertResult(Result(sourceTab.LeftProject,"Aggregate",64,100));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",64,50));
        session.UpsertResult(Failed(sourceTab.LeftProject,"Aggregate",128));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",128,25));

        var rows = service.BuildComparisonRowsForElementCount(session,sourceTab,64,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal("Aggregate",row.MethodName);
        Assert.Equal(100,row.Left.TimeNanoseconds);
        Assert.Equal(50,row.Right.TimeNanoseconds);
        Assert.Equal(2,row.PairRatio.TimeRatio.NumericValue);
    }

    [Fact]
    public void BuildLatestComparisonRows_UsesLatestCompletedResultPerProject(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var session = new global::UI.BenchmarkRunSession();
        var service = new global::UI.BenchmarkRunProjectionService();
        session.UpsertResult(Result(sourceTab.LeftProject,"Aggregate",16,100,DateTimeOffset.UtcNow.AddMinutes(-2)));
        session.UpsertResult(Result(sourceTab.LeftProject,"Aggregate",64,80,DateTimeOffset.UtcNow.AddMinutes(-1)));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",64,40,DateTimeOffset.UtcNow));

        var rows = service.BuildLatestComparisonRows(session,sourceTab,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal(80,row.Left.TimeNanoseconds);
        Assert.Equal(40,row.Right.TimeNanoseconds);
    }

    private static global::UI.ReportRow[] CreateRows(
        global::UI.BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyList<global::UI.BenchmarkResultItem> results) =>
        results.Select(static item => new global::UI.ReportRow(
            item.Project,
            item.Method,
            item.TypeFullName,
            item.Method,
            item.Status,
            item.Mean,
            item.ErrorNanoseconds?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.StdDevNanoseconds?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.AllocatedBytesValue?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            item.MeanNanoseconds??0,
            item.ErrorNanoseconds??0,
            item.StdDevNanoseconds??0,
            item.AllocatedBytesValue??0,
            0,
            0,
            item.AssignedLogicalCpu?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            item.StartedAt?.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.StartedAt?.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.ArtifactFolder,
            item.ExitCode,
            item.ArgumentKey,
            item.Arguments)).ToArray();

    private static global::UI.BenchmarkResultItem Result(
        string project,
        string method,
        int elementCount,
        double mean,
        DateTimeOffset? startedAt = null) =>
        new(new global::UI.BenchmarkJob{
            Project=project,
            MethodName=method,
            TypeFullName="Sample.Type",
            SourceTab=global::UI.BenchmarkSourceTabs.TryGetByProject(project)?.Tab??global::UI.BenchmarkSourceTab.SystemArray,
            ElementCount=elementCount,
            Status=global::UI.JobStatus.Completed,
            MeanNanoseconds=mean,
            AllocatedBytes=128,
            StartedAt=startedAt??DateTimeOffset.UtcNow,
            ExitCode=0,
        });

    private static global::UI.BenchmarkResultItem Failed(string project,string method,int elementCount) =>
        new(new global::UI.BenchmarkJob{
            Project=project,
            MethodName=method,
            TypeFullName="Sample.Type",
            SourceTab=global::UI.BenchmarkSourceTabs.TryGetByProject(project)?.Tab??global::UI.BenchmarkSourceTab.SystemArray,
            ElementCount=elementCount,
            Status=global::UI.JobStatus.Failed,
            StartedAt=DateTimeOffset.UtcNow,
            ExitCode=1,
        });
}
