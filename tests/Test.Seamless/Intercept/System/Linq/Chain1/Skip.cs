using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Skip:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() => countImpl();

    private static void countImpl() {
        var expected=SystemEnumerable.Skip(LinqChain1TestBase.Source(),2).ToList();
        var actual=LinqChain1TestBase.Source().Skip(2).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
