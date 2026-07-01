namespace UI;

public sealed class BenchmarkResultCpuDisplayLogicTests{
    [Fact]
    public void ResolveAssignedLogicalCpuReturnsAssignedCpuWhenAvailable(){
        var logicalCpu = BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(4,[0,2,4,6]);

        Assert.Equal(4,logicalCpu);
    }

    [Fact]
    public void ResolveAssignedLogicalCpuReturnsNullWhenMissing(){
        Assert.Null(BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(null,[0,2,4,6]));
        Assert.Null(BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(5,[0,2,4,6]));
    }
}
