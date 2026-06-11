using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class SkipLast_SkipLast:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void SpanFusion() => SpanFusionImpl();

    private static void SpanFusionImpl() {
        //span経路(配列)・引数違いで加算融合になること
        var expected=SystemEnumerable.SkipLast(SystemEnumerable.SkipLast(Source(),1),2).ToList();
        var actual=new Consumer<int>();
        Source().SkipLast(1).SkipLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void SpanChain3() => SpanChain3Impl();

    private static void SpanChain3Impl() {
        //span経路・3段連鎖(合計が要素数を超えるケース)
        var expected=SystemEnumerable.SkipLast(SystemEnumerable.SkipLast(SystemEnumerable.SkipLast(Source(),2),2),2).ToList();
        var actual=new Consumer<int>();
        Source().SkipLast(2).SkipLast(2).SkipLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void NonSpanQueuePath() => NonSpanQueuePathImpl();

    private static void NonSpanQueuePathImpl() {
        //非span経路(Where挟みでQueue経路)
        var expected=SystemEnumerable.SkipLast(SystemEnumerable.SkipLast(SystemEnumerable.Where(Source(),p => p>0),1),2).ToList();
        var actual=new Consumer<int>();
        Source().Where(p => p>0).SkipLast(1).SkipLast(2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
