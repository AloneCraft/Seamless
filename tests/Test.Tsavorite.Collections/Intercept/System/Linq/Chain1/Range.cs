using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class Range:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void start_count() => start_countImpl();

    private static void start_countImpl() {
        var expected=SystemEnumerable.Range(1,4).ToList();
        var actual=Enumerable.Range(1,4).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
