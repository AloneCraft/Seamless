using System.Linq;

namespace Test.Tsavorite.Collections.Intercept.System.Linq;

public sealed class EnumerableComparisonCatalogTests {
    public static IEnumerable<object[]> BenchmarkMethodNames() {
        foreach(var methodName in EnumerableComparisonCatalog.BenchmarkMethodNames) {
            yield return [methodName];
        }
    }

    [Fact,Trait("Category","Unit")]
    public void BenchmarkMethodNames_cover_current_chain1_surface() {
        var chain1Directory = Path.Combine(AppContext.BaseDirectory,"..","..","..","Intercept","System","Linq","Chain1");
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
        var flinq = EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunFLinq(methodName));
        var zlinq = EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunZLinq(methodName));

        Assert.Equal(expected,flinq);
        Assert.Equal(expected,zlinq);
    }
}
