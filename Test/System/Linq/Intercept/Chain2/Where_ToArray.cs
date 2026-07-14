namespace Test.System.Linq.Intercept.Chain2;

// 2 個目が終端演算子（ToArray）のケース。materialize して直接比較する。
public sealed class Where_ToArray(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected = Source().Where(static v => v>2).ToArray();
        this.AssertOptimized(nameof(StaticOptimized),expected,() => StaticOptimized());
        this.AssertOptimized(nameof(StaticNoOptimized),expected,() => StaticNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int[] StaticOptimized() =>
        Source().Where(static v => v>2).ToArray();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int[] StaticNoOptimized() =>
        Source().Where(static v => v>2).ToArray();
}
