namespace Test.System.Linq.Intercept.Chain1;

public sealed class Append(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void element() {
        var expected=Source().Append(5).ToList();
        this.MeasureOptimized(nameof(elementOptimized),()=>elementOptimized(expected));
        this.MeasureOptimized(nameof(elementNoOptimized),()=>elementNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void elementOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Append(5).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void elementNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Append(5).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
