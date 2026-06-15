using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

// 2 個目が終端演算子（ToArray）のケース。materialize して直接比較する。
public sealed class Where_ToArray:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() {
        var expected=Source().Where(static v => v>2).ToArray();
        AssertSequenceEqual(expected,StaticOptimized());
        AssertSequenceEqual(expected,StaticNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int[] StaticOptimized() =>
        Source().Where(static v => v>2).ToArray();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int[] StaticNoOptimized() =>
        Source().Where(static v => v>2).ToArray();
}
