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

    [Fact]
    public void SharedProjectRequiresExactAssembly(){
        var persisted=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessPersisted);
        var tsavorite=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessTsavorite);

        Assert.Null(global::UI.BenchmarkSourceTabs.TryGetByProject(persisted.RightProject));
        Assert.Equal(persisted,global::UI.BenchmarkSourceTabs.TryGetByProjectAndAssembly(persisted.RightProject,persisted.RightAssemblyName));
        Assert.Equal(tsavorite,global::UI.BenchmarkSourceTabs.TryGetByProjectAndAssembly(tsavorite.RightProject,tsavorite.RightAssemblyName));
        Assert.Null(global::UI.BenchmarkSourceTabs.TryGetByProjectAndAssembly(persisted.RightProject,"Wrong.Assembly"));
        Assert.Throws<InvalidDataException>(() => global::UI.BenchmarkSourceTabs.GetByProjectAndAssembly(persisted.RightProject,"Wrong.Assembly"));
    }

    [Fact]
    public void ContainsCaseRequiresProjectAssemblyAndTypePrefix(){
        var sourceTab=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.OptimizationRewrite);

        Assert.True(sourceTab.ContainsCase(new(
            sourceTab.LeftProject,
            sourceTab.LeftAssemblyName,
            sourceTab.LeftTypePrefix+"ClassInt32",
            "Aggregate")));
        Assert.False(sourceTab.ContainsCase(new(
            sourceTab.LeftProject,
            "Wrong.Assembly",
            sourceTab.LeftTypePrefix+"ClassInt32",
            "Aggregate")));
        Assert.False(sourceTab.ContainsCase(new(
            sourceTab.LeftProject,
            sourceTab.LeftAssemblyName,
            "Wrong.Type",
            "Aggregate")));
    }
}
