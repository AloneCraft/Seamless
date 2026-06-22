using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain2;

public sealed class SkipLast_SkipLast(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void SpanFusion() {
                //span経路(配列)・引数違いで加算融合になること
        var expected=Source().SkipLast(1).SkipLast(2).ToList();
        this.MeasureOptimized(nameof(SpanFusionOptimized),()=>SpanFusionOptimized(expected));
        this.MeasureOptimized(nameof(SpanFusionNoOptimized),()=>SpanFusionNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void SpanFusionOptimized(dynamic expected) {
                //span経路(配列)・引数違いで加算融合になること
        var actual=new Consumer<int>();
                Source().SkipLast(1).SkipLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void SpanFusionNoOptimized(dynamic expected) {
                //span経路(配列)・引数違いで加算融合になること
        var actual=new Consumer<int>();
                Source().SkipLast(1).SkipLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void SpanChain3() {
                //span経路・3段連鎖(合計が要素数を超えるケース)
        var expected=Source().SkipLast(2).SkipLast(2).SkipLast(2).ToList();
        this.MeasureOptimized(nameof(SpanChain3Optimized),()=>SpanChain3Optimized(expected));
        this.MeasureOptimized(nameof(SpanChain3NoOptimized),()=>SpanChain3NoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void SpanChain3Optimized(dynamic expected) {
                //span経路・3段連鎖(合計が要素数を超えるケース)
        var actual=new Consumer<int>();
                Source().SkipLast(2).SkipLast(2).SkipLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void SpanChain3NoOptimized(dynamic expected) {
                //span経路・3段連鎖(合計が要素数を超えるケース)
        var actual=new Consumer<int>();
                Source().SkipLast(2).SkipLast(2).SkipLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void NonSpanQueuePath() {
                //非span経路(Where挟みでQueue経路)
        var expected=Source().Where(p => p>0).SkipLast(1).SkipLast(2).ToList();
        this.MeasureOptimized(nameof(NonSpanQueuePathOptimized),()=>NonSpanQueuePathOptimized(expected));
        this.MeasureOptimized(nameof(NonSpanQueuePathNoOptimized),()=>NonSpanQueuePathNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NonSpanQueuePathOptimized(dynamic expected) {
                //非span経路(Where挟みでQueue経路)
        var actual=new Consumer<int>();
                Source().Where(p => p>0).SkipLast(1).SkipLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NonSpanQueuePathNoOptimized(dynamic expected) {
                //非span経路(Where挟みでQueue経路)
        var actual=new Consumer<int>();
                Source().Where(p => p>0).SkipLast(1).SkipLast(2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
