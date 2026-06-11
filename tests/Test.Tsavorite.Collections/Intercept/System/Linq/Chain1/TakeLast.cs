using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class TakeLast:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() => countImpl();

    private static void countImpl() {
        var expected=SystemEnumerable.TakeLast(Source(),2).ToList();
        var actual=new Consumer<int>();
        Source().TakeLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
