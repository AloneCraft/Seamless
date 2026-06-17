using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class ElementAtOrDefault(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void index() {
        var expected=Source().ElementAtOrDefault(2);
        MeasureOptimized(nameof(indexOptimized),()=>indexOptimized(expected));
        MeasureOptimized(nameof(indexNoOptimized),()=>indexNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexOptimized(dynamic expected) {
        var actual=Source().ElementAtOrDefault(2);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexNoOptimized(dynamic expected) {
        var actual=Source().ElementAtOrDefault(2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void indexFromEnd() {
        var expected=Source().ElementAtOrDefault(^1);
        MeasureOptimized(nameof(indexFromEndOptimized),()=>indexFromEndOptimized(expected));
        MeasureOptimized(nameof(indexFromEndNoOptimized),()=>indexFromEndNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexFromEndOptimized(dynamic expected) {
        var actual=Source().ElementAtOrDefault(^1);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexFromEndNoOptimized(dynamic expected) {
        var actual=Source().ElementAtOrDefault(^1);
        Assert.Equal(expected,actual);
    }

}
