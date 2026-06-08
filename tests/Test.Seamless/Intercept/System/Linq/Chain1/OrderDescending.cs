using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class OrderDescending:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.OrderDescending(SourceWithDuplicates()).ToList();
        var actual=SourceWithDuplicates().OrderDescending().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.OrderDescending(SourceWithDuplicates(),Comparer<int>.Default).ToList();
        var actual=SourceWithDuplicates().OrderDescending(Comparer<int>.Default).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
