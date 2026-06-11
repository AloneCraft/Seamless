using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class SkipLast:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() => countImpl();

    private static void countImpl() {
        var expected=SystemEnumerable.SkipLast(Source(),2).ToList();
        var actual=new Consumer<int>();
        Source().SkipLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
