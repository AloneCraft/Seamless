using UI;
using System.Reflection;

namespace Test.Benchmark;

public sealed class BenchmarkScalableMetadata {
    [Fact]
    public void ScalableSourceTabs_AreSetDictionaryAndPersistedList() {
        Assert.Equal(
            [
                BenchmarkSourceTabs.ScalablePersistedSetId,
                BenchmarkSourceTabs.ScalableTsavoriteSetId,
                BenchmarkSourceTabs.ScalablePersistedDictionaryId,
                BenchmarkSourceTabs.ScalableTsavoriteDictionaryId,
                BenchmarkSourceTabs.ScalablePersistedListId,
            ],
            BenchmarkSourceTabs.ScalableAll.Select(static tab => tab.Id).ToArray());

        var set = BenchmarkSourceTabs.ScalableAll[0];
        Assert.Equal("Persisted",set.LeftHeader);
        Assert.Equal("Previous",set.RightHeader);
        Assert.Equal("Scalable.Persisted.Set.Previous",set.RightProject);

        var list = BenchmarkSourceTabs.ScalableAll[4];
        Assert.Equal("Scalable.Persisted.List",list.LeftProject);
        Assert.Equal("Scalable.Persisted.List.Previous",list.RightProject);
        Assert.Equal(2,list.ProjectOrder.Count);
    }

    [Fact]
    public void ScalableCatalog_ContainsOnlyScalableProjects() {
        Assert.All(BenchmarkCatalog.ScalableProjects,project => {
            Assert.StartsWith("Scalable.",project.DisplayName,StringComparison.Ordinal);
            Assert.StartsWith("Scalable.",project.AssemblyName,StringComparison.Ordinal);
            Assert.StartsWith("Scalable.",project.TypePrefix,StringComparison.Ordinal);
        });
        Assert.DoesNotContain(BenchmarkCatalog.ScalableProjects,static project => project.AssemblyName=="Persisted.Collections");
        Assert.DoesNotContain(BenchmarkCatalog.ScalableProjects,static project => project.AssemblyName=="Tsavorite.Collections");
    }

    [Fact]
    public void ScalableElementCounts_AreLargeEightStepRange() {
        var type = Assembly.Load(new AssemblyName("Scalable.Persisted.Collections"))
            .GetType("Scalable.Collections.ScalableElementCounts",throwOnError: true)!;
        var all = ((IEnumerable<int>)type.GetProperty("All")!.GetValue(null)!).ToArray();
        var defaultInitial = ((IEnumerable<int>)type.GetProperty("DefaultInitial")!.GetValue(null)!).ToArray();
        var baseElementCount = (int)type.GetField("BaseElementCount")!.GetValue(null)!;

        Assert.Equal([65536,131072,262144,524288,1048576,2097152,4194304,8388608],all);
        Assert.Equal([65536],defaultInitial);
        Assert.Equal(65536,baseElementCount);
    }
}

