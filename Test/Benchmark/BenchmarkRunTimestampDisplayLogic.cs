using UI;

namespace Test.Benchmark;

public sealed class BenchmarkRunTimestampDisplayLogic{
    [Fact]
    public void ResolveRunningTimestampUsesStartedAtWhenAvailable(){
        var startedAt = new DateTimeOffset(2026,7,1,17,6,18,TimeSpan.Zero);
        var observedAt = new DateTimeOffset(2026,7,1,17,6,30,TimeSpan.Zero);

        var timestamp = global::UI.BenchmarkRunTimestampDisplayLogic.ResolveRunningTimestamp(startedAt,observedAt);

        Assert.Equal(startedAt,timestamp);
    }

    [Fact]
    public void ResolveRunningTimestampFallsBackToObservedAt(){
        var observedAt = new DateTimeOffset(2026,7,1,17,6,30,TimeSpan.Zero);

        var timestamp = global::UI.BenchmarkRunTimestampDisplayLogic.ResolveRunningTimestamp(null,observedAt);

        Assert.Equal(observedAt,timestamp);
    }
}



