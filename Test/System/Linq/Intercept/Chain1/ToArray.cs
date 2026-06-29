namespace Test.System.Linq.Intercept.Chain1;

public sealed class ToArray(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=Source().ToArray();
        this.MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=Source().ToArray();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=Source().ToArray();
        AssertSequenceEqual(expected,actual);
    }

}
