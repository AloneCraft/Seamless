using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Take:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() {
        var expected=Source().Take(3).ToList();
        countOptimized(expected);
        countNoOptimized(expected);
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
    public static void range() {
        var expected=Source().Take(new global::System.Range(1,3)).ToList();
        rangeOptimized(expected);
        rangeNoOptimized(expected);
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
