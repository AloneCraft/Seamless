using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class TryGetNonEnumeratedCount(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void count() {
        var expectedResult=Source().TryGetNonEnumeratedCount(out var expected);
        MeasureOptimized(nameof(countOptimized),()=>countOptimized(expectedResult,expected));
        MeasureOptimized(nameof(countNoOptimized),()=>countNoOptimized(expectedResult,expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void countOptimized(bool expectedResult,int expected) {
        GeneratedInterceptorAssert.ContainsMethod<TryGetNonEnumeratedCount>(nameof(countOptimized));
        var actualResult=Source().TryGetNonEnumeratedCount(out var actual);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void countNoOptimized(bool expectedResult,int expected) {
        var actualResult=Source().TryGetNonEnumeratedCount(out var actual);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expected,actual);
    }

}
