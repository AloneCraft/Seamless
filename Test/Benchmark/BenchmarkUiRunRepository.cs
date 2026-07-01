using Seamless.UI.Storage;

namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkUiRunRepository{
    [Fact]
    public async Task QueueSaveResult_RestoresResultItemsFromDatabase(){
        var store = new global::Seamless.UI.Storage.UiRunStore(CreateDatabasePath());
        var repository = new global::UI.BenchmarkUiRunRepository(store);
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var runId = await repository.CreateRunAsync(UiRunAppKind.Benchmark,"Benchmark","Fast",sourceTab.Tab);
        var result = Result(sourceTab.LeftProject,sourceTab.Tab,"Aggregate",64,123.5,"Sample.Type","Count=64",[new global::UI.BenchmarkArgument("Count","64")]);

        repository.QueueSaveResult(result,runId,"Aggregate",ViewState());

        Assert.True(WaitUntil(() => store.ListMetrics(runId!.Value).Any(static item => item.MetricName=="MeanNanoseconds")));
        var loaded = Assert.Single(repository.LoadResultItems(runId!.Value,DateTimeOffset.UtcNow,sourceTab));
        Assert.Equal(sourceTab.LeftProject,loaded.Project);
        Assert.Equal("Sample.Type",loaded.TypeFullName);
        Assert.Equal("Count=64",loaded.ArgumentKey);
        Assert.Equal(64,loaded.ElementCount);
        Assert.Equal(123.5,loaded.MeanNanoseconds);
    }

    [Fact]
    public async Task QueueSaveResult_ReplacesMeasurementForSameJob(){
        var store = new global::Seamless.UI.Storage.UiRunStore(CreateDatabasePath());
        var repository = new global::UI.BenchmarkUiRunRepository(store);
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var runId = await repository.CreateRunAsync(UiRunAppKind.Benchmark,"Benchmark","Fast",sourceTab.Tab);

        repository.QueueSaveResult(Result(sourceTab.LeftProject,sourceTab.Tab,"Aggregate",64,100,"Sample.Type","Count=64",[new global::UI.BenchmarkArgument("Count","64")]),runId,"Aggregate",ViewState());
        Assert.True(WaitUntil(() => store.ListMetrics(runId!.Value).Any(static item => item.MetricName=="MeanNanoseconds"&&Math.Abs(item.NumericValue-100)<0.001)));

        repository.QueueSaveResult(Result(sourceTab.LeftProject,sourceTab.Tab,"Aggregate",64,250,"Sample.Type","Count=64",[new global::UI.BenchmarkArgument("Count","64")]),runId,"Aggregate",ViewState());

        Assert.True(WaitUntil(() => store.ListMetrics(runId!.Value).Any(static item => item.MetricName=="MeanNanoseconds"&&Math.Abs(item.NumericValue-250)<0.001)));
        var metrics = store.ListMetrics(runId!.Value).Where(static item => item.MetricName=="MeanNanoseconds").ToArray();
        Assert.Single(metrics);
        Assert.Equal(250,metrics[0].NumericValue);
    }

    [Fact]
    public async Task LoadResultItems_PreservesScalableTypeAndArgumentKey(){
        var store = new global::Seamless.UI.Storage.UiRunStore(CreateDatabasePath());
        var repository = new global::UI.BenchmarkUiRunRepository(store);
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.ScalableTsavoriteSet);
        var arguments = new[] {
            new global::UI.BenchmarkArgument("Count","262144"),
            new global::UI.BenchmarkArgument("Index","131072"),
            new global::UI.BenchmarkArgument("Page","4096"),
        };
        var argumentKey = global::UI.BenchmarkArgumentFormatting.CreateKey(arguments);
        var runId = await repository.CreateRunAsync(UiRunAppKind.ScalableBenchmark,"Scalable","Fast",sourceTab.Tab);
        var result = Result(sourceTab.LeftProject,sourceTab.Tab,"Add",262144,500,"Scalable.Tsavorite.Collections.Set.SetInt32",argumentKey,arguments);

        repository.QueueSaveResult(result,runId,"Add",ViewState());

        Assert.True(WaitUntil(() => store.ListMetrics(runId!.Value).Any(static item => item.MetricName=="MeanNanoseconds")));
        var loaded = Assert.Single(repository.LoadResultItems(runId!.Value,DateTimeOffset.UtcNow,sourceTab));
        Assert.Equal("Scalable.Tsavorite.Collections.Set.SetInt32",loaded.TypeFullName);
        Assert.Equal(argumentKey,loaded.ArgumentKey);
        Assert.Equal("262144",global::UI.BenchmarkArgumentFormatting.GetValue(loaded.Arguments,"Count"));
        Assert.Equal("131072",global::UI.BenchmarkArgumentFormatting.GetValue(loaded.Arguments,"Index"));
    }

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
            AssignedLogicalCpu=0,
            ExitCode=0,
        });

    private static global::UI.BenchmarkViewStateSnapshot ViewState() =>
        new("{}",global::UI.BenchmarkSourceTab.SystemArray.ToString(),"64");

    private static bool WaitUntil(Func<bool> condition){
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while(DateTimeOffset.UtcNow<deadline){
            if(condition()){
                return true;
            }
            Thread.Sleep(25);
        }
        return condition();
    }

    private static string CreateDatabasePath(){
        var root = Path.Combine(AppContext.BaseDirectory,"BenchmarkUiRunRepositoryTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root,"runs.db");
    }
}



