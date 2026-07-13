namespace Test.Benchmark;

public sealed class BenchmarkSourceTabsTests{
    [Fact]
    public void StandardAll_UsesShortDisplayNamesAndStableIds(){
        Assert.Equal(
            ["Rewrite","PipelineFusion","PredicatePushdown","Array","IEnumerable","Range","List","Persisted","Tsavorite"],
            global::UI.BenchmarkSourceTabs.StandardAll.Select(static tab=>tab.DisplayName));
        Assert.Equal(
            ["Optimization.Rewrite","Optimization.PipelineFusion","Optimization.PredicatePushdown","System.Array","System.Collections.Generic.IEnumerable<>","System.Range","System.List","Seamless.Persisted","Seamless.Tsavorite"],
            global::UI.BenchmarkSourceTabs.StandardAll.Select(static tab=>tab.Id));
    }

    [Fact]
    public void ScalableAll_UsesShortDisplayNamesAndStableIds(){
        Assert.Equal(
            ["Persisted.Set","Tsavorite.Set","Persisted.Dictionary","Tsavorite.Dictionary","Persisted.List"],
            global::UI.BenchmarkSourceTabs.ScalableAll.Select(static tab=>tab.DisplayName));
        Assert.Equal(
            ["Scalable.Persisted.Set","Scalable.Tsavorite.Set","Scalable.Persisted.Dictionary","Scalable.Tsavorite.Dictionary","Scalable.Persisted.List"],
            global::UI.BenchmarkSourceTabs.ScalableAll.Select(static tab=>tab.Id));
    }

    [Fact]
    public void IEnumerableSourceTab_UsesWindowsSafeArtifactPathPart(){
        var pathPart=global::UI.BenchmarkSourceTabs.ToPathPart(global::UI.BenchmarkSourceTab.SystemIEnumerable);

        Assert.Equal("System.Collections.Generic.IEnumerable__",pathPart);
        Assert.DoesNotContain(pathPart,static ch=>ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*');
    }

    [Fact]
    public void EverySourceTab_HasNonEmptyArtifactPathPart(){
        foreach(var tab in global::UI.BenchmarkSourceTabs.StandardAll.Concat(global::UI.BenchmarkSourceTabs.ScalableAll)){
            Assert.False(string.IsNullOrWhiteSpace(global::UI.BenchmarkSourceTabs.ToPathPart(tab.Tab)));
        }
    }
}
