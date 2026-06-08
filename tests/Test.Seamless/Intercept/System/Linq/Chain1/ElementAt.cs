using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class ElementAt:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void index() => indexImpl();

    private static void indexImpl() {
        var expected=SystemEnumerable.ElementAt(Source(),2);
        var actual=Source().ElementAt(2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void indexFromEnd() {
        indexFromEndImpl();
    }

    private static void indexFromEndImpl() {
        var expected=SystemEnumerable.ElementAt(Source(),^1);
        var actual=Source().ElementAt(^1);
        Assert.Equal(expected,actual);
    }

}
