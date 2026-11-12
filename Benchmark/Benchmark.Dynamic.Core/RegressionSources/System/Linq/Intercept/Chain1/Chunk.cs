namespace Test.System.Linq.Intercept.Chain1;

public sealed class Chunk(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void size() {
        var expected = Source().Chunk(2).ToList();
        this.MeasureOptimized(nameof(sizeOptimized),() => sizeOptimized(expected));
        this.MeasureOptimized(nameof(sizeNoOptimized),() => sizeNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void sizeOptimized(dynamic expected) {
        var actual = new Consumer<int[]>();
        Source().Chunk(2).Consume(actual);
        AssertNestedSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void sizeNoOptimized(dynamic expected) {
        var actual = new Consumer<int[]>();
        Source().Chunk(2).Consume(actual);
        AssertNestedSequenceEqual(expected,actual.Items);
    }

}
