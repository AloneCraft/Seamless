using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Range(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void start_count() {
        var expected = SystemEnumerable.Range(1,4).ToList();
        this.MeasureOptimized(nameof(start_countOptimized),() => start_countOptimized(expected));
        this.MeasureOptimized(nameof(start_countNoOptimized),() => start_countNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void start_countOptimized(dynamic expected) {
        var actual = Enumerable.Range(1,4).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void start_countNoOptimized(dynamic expected) {
        var actual = Enumerable.Range(1,4).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Sum_matches_System_for_empty_single_multiple_and_overflow() {
        foreach(var (start,count) in new[] { (0,0),(7,1),(-4,9),(1,4_096) }) {
            Assert.Equal(SystemEnumerable.Range(start,count).Sum(),SumOptimized(start,count));
        }

        Assert.Throws<OverflowException>(()=>SystemEnumerable.Range(1,65_536).Sum());
        Assert.Throws<OverflowException>(()=>SumOptimized(1,65_536));
    }

    [Fact, Trait("Category","Unit")]
    public static void Select_matches_System_selector_order_side_effects_and_exception() {
        foreach(var (start,count) in new[] { (0,0),(7,1),(-4,9) }) {
            var expectedVisited = new List<int>();
            var expected = SystemEnumerable.Range(start,count)
                .Select(value=>SelectValue(value,expectedVisited,int.MinValue))
                .ToArray();
            var actualVisited = new List<int>();
            var actualConsumer = new RangeSelectConsumer();

            SelectConsumeOptimized(start,count,actualVisited,int.MinValue,actualConsumer);

            Assert.Equal(expectedVisited,actualVisited);
            Assert.Equal(expected,actualConsumer.Items);
        }

        var expectedThrowVisited = new List<int>();
        var expectedThrowConsumer = new RangeSelectConsumer();
        var expectedException = Assert.Throws<InvalidOperationException>(()=>
            SystemEnumerable.Range(3,5)
                .Select(value=>SelectValue(value,expectedThrowVisited,5))
                .Consume(expectedThrowConsumer));
        var actualThrowVisited = new List<int>();
        var actualThrowConsumer = new RangeSelectConsumer();
        var actualException = Assert.Throws<InvalidOperationException>(()=>
            SelectConsumeOptimized(3,5,actualThrowVisited,5,actualThrowConsumer));

        Assert.Equal(expectedException.Message,actualException.Message);
        Assert.Equal(expectedThrowVisited,actualThrowVisited);
        Assert.Equal(expectedThrowConsumer.Items,actualThrowConsumer.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int SumOptimized(int start,int count)=>Enumerable.Range(start,count).Sum();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void SelectConsumeOptimized(
        int start,
        int count,
        List<int> visited,
        int throwAt,
        RangeSelectConsumer consumer)=>
        Enumerable.Range(start,count)
            .Select(value=>SelectValue(value,visited,throwAt))
            .Consume(consumer);

    private static int SelectValue(int value,List<int> visited,int throwAt) {
        visited.Add(value);
        if(value==throwAt) {
            throw new InvalidOperationException("selector failure");
        }
        return value*2;
    }

}

internal sealed class RangeSelectConsumer {
    internal List<int> Items { get; }=[];
    internal void Consume(int value)=>this.Items.Add(value);
}

internal static class RangeSelectConsumerExtensions {
    internal static void Consume(this IEnumerable<int> source,RangeSelectConsumer consumer) {
        foreach(var value in source) {
            consumer.Consume(value);
        }
    }
}
