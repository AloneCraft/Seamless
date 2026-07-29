using Benchmark.Dynamic.Infrastructure;

namespace Benchmark.Dynamic.Tests;

public sealed class WorkerProcessSession {
    [Fact,Trait("Category","Unit")]
    public void ForcedTerminationDelay() {
        Assert.Equal(TimeSpan.FromSeconds(3),global::Benchmark.Dynamic.Infrastructure.WorkerProcessSession.ForcedTerminationDelay);
    }
}
