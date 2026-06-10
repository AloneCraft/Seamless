using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain2;

public sealed class TakeLast_TakeLast:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void SpanFusion() => SpanFusionImpl();

    private static void SpanFusionImpl() {
        //span経路(配列)・引数違いでmin融合になること
        var expected=SystemEnumerable.TakeLast(SystemEnumerable.TakeLast(Source(),3),2).ToList();
        var actual=new Consumer<int>();
        Source().TakeLast(3).TakeLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void SpanChain3() => SpanChain3Impl();

    private static void SpanChain3Impl() {
        //span経路・3段連鎖
        var expected=SystemEnumerable.TakeLast(SystemEnumerable.TakeLast(SystemEnumerable.TakeLast(Source(),2),3),4).ToList();
        var actual=new Consumer<int>();
        Source().TakeLast(2).TakeLast(3).TakeLast(4).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void NonSpanQueuePath() => NonSpanQueuePathImpl();

    private static void NonSpanQueuePathImpl() {
        //非span経路(Where挟みでQueue経路)
        var expected=SystemEnumerable.TakeLast(SystemEnumerable.TakeLast(SystemEnumerable.Where(Source(),p => p>0),3),2).ToList();
        var actual=new Consumer<int>();
        Source().Where(p => p>0).TakeLast(3).TakeLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
