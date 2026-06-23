using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Sequence(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact,Trait("Category","Unit")]
    public void start_end_step() {
        var expected=SystemEnumerable.Sequence(1L,9L,2L).ToList();
        MeasureOptimized(nameof(start_end_stepOptimized),()=>start_end_stepOptimized(expected));
        MeasureOptimized(nameof(start_end_stepNoOptimized),()=>start_end_stepNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void start_end_stepOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(1L,9L,2L).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void start_end_stepNoOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(1L,9L,2L).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void descending() {
        var expected=SystemEnumerable.Sequence(9L,1L,-2L).ToList();
        MeasureOptimized(nameof(descendingOptimized),()=>descendingOptimized(expected));
        MeasureOptimized(nameof(descendingNoOptimized),()=>descendingNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void descendingOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(9L,1L,-2L).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void descendingNoOptimized(dynamic expected) {
        var actual=Enumerable.Sequence(9L,1L,-2L).ToList();
        AssertSequenceEqual(expected,actual);
    }
}
