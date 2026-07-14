namespace Test.System.Linq.Intercept.Chain1;

public sealed class Concat(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void second() {
        var expected = Source().Concat(Second()).ToList();
        this.MeasureOptimized(nameof(secondOptimized),() => secondOptimized(expected));
        this.MeasureOptimized(nameof(secondNoOptimized),() => secondNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
