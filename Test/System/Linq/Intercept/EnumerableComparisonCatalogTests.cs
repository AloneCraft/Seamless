using DynamicLinq;
namespace Test.System.Linq.Intercept;

public sealed class EnumerableComparisonCatalogTests {
    public static IEnumerable<object[]> BenchmarkMethodNames() {
        foreach(var methodName in EnumerableComparisonCatalog.BenchmarkMethodNames) {
            yield return [methodName];
        }
    }

    [Fact,Trait("Category","Unit")]
    public void BenchmarkMethodNames_cover_current_chain1_surface() {
        var chain1Directory = Path.Combine(AppContext.BaseDirectory,"..","..","..","System","Linq","Intercept","Chain1");
        var actualMethodNames = Directory
            .EnumerateFiles(chain1Directory,"*.cs",SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static fileName => fileName!="LinqChain1TestBase")
            .Order()
            .ToArray();
        var expectedMethodNames = EnumerableComparisonCatalog.BenchmarkMethodNames
            .Concat(EnumerableComparisonCatalog.Chain1ExcludedMethodNames)
            .Order()
            .ToArray();

        Assert.Equal(expectedMethodNames,actualMethodNames);
    }

    [Theory,Trait("Category","Unit"),MemberData(nameof(BenchmarkMethodNames))]
    public void BenchmarkMethodNames_match_enumerable_result(string methodName) {
        var expected = EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunSystem(methodName));
        using var optimizedScope = LinqCounterScope.Start();
        var flinq = EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunFLinq(methodName));
        using var noOptimizedScope = LinqCounterScope.Start();
        var noOptimized = EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunFLinqNoOptimized(methodName));
        var zlinq = EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunZLinq(methodName));

        Assert.Equal(expected,flinq);
        Assert.Equal(expected,noOptimized);
        Assert.Equal(expected,zlinq);
        Assert.True(optimizedScope.Counter.PhysicalDelegate<=noOptimizedScope.Counter.PhysicalDelegate);
        Assert.Equal(noOptimizedScope.Counter.LogicalDelegate,noOptimizedScope.Counter.PhysicalDelegate);
    }
}
