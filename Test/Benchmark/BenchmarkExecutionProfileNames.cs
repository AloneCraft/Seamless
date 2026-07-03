using UI;

namespace Test.Benchmark;

public sealed class BenchmarkExecutionProfileNames{
    [Fact]
    public void ToFolderName(){
        Assert.Equal("Accurate",global::UI.BenchmarkExecutionProfileNames.ToFolderName(global::UI.BenchmarkExecutionProfile.Accurate));
        Assert.Equal("Intermediate",global::UI.BenchmarkExecutionProfileNames.ToFolderName(global::UI.BenchmarkExecutionProfile.Intermediate));
        Assert.Equal("Fast",global::UI.BenchmarkExecutionProfileNames.ToFolderName(global::UI.BenchmarkExecutionProfile.Fast));
        Assert.Equal("Test",global::UI.BenchmarkExecutionProfileNames.ToFolderName(global::UI.BenchmarkExecutionProfile.Test));
    }

    [Fact]
    public void GetAllocatedBytesSource(){
        Assert.Equal("BenchmarkDotNet MemoryDiagnoser",global::UI.BenchmarkExecutionProfileNames.GetAllocatedBytesSource(global::UI.BenchmarkExecutionProfile.Accurate));
        Assert.Equal("BenchmarkDotNet MemoryDiagnoser",global::UI.BenchmarkExecutionProfileNames.GetAllocatedBytesSource(global::UI.BenchmarkExecutionProfile.Intermediate));
        Assert.Equal("GC.GetAllocatedBytesForCurrentThread",global::UI.BenchmarkExecutionProfileNames.GetAllocatedBytesSource(global::UI.BenchmarkExecutionProfile.Fast));
        Assert.Equal("Thread.Sleep test runner",global::UI.BenchmarkExecutionProfileNames.GetAllocatedBytesSource(global::UI.BenchmarkExecutionProfile.Test));
    }
}
