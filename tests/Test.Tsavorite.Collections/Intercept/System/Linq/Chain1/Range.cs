using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Range:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void start_count() {
        var expected=SystemEnumerable.Range(1,4).ToList();
        start_countOptimized(expected);
        start_countNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void start_countOptimized(dynamic expected) {
        var actual=Enumerable.Range(1,4).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void start_countNoOptimized(dynamic expected) {
        var actual=Enumerable.Range(1,4).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
