using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Take(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void count() {
        var expected=Source().Take(3).ToList();
        MeasureOptimized(nameof(countOptimized),()=>countOptimized(expected));
        MeasureOptimized(nameof(countNoOptimized),()=>countNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Take(3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Take(3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void range() {
        var expected=Source().Take(new global::System.Range(1,3)).ToList();
        MeasureOptimized(nameof(rangeOptimized),()=>rangeOptimized(expected));
        MeasureOptimized(nameof(rangeNoOptimized),()=>rangeNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void rangeOptimized(dynamic expected) {
        var actual=Source().Take(new global::System.Range(1,3)).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void rangeNoOptimized(dynamic expected) {
        var actual=Source().Take(new global::System.Range(1,3)).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
