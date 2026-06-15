using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class TakeLast:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() {
        var expected=Source().TakeLast(2).ToList();
        countOptimized(expected);
        countNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void countNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
