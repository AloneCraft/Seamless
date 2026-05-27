using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class DefaultIfEmpty:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.DefaultIfEmpty(LinqChain1TestBase.EmptySource());
        var actual=LinqChain1TestBase.EmptySource().DefaultIfEmpty();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void defaultValue() => defaultValueImpl();

    private static void defaultValueImpl() {
        var expected=SystemEnumerable.DefaultIfEmpty(LinqChain1TestBase.EmptySource(),42);
        var actual=LinqChain1TestBase.EmptySource().DefaultIfEmpty(42);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
