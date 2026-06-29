namespace Test.System.Linq.Intercept.Chain1;

public sealed class DefaultIfEmpty(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=EmptySource().DefaultIfEmpty().ToList();
        this.MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void defaultValue() {
        var expected=EmptySource().DefaultIfEmpty(42).ToList();
        this.MeasureOptimized(nameof(defaultValueOptimized),()=>defaultValueOptimized(expected));
        this.MeasureOptimized(nameof(defaultValueNoOptimized),()=>defaultValueNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void defaultValueOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty(42).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void defaultValueNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty(42).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
