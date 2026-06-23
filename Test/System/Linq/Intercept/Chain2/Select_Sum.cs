using System.Linq;


namespace Test.System.Linq.Intercept.Chain2;

// 2 個目がスカラー終端演算子（Sum）のケース。
public sealed class Select_Sum(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Source().Select(static v => v*2).Sum();
        MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=Source().Select(static v => v*2).Sum();
                Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=Source().Select(static v => v*2).Sum();
                Assert.Equal(expected,actual);
    }
}
