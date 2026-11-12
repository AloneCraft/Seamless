namespace Test.System.Linq.Intercept.Chain2;

public sealed class Index_Select(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected = Source().Index().Select(static pair => pair.Index+pair.Item).ToList();
        this.MeasureOptimized(nameof(StaticOptimized),() => StaticOptimized(expected));
        this.MeasureOptimized(nameof(StaticNoOptimized),() => StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Index().Select(static pair => pair.Index+pair.Item).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Index().Select(static pair => pair.Index+pair.Item).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void Lambda() {
        var expected = Source().Index().Select(pair => pair.Index+pair.Item).ToList();
        this.MeasureOptimized(nameof(LambdaOptimized),() => LambdaOptimized(expected));
        this.MeasureOptimized(nameof(LambdaNoOptimized),() => LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Index().Select(pair => pair.Index+pair.Item).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Index().Select(pair => pair.Index+pair.Item).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
