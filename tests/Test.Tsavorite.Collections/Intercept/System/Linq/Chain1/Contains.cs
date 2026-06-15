using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Contains:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void value() {
        var expected=Source().Contains(3);
        valueOptimized(expected);
        valueNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void valueOptimized(dynamic expected) {
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void valueNoOptimized(dynamic expected) {
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void value_comparer() {
        var expected=Source().Contains(3,EqualityComparer<int>.Default);
        value_comparerOptimized(expected);
        value_comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void value_comparerOptimized(dynamic expected) {
        var actual=Source().Contains(3,EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void value_comparerNoOptimized(dynamic expected) {
        var actual=Source().Contains(3,EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

}
