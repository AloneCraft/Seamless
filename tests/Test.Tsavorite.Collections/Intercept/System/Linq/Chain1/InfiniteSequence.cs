using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class InfiniteSequence:LinqChain1TestBase {
    [Fact,Trait("Category","Unit")]
    public static void start_step() {
        var expected=Enumerable.InfiniteSequence(1L,2L).Take(5).ToList();
        start_stepOptimized(expected);
        start_stepNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void start_stepOptimized(dynamic expected) {
        var actual=Enumerable.InfiniteSequence(1L,2L).Take(5).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void start_stepNoOptimized(dynamic expected) {
        var actual=Enumerable.InfiniteSequence(1L,2L).Take(5).ToList();
        AssertSequenceEqual(expected,actual);
    }
}
