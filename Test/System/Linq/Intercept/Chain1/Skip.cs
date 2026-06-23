using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Skip(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void count() {
        var expected=Source().Skip(2).ToList();
        MeasureOptimized(nameof(countOptimized),()=>countOptimized(expected));
        MeasureOptimized(nameof(countNoOptimized),()=>countNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void countOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Skip(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void countNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Skip(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
