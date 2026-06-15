using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

// 2 個目がスカラー終端演算子（Count）のケース。
public sealed class Where_Count:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() {
        var expected=Source().Where(static v => v>2).Count();
        StaticOptimized(expected);
        StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=Source().Where(static v => v>2).Count();
                Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=Source().Where(static v => v>2).Count();
                Assert.Equal(expected,actual);
    }
}
