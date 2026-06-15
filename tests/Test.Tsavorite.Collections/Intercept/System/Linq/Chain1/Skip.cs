using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Skip(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void count() {
        var expected=Source().Skip(2).ToList();
        MeasureOptimized(nameof(countOptimized),()=>countOptimized(expected));
        MeasureOptimized(nameof(countNoOptimized),()=>countNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Skip(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Skip(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
