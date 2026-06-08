using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class DefaultIfEmpty:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.DefaultIfEmpty(EmptySource()).ToList();
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void defaultValue() => defaultValueImpl();

    private static void defaultValueImpl() {
        var expected=SystemEnumerable.DefaultIfEmpty(EmptySource(),42).ToList();
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty(42).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
