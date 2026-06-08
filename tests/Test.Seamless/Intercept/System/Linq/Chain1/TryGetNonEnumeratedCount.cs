using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class TryGetNonEnumeratedCount:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() => countImpl();

    private static void countImpl() {
        var expectedResult=SystemEnumerable.TryGetNonEnumeratedCount(Source(),out var expected);
        var actualResult=Source().TryGetNonEnumeratedCount(out var actual);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expected,actual);
    }

}
