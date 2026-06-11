using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Contains:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void value() => valueImpl();

    private static void valueImpl() {
        var expected=SystemEnumerable.Contains(Source(),3);
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void value_comparer() => value_comparerImpl();

    private static void value_comparerImpl() {
        var expected=SystemEnumerable.Contains(Source(),3,EqualityComparer<int>.Default);
        var actual=Source().Contains(3,EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

}
