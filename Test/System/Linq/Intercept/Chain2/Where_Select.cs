using System.Linq;


namespace Test.System.Linq.Intercept.Chain2;

public sealed class Where_Select(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Source().Where(static v => v>2).Select(static v => v*10).ToList();
        MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(static v => v>2).Select(static v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(static v => v>2).Select(static v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void Query() {
        var expected=Source().Where(v => v>2).Select(v => v*10).ToList();
        MeasureOptimized(nameof(QueryOptimized),()=>QueryOptimized(expected));
        MeasureOptimized(nameof(QueryNoOptimized),()=>QueryNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() where v>2 select v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() where v>2 select v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void Lambda() {
        var expected=Source().Where(v => v>2).Select(v => v*10).ToList();
        MeasureOptimized(nameof(LambdaOptimized),()=>LambdaOptimized(expected));
        MeasureOptimized(nameof(LambdaNoOptimized),()=>LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(v => v>2).Select(v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(v => v>2).Select(v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
