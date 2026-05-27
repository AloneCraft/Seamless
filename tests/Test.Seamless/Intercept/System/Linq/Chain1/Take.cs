using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Take:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() => countImpl();

    private static void countImpl() {
        var expected=SystemEnumerable.Take(LinqChain1TestBase.Source(),3);
        var actual=LinqChain1TestBase.Source().Take(3);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void range() => rangeImpl();

    private static void rangeImpl() {
        var expected=SystemEnumerable.Take(LinqChain1TestBase.Source(),new global::System.Range(1,3));
        var actual=LinqChain1TestBase.Source().Take(new global::System.Range(1,3));
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
