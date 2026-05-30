using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Concat:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.Concat(LinqChain1TestBase.Source(),LinqChain1TestBase.Second()).ToList();
        var actual=LinqChain1TestBase.Source().Concat(LinqChain1TestBase.Second()).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
