namespace Test.System.Linq.Intercept.Chain2;

public sealed class Select_Select(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Source().Select(static v => v*2).Select(static v => v+1).ToList();
        this.MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        this.MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(static v => v*2).Select(static v => v+1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(static v => v*2).Select(static v => v+1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void Lambda() {
        var expected=Source().Select(v => v*2).Select(v => v+1).ToList();
        this.MeasureOptimized(nameof(LambdaOptimized),()=>LambdaOptimized(expected));
        this.MeasureOptimized(nameof(LambdaNoOptimized),()=>LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(v => v*2).Select(v => v+1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(v => v*2).Select(v => v+1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void Query() {
        var expected=Source().Select(v => v*2).Select(v => v+1).ToList();
        this.MeasureOptimized(nameof(QueryOptimized),()=>QueryOptimized(expected));
        this.MeasureOptimized(nameof(QueryNoOptimized),()=>QueryNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() select v*2 into doubled select doubled+1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() select v*2 into doubled select doubled+1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
