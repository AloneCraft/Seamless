using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class SequenceEqual:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() {
        var expected=Source().SequenceEqual(EqualSource());
        secondOptimized(expected);
        secondNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource());
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_comparer() {
        var expected=Source().SequenceEqual(EqualSource(),EqualityComparer<int>.Default);
        second_comparerOptimized(expected);
        second_comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_comparerOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource(),EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_comparerNoOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource(),EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

}
