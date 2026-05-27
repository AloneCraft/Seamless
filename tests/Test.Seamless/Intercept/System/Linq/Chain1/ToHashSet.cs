using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class ToHashSet:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.ToHashSet(LinqChain1TestBase.Source());
        var actual=LinqChain1TestBase.Source().ToHashSet();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.ToHashSet(LinqChain1TestBase.Source(),EqualityComparer<int>.Default);
        var actual=LinqChain1TestBase.Source().ToHashSet(EqualityComparer<int>.Default);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
