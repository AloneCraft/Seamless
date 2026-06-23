using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Contains(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void value() {
        var expected=Source().Contains(3);
        MeasureOptimized(nameof(valueOptimized),()=>valueOptimized(expected));
        MeasureOptimized(nameof(valueNoOptimized),()=>valueNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void valueOptimized(dynamic expected) {
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void valueNoOptimized(dynamic expected) {
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void value_comparer() {
        var expected=Source().Contains(3,EqualityComparer<int>.Default);
        MeasureOptimized(nameof(value_comparerOptimized),()=>value_comparerOptimized(expected));
        MeasureOptimized(nameof(value_comparerNoOptimized),()=>value_comparerNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void value_comparerOptimized(dynamic expected) {
        var actual=Source().Contains(3,EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void value_comparerNoOptimized(dynamic expected) {
        var actual=Source().Contains(3,EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

}
