using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Cast:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Cast<int>(LinqChain1TestBase.CastObjects()).ToList();
        var actual=LinqChain1TestBase.CastObjects().Cast<int>().ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
