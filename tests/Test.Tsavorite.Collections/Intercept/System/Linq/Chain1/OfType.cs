using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class OfType:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.OfType<int>(CastObjects()).ToList();
        var actual=new Consumer<int>();
        CastObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void mixedTypes() => mixedTypesImpl();

    private static void mixedTypesImpl() {
        var expected=SystemEnumerable.OfType<int>(MixedObjects()).ToList();
        var actual=new Consumer<int>();
        MixedObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
