using System.Linq;


namespace Test.System.Linq.Intercept.Chain2;

// 2 個目がスカラー終端演算子（Count）のケース。
public sealed class Where_Count(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Source().Where(static v => v>2).Count();
        MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=Source().Where(static v => v>2).Count();
                Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=Source().Where(static v => v>2).Count();
                Assert.Equal(expected,actual);
    }
}
