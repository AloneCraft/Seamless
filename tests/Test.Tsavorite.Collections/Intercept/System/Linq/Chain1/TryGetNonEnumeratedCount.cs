using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class TryGetNonEnumeratedCount:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() {
        var expectedResult=Source().TryGetNonEnumeratedCount(out var expected);
        countOptimized(expectedResult,expected);
        countNoOptimized(expectedResult,expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countOptimized(bool expectedResult,int expected) {
        GeneratedInterceptorAssert.ContainsMethod<TryGetNonEnumeratedCount>(nameof(countOptimized));
        var actualResult=Source().TryGetNonEnumeratedCount(out var actual);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countNoOptimized(bool expectedResult,int expected) {
        var actualResult=Source().TryGetNonEnumeratedCount(out var actual);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expected,actual);
    }

}
