using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class OfType:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.OfType<int>(LinqChain1TestBase.CastObjects()).ToList();
        var actual=LinqChain1TestBase.CastObjects().OfType<int>().ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void mixedTypes() => mixedTypesImpl();

    private static void mixedTypesImpl() {
        var expected=SystemEnumerable.OfType<int>(LinqChain1TestBase.MixedObjects()).ToList();
        var actual=LinqChain1TestBase.MixedObjects().OfType<int>().ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
