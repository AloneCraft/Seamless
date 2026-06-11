using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Distinct:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Distinct(SourceWithDuplicates()).ToList();
        var actual=new Consumer<int>();
        SourceWithDuplicates().Distinct().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() => comparerImpl();

    private static void comparerImpl() {
        var expected=SystemEnumerable.Distinct(SourceWithDuplicates(),EqualityComparer<int>.Default).ToList();
        var actual=new Consumer<int>();
        SourceWithDuplicates().Distinct(EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
