using SystemEnumerable = global::System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Contains(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void value() {
        var expected=Source().Contains(3);
        this.MeasureOptimized(nameof(valueOptimized),()=>valueOptimized(expected));
        this.MeasureOptimized(nameof(valueNoOptimized),()=>valueNoOptimized(expected));
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
    public void value_comparer() {
        var expected=Source().Contains(3,EqualityComparer<int>.Default);
        this.MeasureOptimized(nameof(value_comparerOptimized),()=>value_comparerOptimized(expected));
        this.MeasureOptimized(nameof(value_comparerNoOptimized),()=>value_comparerNoOptimized(expected));
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
