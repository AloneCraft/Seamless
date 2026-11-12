using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Repeat(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void element_count() {
        var expected = SystemEnumerable.Repeat(7,3).ToList();
        this.MeasureOptimized(nameof(element_countOptimized),() => element_countOptimized(expected));
        this.MeasureOptimized(nameof(element_countNoOptimized),() => element_countNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void element_countOptimized(dynamic expected) {
        var actual = Enumerable.Repeat(7,3).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void element_countNoOptimized(dynamic expected) {
        var actual = Enumerable.Repeat(7,3).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
