using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class SequenceEqual:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.SequenceEqual(LinqChain1TestBase.Source(),LinqChain1TestBase.EqualSource());
        var actual=LinqChain1TestBase.Source().SequenceEqual(LinqChain1TestBase.EqualSource());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_comparer() => second_comparerImpl();

    private static void second_comparerImpl() {
        var expected=SystemEnumerable.SequenceEqual(LinqChain1TestBase.Source(),LinqChain1TestBase.EqualSource(),EqualityComparer<int>.Default);
        var actual=LinqChain1TestBase.Source().SequenceEqual(LinqChain1TestBase.EqualSource(),EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

}
