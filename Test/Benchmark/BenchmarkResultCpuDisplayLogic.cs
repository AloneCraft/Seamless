using UI;

namespace Test.Benchmark;

public sealed class BenchmarkResultCpuDisplayLogic{
    [Fact]
    public void ResolveAssignedLogicalCpuReturnsAssignedCpuWhenAvailable(){
        var logicalCpu = global::UI.BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(4,[0,2,4,6]);

        Assert.Equal(4,logicalCpu);
    }

    [Fact]
    public void ResolveAssignedLogicalCpuReturnsNullWhenMissing(){
        Assert.Null(global::UI.BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(null,[0,2,4,6]));
        Assert.Null(global::UI.BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(5,[0,2,4,6]));
    }
}



