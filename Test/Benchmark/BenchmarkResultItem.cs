namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkResultItemTests{
    [Fact]
    public void Constructor_UsesStoredSourceTabIdForSharedSeamlessLinqProject(){
        var persisted = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessPersisted);
        var tsavorite = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessTsavorite);

        var persistedResult = new global::UI.BenchmarkResultItem(StoredResult(
            persisted.RightProject,
            "Benchmark.Persisted.Linq.Enumerable.LinqInt32",
            persisted.Id));
        var tsavoriteResult = new global::UI.BenchmarkResultItem(StoredResult(
            tsavorite.RightProject,
            "Benchmark.Tsavorite.Linq.Enumerable.LinqInt32",
            tsavorite.Id));

        Assert.Equal(global::UI.BenchmarkSourceTab.SeamlessPersisted,persistedResult.SourceTab);
        Assert.Equal(global::UI.BenchmarkSourceTab.SeamlessTsavorite,tsavoriteResult.SourceTab);
    }

    [Fact]
    public void Failed_UsesBenchmarkAssemblyForSharedSeamlessLinqProject(){
        var tsavorite = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessTsavorite);

        var result = global::UI.BenchmarkResultItem.Failed(
            new global::UI.BenchmarkCase(
                tsavorite.RightProject,
                tsavorite.RightAssemblyName,
                "Benchmark.Tsavorite.Linq.Enumerable.LinqInt32",
                "Aggregate_func"),
            new InvalidOperationException("boom"));

        Assert.Equal(global::UI.BenchmarkSourceTab.SeamlessTsavorite,result.SourceTab);
    }

    [Fact]
    public void Constructor_ResolvesUniqueProjectWhenStoredSourceTabIdIsMissing(){
        var array = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);

        var result = new global::UI.BenchmarkResultItem(StoredResult(
            array.LeftProject,
            "Benchmark.System.Linq.Enumerable.FLinqInt32",
            string.Empty));

        Assert.Equal(global::UI.BenchmarkSourceTab.SystemArray,result.SourceTab);
    }

    [Fact]
    public void Constructor_RejectsAmbiguousProjectWhenStoredSourceTabIdIsMissing(){
        var persisted=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessPersisted);

        Assert.Throws<InvalidDataException>(() => new global::UI.BenchmarkResultItem(StoredResult(
            persisted.RightProject,
            "Benchmark.Persisted.Linq.Enumerable.LinqInt32",
            string.Empty)));
    }

    [Fact]
    public void Constructor_RejectsUnknownStoredSourceTabId(){
        var array=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);

        Assert.Throws<InvalidDataException>(() => new global::UI.BenchmarkResultItem(StoredResult(
            array.LeftProject,
            "Benchmark.System.Linq.Enumerable.FLinqInt32",
            "Unknown.Tab")));
    }

    [Fact]
    public void Constructor_NormalizesLoopCountTotalsPerOperation(){
        var sourceTab=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var result = new global::UI.BenchmarkResultItem(new global::UI.BenchmarkJob{
            Project=sourceTab.LeftProject,
            AssemblyName=sourceTab.LeftAssemblyName,
            MethodName="Aggregate_func",
            TypeFullName="Benchmark.System.Linq.Enumerable.FLinqInt32",
            SourceTab=global::UI.BenchmarkSourceTab.SystemArray,
            ElementCount=4096,
            LoopCount=8,
            Status=global::UI.JobStatus.Completed,
            MeanNanoseconds=800,
            ErrorNanoseconds=80,
            StdDevNanoseconds=40,
            AllocatedBytes=1024,
        });

        Assert.Equal(100,result.MeanNanoseconds);
        Assert.Equal(800,result.TotalNanoseconds);
        Assert.Equal(10,result.ErrorNanoseconds);
        Assert.Equal(5,result.StdDevNanoseconds);
        Assert.Equal(128,result.AllocatedBytesValue);
    }

    [Fact]
    public void Constructor_DoesNotReconstructMissingTotalForLegacySnapshot(){
        var stored=StoredResult(
            global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray).LeftProject,
            "Benchmark.System.Linq.Enumerable.FLinqInt32",
            global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray).Id);

        var result=new global::UI.BenchmarkResultItem(stored);

        Assert.Equal(100,result.MeanNanoseconds);
        Assert.Null(result.TotalNanoseconds);
    }

    private static global::UI.BenchmarkStoredResult StoredResult(
        string project,
        string typeFullName,
        string sourceTabId) =>
        new(
            project,
            "Aggregate_func",
            typeFullName,
            64,
            global::UI.BenchmarkWorkloadAxis.DefaultLoopCount,
            "Completed",
            "100",
            100,
            128,
            DateTimeOffset.UtcNow,
            null,
            string.Empty){
            SourceTabId=sourceTabId,
        };
}
