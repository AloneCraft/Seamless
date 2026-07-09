using Seamless.UI.Storage;

namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkUiRunRepository{
    [Fact]
    public async Task QueueSaveBenchmarkSnapshotItems_RestoresResultItemsFromDatabase(){
        var store = new global::Seamless.UI.Storage.UiRunStore(CreateDatabasePath());
        var repository = new global::UI.BenchmarkUiRunRepository(store);
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var runId = await repository.CreateRunAsync(UiRunAppKind.Benchmark,"Benchmark","Fast",sourceTab.Tab);
        var result = Result(sourceTab.LeftProject,sourceTab.Tab,"Aggregate",64,123.5,"Sample.Type","Count=64",[new global::UI.BenchmarkArgument("Count","64")]);
        repository.QueueSaveRunSettings(runId,string.Empty,[64],TimeSpan.Zero,[]);

        repository.QueueSaveBenchmarkSnapshotItems(runId,[
            Snapshot(runId!.Value,sourceTab,"Sample.Type","Aggregate",7,CompletedSide(result),EmptySide(sourceTab.RightProject,sourceTab.Tab,"Sample.Type","Aggregate")),
        ]);

        await repository.FlushAsync();
        Assert.Contains(store.ListMetrics(runId!.Value),static item => item.MetricName=="MeanNanoseconds");
        var loaded = repository.LoadResultItems(runId!.Value,DateTimeOffset.UtcNow,sourceTab)
            .Single(static item => item.MeanNanoseconds is not null);
        Assert.Equal(sourceTab.LeftProject,loaded.Project);
        Assert.Equal("Sample.Type",loaded.TypeFullName);
        Assert.Equal(global::UI.BenchmarkWorkloadAxis.ResolveElementCount(sourceTab.Tab,64),loaded.ElementCount);
        Assert.Equal(123.5,loaded.MeanNanoseconds);
        Assert.Equal(7,loaded.AssignedLogicalCpu);
    }

    [Fact]
    public async Task QueueSaveBenchmarkSnapshotItems_ReplacesMeasurementForSameGridRow(){
        var store = new global::Seamless.UI.Storage.UiRunStore(CreateDatabasePath());
        var repository = new global::UI.BenchmarkUiRunRepository(store);
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var runId = await repository.CreateRunAsync(UiRunAppKind.Benchmark,"Benchmark","Fast",sourceTab.Tab);
        repository.QueueSaveRunSettings(runId,string.Empty,[64],TimeSpan.Zero,[]);

        repository.QueueSaveBenchmarkSnapshotItems(runId,[
            Snapshot(runId!.Value,sourceTab,"Sample.Type","Aggregate",7,CompletedSide(Result(sourceTab.LeftProject,sourceTab.Tab,"Aggregate",64,100,"Sample.Type","Count=64",[new global::UI.BenchmarkArgument("Count","64")])),EmptySide(sourceTab.RightProject,sourceTab.Tab,"Sample.Type","Aggregate")),
        ]);
        await repository.FlushAsync();
        Assert.Contains(store.ListMetrics(runId!.Value),static item => item.MetricName=="MeanNanoseconds"&&Math.Abs(item.NumericValue-100)<0.001);

        repository.QueueSaveBenchmarkSnapshotItems(runId,[
            Snapshot(runId!.Value,sourceTab,"Sample.Type","Aggregate",7,CompletedSide(Result(sourceTab.LeftProject,sourceTab.Tab,"Aggregate",64,250,"Sample.Type","Count=64",[new global::UI.BenchmarkArgument("Count","64")])),EmptySide(sourceTab.RightProject,sourceTab.Tab,"Sample.Type","Aggregate")),
        ]);

        await repository.FlushAsync();
        var metrics = store.ListMetrics(runId!.Value).Where(static item => item.MetricName=="MeanNanoseconds").ToArray();
        Assert.Single(metrics);
        Assert.Equal(250,metrics[0].NumericValue);
    }

    [Fact]
    public async Task LoadResultItems_UsesStoredGridRowTypeAndBenchmark(){
        var store = new global::Seamless.UI.Storage.UiRunStore(CreateDatabasePath());
        var repository = new global::UI.BenchmarkUiRunRepository(store);
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.ScalableTsavoriteSet);
        var runId = await repository.CreateRunAsync(UiRunAppKind.ScalableBenchmark,"Scalable","Fast",sourceTab.Tab);
        var result = Result(sourceTab.LeftProject,sourceTab.Tab,"Add",262144,500,"SetInt32",string.Empty,[]);
        repository.QueueSaveRunSettings(runId,string.Empty,[262144],TimeSpan.Zero,[]);

        repository.QueueSaveBenchmarkSnapshotItems(runId,[
            Snapshot(runId!.Value,sourceTab,"SetInt32","Add",7,CompletedSide(result),EmptySide(sourceTab.RightProject,sourceTab.Tab,"SetInt32","Add")),
        ]);

        await repository.FlushAsync();
        Assert.Contains(store.ListMetrics(runId!.Value),static item => item.MetricName=="MeanNanoseconds");
        var loaded = repository.LoadResultItems(runId!.Value,DateTimeOffset.UtcNow,sourceTab)
            .Single(static item => item.MeanNanoseconds is not null);
        Assert.Equal("SetInt32",loaded.TypeFullName);
        Assert.Equal("Add",loaded.Method);
        Assert.Empty(loaded.ArgumentKey);
    }

    private static global::UI.BenchmarkGridSnapshotItem Snapshot(
        long runId,
        global::UI.BenchmarkSourceTabDefinition sourceTab,
        string typeName,
        string benchmarkName,
        int? cpuNumber,
        global::UI.BenchmarkGridSnapshotSide left,
        global::UI.BenchmarkGridSnapshotSide right) =>
        new(runId,0,sourceTab.Id,typeName,benchmarkName,cpuNumber,true,left,right);

    private static global::UI.BenchmarkGridSnapshotSide CompletedSide(global::UI.BenchmarkResultItem result) =>
        new(UiRunStatus.Completed,string.Empty,result.StartedAt,result.MeanNanoseconds,result.AllocatedBytesValue,result);

    private static global::UI.BenchmarkGridSnapshotSide EmptySide(
        string project,
        global::UI.BenchmarkSourceTab sourceTab,
        string typeName,
        string benchmarkName) =>
        new(UiRunStatus.Queued,string.Empty,null,null,null,Result(project,sourceTab,benchmarkName,0,0,typeName,string.Empty,[]));

    private static global::UI.BenchmarkResultItem Result(
        string project,
        global::UI.BenchmarkSourceTab sourceTab,
        string method,
        int elementCount,
        double mean,
        string typeFullName,
        string argumentKey,
        IReadOnlyList<global::UI.BenchmarkArgument>? arguments = null) =>
        new(new global::UI.BenchmarkJob{
            Project=project,
            MethodName=method,
            TypeFullName=typeFullName,
            SourceTab=sourceTab,
            ElementCount=elementCount,
            ArgumentKey=argumentKey,
            Arguments=arguments?.ToList()??[],
            Status=global::UI.JobStatus.Completed,
            MeanNanoseconds=mean,
            AllocatedBytes=1024,
            StartedAt=DateTimeOffset.UtcNow,
            AssignedLogicalCpu=7,
            ExitCode=0,
        });

    private static string CreateDatabasePath(){
        var root = Path.Combine(AppContext.BaseDirectory,"BenchmarkUiRunRepositoryTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root,"runs.db");
    }
}



