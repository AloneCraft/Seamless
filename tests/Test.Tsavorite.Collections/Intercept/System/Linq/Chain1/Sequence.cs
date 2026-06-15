using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Sequence:LinqChain1TestBase {
    [Fact,Trait("Category","Unit")]
    public static void start_end_step() {
        var expected=SystemEnumerable.Sequence(1L,9L,2L).ToList();
        start_end_stepOptimized(expected);
        start_end_stepNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void start_end_stepOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(1L,9L,2L).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void start_end_stepNoOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(1L,9L,2L).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public static void descending() {
        var expected=SystemEnumerable.Sequence(9L,1L,-2L).ToList();
        descendingOptimized(expected);
        descendingNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void descendingOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(9L,1L,-2L).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void descendingNoOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(9L,1L,-2L).ToList();
        AssertSequenceEqual(expected,actual);
    }
}
