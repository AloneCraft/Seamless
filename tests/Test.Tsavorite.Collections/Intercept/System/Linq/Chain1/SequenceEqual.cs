using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class SequenceEqual:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.SequenceEqual(Source(),EqualSource());
        var actual=Source().SequenceEqual(EqualSource());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_comparer() => second_comparerImpl();

    private static void second_comparerImpl() {
        var expected=SystemEnumerable.SequenceEqual(Source(),EqualSource(),EqualityComparer<int>.Default);
        var actual=Source().SequenceEqual(EqualSource(),EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

}
