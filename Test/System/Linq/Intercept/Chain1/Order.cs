using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Order(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=SourceWithDuplicates().Order().ToList();
        MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void comparer() {
        var expected=SourceWithDuplicates().Order(Comparer<int>.Default).ToList();
        MeasureOptimized(nameof(comparerOptimized),()=>comparerOptimized(expected));
        MeasureOptimized(nameof(comparerNoOptimized),()=>comparerNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void comparerOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order(Comparer<int>.Default).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void comparerNoOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order(Comparer<int>.Default).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
