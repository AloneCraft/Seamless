using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Empty(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=SystemEnumerable.Empty<int>().ToList();
        MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=Enumerable.Empty<int>().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=Enumerable.Empty<int>().ToList();
        AssertSequenceEqual(expected,actual);
    }

}
