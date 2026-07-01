using Seamless.UI.Storage;

namespace Test.Benchmark;

public sealed class UiRunStoreTests{
    [Fact]
    public void CreateRun_InitializesSchemaAndListsRun(){
        var path = CreateDatabasePath();
        var store = new UiRunStore(path);

        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark run",
            "Fast",
            "SystemArray",
            DateTimeOffset.UtcNow));

        var runs = store.ListRuns();

        Assert.Equal(UiRunStatus.Queued,run.Status);
        var loaded = Assert.Single(runs);
        Assert.Equal(run.RunId,loaded.RunId);
        Assert.Equal(UiRunAppKind.Benchmark,loaded.AppKind);
        Assert.Equal("Benchmark run",loaded.DisplayName);
    }

    [Fact]
    public void UpsertCaseJobAndMeasurement_RoundTripsNormalizedMetrics(){
        var path = CreateDatabasePath();
        var store = new UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.ScalableBenchmark,
            "Scalable",
            "Accurate",
            "ScalableSet",
            DateTimeOffset.UtcNow));
        var testCase = store.UpsertCase(
            run.RunId,
            "Aggregate|N=64",
            "Aggregate",
            "Left",
            "Aggregate",
            "Sample.Type",
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
                ["Count"]="64",
            });
        var job = store.UpsertJob(
            run.RunId,
            testCase.CaseId,
            "Aggregate|Left|64|0",
            UiRunStatus.Completed,
            64,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "Completed",
            string.Empty);

        store.SaveMeasurement(job.JobId,[
            new("MeanNanoseconds",123.5,"ns"),
            new("AllocatedBytes",4096,"bytes"),
        ]);
        store.SetRunStatus(run.RunId,UiRunStatus.Completed,"done");

        var metrics = store.ListMetrics(run.RunId);
        var loadedRun = Assert.Single(store.ListRuns(UiRunAppKind.ScalableBenchmark));

        Assert.Equal(UiRunStatus.Completed,loadedRun.Status);
        Assert.Contains(metrics,item => item.MetricName=="MeanNanoseconds"&&Math.Abs(item.NumericValue-123.5)<0.001);
        Assert.Contains(metrics,item => item.MetricName=="AllocatedBytes"&&Math.Abs(item.NumericValue-4096)<0.001);
    }

    [Fact]
    public void UiState_RoundTripsSelectionsAndViewState(){
        var path = CreateDatabasePath();
        var store = new UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.ParameterSweep,
            "Sweep",
            "Default",
            "ParameterSweep",
            DateTimeOffset.UtcNow));

        store.SaveUiSelection(run.RunId,"cell","HashSet|Int32",true);
        store.SaveUiSelection(run.RunId,"cell","Dictionary|String",false);
        store.SaveUiViewState(run.RunId,"SelectedLowerTab","Chart");

        var selections = store.ListUiSelections(run.RunId);
        var viewStates = store.ListUiViewStates(run.RunId);

        Assert.Contains(selections,item => item.SelectionKey=="HashSet|Int32"&&item.IsSelected);
        Assert.Contains(selections,item => item.SelectionKey=="Dictionary|String"&&!item.IsSelected);
        var viewState = Assert.Single(viewStates);
        Assert.Equal("SelectedLowerTab",viewState.StateKey);
        Assert.Equal("Chart",viewState.StateValue);
    }

    private static string CreateDatabasePath(){
        var root = Path.Combine(AppContext.BaseDirectory,"UiRunStoreTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root,"runs.db");
    }
}
