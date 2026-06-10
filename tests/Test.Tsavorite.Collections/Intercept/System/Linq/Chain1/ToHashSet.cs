using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class ToHashSet:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.ToHashSet(Source());
        var actual=Source().ToHashSet();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.ToHashSet(Source(),EqualityComparer<int>.Default);
        var actual=Source().ToHashSet(EqualityComparer<int>.Default);
        AssertSequenceEqual(expected,actual);
    }

}
