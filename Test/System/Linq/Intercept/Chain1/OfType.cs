using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class OfType(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=CastObjects().OfType<int>().ToList();
        this.MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        CastObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        CastObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void mixedTypes() {
        var expected=MixedObjects().OfType<int>().ToList();
        this.MeasureOptimized(nameof(mixedTypesOptimized),()=>mixedTypesOptimized(expected));
        this.MeasureOptimized(nameof(mixedTypesNoOptimized),()=>mixedTypesNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void mixedTypesOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        MixedObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void mixedTypesNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        MixedObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
