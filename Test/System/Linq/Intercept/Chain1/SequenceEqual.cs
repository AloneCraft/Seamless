using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class SequenceEqual(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void second() {
        var expected=Source().SequenceEqual(EqualSource());
        MeasureOptimized(nameof(secondOptimized),()=>secondOptimized(expected));
        MeasureOptimized(nameof(secondNoOptimized),()=>secondNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource());
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void second_comparer() {
        var expected=Source().SequenceEqual(EqualSource(),EqualityComparer<int>.Default);
        MeasureOptimized(nameof(second_comparerOptimized),()=>second_comparerOptimized(expected));
        MeasureOptimized(nameof(second_comparerNoOptimized),()=>second_comparerNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_comparerOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource(),EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_comparerNoOptimized(dynamic expected) {
        var actual=Source().SequenceEqual(EqualSource(),EqualityComparer<int>.Default);
        Assert.Equal(expected,actual);
    }

}
