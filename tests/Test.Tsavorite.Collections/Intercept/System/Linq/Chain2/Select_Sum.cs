using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

// 2 個目がスカラー終端演算子（Sum）のケース。
public sealed class Select_Sum:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() {
        var expected=Source().Select(static v => v*2).Sum();
        StaticOptimized(expected);
        StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=Source().Select(static v => v*2).Sum();
                Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=Source().Select(static v => v*2).Sum();
                Assert.Equal(expected,actual);
    }
}
