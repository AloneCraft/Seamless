using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class ElementAtOrDefault:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void index() => indexImpl();

    private static void indexImpl() {
        var expected=SystemEnumerable.ElementAtOrDefault(LinqChain1TestBase.Source(),2);
        var actual=LinqChain1TestBase.Source().ElementAtOrDefault(2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void indexFromEnd() => indexFromEndImpl();

    private static void indexFromEndImpl() {
        var expected=SystemEnumerable.ElementAtOrDefault(LinqChain1TestBase.Source(),^1);
        var actual=LinqChain1TestBase.Source().ElementAtOrDefault(^1);
        Assert.Equal(expected,actual);
    }

}
