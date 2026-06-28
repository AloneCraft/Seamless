using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain2;

public sealed class Concat_Select(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Source().Concat(Second()).Select(static v => v+100).ToList();
        this.MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        this.MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Concat(Second()).Select(static v => v+100).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Concat(Second()).Select(static v => v+100).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
