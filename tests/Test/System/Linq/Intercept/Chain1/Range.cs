using System.Linq;

using SystemEnumerable = global::System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Range(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void start_count() {
        var expected=SystemEnumerable.Range(1,4).ToList();
        MeasureOptimized(nameof(start_countOptimized),()=>start_countOptimized(expected));
        MeasureOptimized(nameof(start_countNoOptimized),()=>start_countNoOptimized(expected));
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
