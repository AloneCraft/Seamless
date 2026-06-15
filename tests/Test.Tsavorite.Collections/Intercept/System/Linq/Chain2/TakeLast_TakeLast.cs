using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class TakeLast_TakeLast(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void SpanFusion() {
                //span経路(配列)・引数違いでmin融合になること
        var expected=Source().TakeLast(3).TakeLast(2).ToList();
        MeasureOptimized(nameof(SpanFusionOptimized),()=>SpanFusionOptimized(expected));
        MeasureOptimized(nameof(SpanFusionNoOptimized),()=>SpanFusionNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SpanFusionOptimized(dynamic expected) {
                //span経路(配列)・引数違いでmin融合になること
        var actual=new Consumer<int>();
                Source().TakeLast(3).TakeLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SpanFusionNoOptimized(dynamic expected) {
                //span経路(配列)・引数違いでmin融合になること
        var actual=new Consumer<int>();
                Source().TakeLast(3).TakeLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void SpanChain3() {
                //span経路・3段連鎖
        var expected=Source().TakeLast(2).TakeLast(3).TakeLast(4).ToList();
        MeasureOptimized(nameof(SpanChain3Optimized),()=>SpanChain3Optimized(expected));
        MeasureOptimized(nameof(SpanChain3NoOptimized),()=>SpanChain3NoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SpanChain3Optimized(dynamic expected) {
                //span経路・3段連鎖
        var actual=new Consumer<int>();
                Source().TakeLast(2).TakeLast(3).TakeLast(4).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SpanChain3NoOptimized(dynamic expected) {
                //span経路・3段連鎖
        var actual=new Consumer<int>();
                Source().TakeLast(2).TakeLast(3).TakeLast(4).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void NonSpanQueuePath() {
                //非span経路(Where挟みでQueue経路)
        var expected=Source().Where(p => p>0).TakeLast(3).TakeLast(2).ToList();
        MeasureOptimized(nameof(NonSpanQueuePathOptimized),()=>NonSpanQueuePathOptimized(expected));
        MeasureOptimized(nameof(NonSpanQueuePathNoOptimized),()=>NonSpanQueuePathNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NonSpanQueuePathOptimized(dynamic expected) {
                //非span経路(Where挟みでQueue経路)
        var actual=new Consumer<int>();
                Source().Where(p => p>0).TakeLast(3).TakeLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NonSpanQueuePathNoOptimized(dynamic expected) {
                //非span経路(Where挟みでQueue経路)
        var actual=new Consumer<int>();
                Source().Where(p => p>0).TakeLast(3).TakeLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
