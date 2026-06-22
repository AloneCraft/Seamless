using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class ElementAt(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void index() {
        var expected=Source().ElementAt(2);
        this.MeasureOptimized(nameof(indexOptimized),()=>indexOptimized(expected));
        this.MeasureOptimized(nameof(indexNoOptimized),()=>indexNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void indexOptimized(dynamic expected) {
        var actual=Source().ElementAt(2);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void indexNoOptimized(dynamic expected) {
        var actual=Source().ElementAt(2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void indexFromEnd() {
        var expected=Source().ElementAt(^1);
        this.MeasureOptimized(nameof(indexFromEndOptimized),()=>indexFromEndOptimized(expected));
        this.MeasureOptimized(nameof(indexFromEndNoOptimized),()=>indexFromEndNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void indexFromEndOptimized(dynamic expected) {
        var actual=Source().ElementAt(^1);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void indexFromEndNoOptimized(dynamic expected) {
        var actual=Source().ElementAt(^1);
        Assert.Equal(expected,actual);
    }

}
