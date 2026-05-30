using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class TakeLast:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() => countImpl();

    private static void countImpl() {
        var expected=SystemEnumerable.TakeLast(LinqChain1TestBase.Source(),2).ToList();
        var actual=LinqChain1TestBase.Source().TakeLast(2).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
