using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class SkipLast(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void count() {
        var expected=Source().SkipLast(2).ToList();
        MeasureOptimized(nameof(countOptimized),()=>countOptimized(expected));
        MeasureOptimized(nameof(countNoOptimized),()=>countNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SkipLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SkipLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
