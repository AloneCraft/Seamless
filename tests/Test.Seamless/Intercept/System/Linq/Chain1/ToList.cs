using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class ToList:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.ToList(LinqChain1TestBase.Source());
        var actual=LinqChain1TestBase.Source().ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
