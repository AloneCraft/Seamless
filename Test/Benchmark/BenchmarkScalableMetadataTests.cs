using Benchmark;
using System.Reflection;

namespace Test.Benchmark;

public sealed class BenchmarkScalableMetadataTests {
    [Fact]
    public void ScalableSourceTabs_AreSetDictionaryAndPersistedList() {
        Assert.Equal(
            [
                BenchmarkSourceTabs.ScalableSetId,
                BenchmarkSourceTabs.ScalableDictionaryId,
                BenchmarkSourceTabs.ScalablePersistedListId,
            ],
            BenchmarkSourceTabs.ScalableAll.Select(static tab => tab.Id).ToArray());

        var set = BenchmarkSourceTabs.ScalableAll[0];
        Assert.Equal("Persisted",set.LeftHeader);
        Assert.Equal("Tsavorite",set.RightHeader);

        var list = BenchmarkSourceTabs.ScalableAll[2];
        Assert.Equal("Scalable.Persisted.List",list.LeftProject);
        Assert.Empty(list.RightProject);
        Assert.Single(list.ProjectOrder);
    }

    [Fact]
    public void ScalableCatalog_ContainsOnlyScalableProjects() {
        Assert.All(BenchmarkCatalog.ScalableProjects,project => {
            Assert.StartsWith("Scalable.",project.DisplayName,StringComparison.Ordinal);
            Assert.StartsWith("Benchmark.Scalable.",project.AssemblyName,StringComparison.Ordinal);
            Assert.StartsWith("Scalable.",project.TypePrefix,StringComparison.Ordinal);
        });
        Assert.DoesNotContain(BenchmarkCatalog.ScalableProjects,static project => project.AssemblyName=="Benchmark.Persisted.Collection");
        Assert.DoesNotContain(BenchmarkCatalog.ScalableProjects,static project => project.AssemblyName=="Benchmark.Tsavorite.Collection");
    }

    [Fact]
    public void ScalableElementCounts_AreLargeEightStepRange() {
        var type = Assembly.Load(new AssemblyName("Benchmark.Scalable.Persisted.Collection"))
            .GetType("Scalable.Collection.ScalableElementCounts",throwOnError: true)!;
        var all = ((IEnumerable<int>)type.GetProperty("All")!.GetValue(null)!).ToArray();
        var defaultInitial = ((IEnumerable<int>)type.GetProperty("DefaultInitial")!.GetValue(null)!).ToArray();
        var baseElementCount = (int)type.GetField("BaseElementCount")!.GetValue(null)!;

        Assert.Equal([65536,131072,262144,524288,1048576,2097152,4194304,8388608],all);
        Assert.Equal([65536],defaultInitial);
        Assert.Equal(65536,baseElementCount);
    }
}
