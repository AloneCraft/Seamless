namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkResultItemTests{
    [Fact]
    public void Constructor_UsesStoredSourceTabIdForSharedSeamlessLinqProject(){
        var persisted = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessPersisted);
        var tsavorite = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessTsavorite);

        var persistedResult = new global::UI.BenchmarkResultItem(StoredResult(
            persisted.RightProject,
            "Persisted.Linq.Enumerable.LinqInt32",
            persisted.Id));
        var tsavoriteResult = new global::UI.BenchmarkResultItem(StoredResult(
            tsavorite.RightProject,
            "Tsavorite.Linq.Enumerable.LinqInt32",
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
                "Tsavorite.Linq.Enumerable.LinqInt32",
                "Aggregate_func"),
            new InvalidOperationException("boom"));

        Assert.Equal(global::UI.BenchmarkSourceTab.SeamlessTsavorite,result.SourceTab);
    }

    [Fact]
    public void Constructor_FallsBackToProjectWhenStoredSourceTabIdIsMissing(){
        var array = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);

        var result = new global::UI.BenchmarkResultItem(StoredResult(
            array.LeftProject,
            "DotNet.Linq.Enumerable.FLinqInt32",
            string.Empty));

        Assert.Equal(global::UI.BenchmarkSourceTab.SystemArray,result.SourceTab);
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
