namespace Test.System.Linq.Intercept.Chain1;

public sealed class InfiniteSequence(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact,Trait("Category","Unit")]
    public void start_step() {
        var expected=Enumerable.InfiniteSequence(1L,2L).Take(5).ToList();
        this.MeasureOptimized(nameof(start_stepOptimized),()=>start_stepOptimized(expected));
        this.MeasureOptimized(nameof(start_stepNoOptimized),()=>start_stepNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void start_stepOptimized(dynamic expected) {
        var actual=Enumerable.InfiniteSequence(1L,2L).Take(5).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void start_stepNoOptimized(dynamic expected) {
        var actual=Enumerable.InfiniteSequence(1L,2L).Take(5).ToList();
        AssertSequenceEqual(expected,actual);
    }
}
