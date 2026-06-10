using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class Prepend:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void element() => elementImpl();

    private static void elementImpl() {
        var expected=SystemEnumerable.Prepend(Source(),0).ToList();
        var actual=new Consumer<int>();
        Source().Prepend(0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
