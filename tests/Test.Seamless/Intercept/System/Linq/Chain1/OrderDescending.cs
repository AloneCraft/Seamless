using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class OrderDescending:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.OrderDescending(LinqChain1TestBase.SourceWithDuplicates());
        var actual=LinqChain1TestBase.SourceWithDuplicates().OrderDescending();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.OrderDescending(LinqChain1TestBase.SourceWithDuplicates(),Comparer<int>.Default);
        var actual=LinqChain1TestBase.SourceWithDuplicates().OrderDescending(Comparer<int>.Default);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
