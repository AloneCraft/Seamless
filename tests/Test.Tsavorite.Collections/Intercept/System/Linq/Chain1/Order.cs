using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class Order:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Order(SourceWithDuplicates()).ToList();
        var actual=SourceWithDuplicates().Order().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.Order(SourceWithDuplicates(),Comparer<int>.Default).ToList();
        var actual=SourceWithDuplicates().Order(Comparer<int>.Default).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
