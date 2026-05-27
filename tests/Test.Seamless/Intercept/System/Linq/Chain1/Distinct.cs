using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Distinct:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Distinct(LinqChain1TestBase.SourceWithDuplicates());
        var actual=LinqChain1TestBase.SourceWithDuplicates().Distinct();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.Distinct(LinqChain1TestBase.SourceWithDuplicates(),EqualityComparer<int>.Default);
        var actual=LinqChain1TestBase.SourceWithDuplicates().Distinct(EqualityComparer<int>.Default);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
