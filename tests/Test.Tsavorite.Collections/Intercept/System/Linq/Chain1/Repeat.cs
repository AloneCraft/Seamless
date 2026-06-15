using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Repeat:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void element_count() {
        var expected=SystemEnumerable.Repeat(7,3).ToList();
        element_countOptimized(expected);
        element_countNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void element_countOptimized(dynamic expected) {
        var actual=Enumerable.Repeat(7,3).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void element_countNoOptimized(dynamic expected) {
        var actual=Enumerable.Repeat(7,3).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
