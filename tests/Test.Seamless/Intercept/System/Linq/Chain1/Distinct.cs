using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Distinct:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Distinct(LinqChain1TestBase.SourceWithDuplicates()).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.SourceWithDuplicates().Distinct().Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.Distinct(LinqChain1TestBase.SourceWithDuplicates(),EqualityComparer<int>.Default).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.SourceWithDuplicates().Distinct(EqualityComparer<int>.Default).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

}
