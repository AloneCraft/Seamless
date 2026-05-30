using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Reverse:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Reverse(LinqChain1TestBase.Source()).ToList();
        var actual=LinqChain1TestBase.Source().Reverse().ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
