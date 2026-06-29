namespace Test.System.Linq.Intercept.Chain1;

public sealed class ToHashSet(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=Source().ToHashSet();
        this.MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=Source().ToHashSet();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=Source().ToHashSet();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void comparer() {
        var expected=Source().ToHashSet(EqualityComparer<int>.Default);
        this.MeasureOptimized(nameof(comparerOptimized),()=>comparerOptimized(expected));
        this.MeasureOptimized(nameof(comparerNoOptimized),()=>comparerNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void comparerOptimized(dynamic expected) {
        var actual=Source().ToHashSet(EqualityComparer<int>.Default);
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void comparerNoOptimized(dynamic expected) {
        var actual=Source().ToHashSet(EqualityComparer<int>.Default);
        AssertSequenceEqual(expected,actual);
    }

}
