using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

internal static class DirectQueryExecution{
    internal static DynamicFallbackResult<T> Capture<T>(Func<T> action){
        var observed=DynamicFallbackCapture.Run(action);
        Assert.Equal(0,observed.FallbackCount);
        return observed;
    }
}
