using System.Linq;


namespace Test.System.Linq.Intercept.Chain2;

public sealed class Select_Take(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Source().Select(static v => v*2).Take(3).ToList();
        MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(static v => v*2).Take(3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(static v => v*2).Take(3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
