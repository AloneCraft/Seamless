using UI;

namespace Test.Benchmark;

public sealed class BenchmarkSourceTabRunningState{
    [Fact]
    public void Apply_MarksSourceTabRunning(){
        var state = new global::UI.BenchmarkSourceTabRunningState();

        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Running));

        Assert.True(state.IsRunning(BenchmarkSourceTab.SystemArray));
        Assert.False(state.IsRunning(BenchmarkSourceTab.SystemList));
    }

    [Fact]
    public void Apply_KeepsSourceTabRunningWhileAnotherJobRuns(){
        var state = new global::UI.BenchmarkSourceTabRunningState();
        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Running));
        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Select",4096,1,JobStatus.Running));

        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Completed));

        Assert.True(state.IsRunning(BenchmarkSourceTab.SystemArray));
    }

    [Fact]
    public void Apply_ClearsSourceTabRunningWhenLastJobEnds(){
        var state = new global::UI.BenchmarkSourceTabRunningState();
        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Running));

        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Completed));

        Assert.False(state.IsRunning(BenchmarkSourceTab.SystemArray));
    }

    [Fact]
    public void Apply_ClearsRunningJobWhenTerminalCpuDiffers(){
        var state = new global::UI.BenchmarkSourceTabRunningState();
        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Running));

        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,1,JobStatus.Completed));

        Assert.False(state.IsRunning(BenchmarkSourceTab.SystemArray));
    }

    [Fact]
    public void Apply_ClearsFailedRunningJob(){
        var state = new global::UI.BenchmarkSourceTabRunningState();
        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Running));

        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Failed));

        Assert.False(state.IsRunning(BenchmarkSourceTab.SystemArray));
    }

    [Fact]
    public void Clear_ClearsAllRunningTabs(){
        var state = new global::UI.BenchmarkSourceTabRunningState();
        state.Apply(Result(BenchmarkSourceTab.SystemArray,"System.Array","Where",4096,0,JobStatus.Running));
        state.Apply(Result(BenchmarkSourceTab.SystemList,"System.Collections.Generic.List","Where",4096,1,JobStatus.Running));

        Assert.True(state.Clear());

        Assert.False(state.IsRunning(BenchmarkSourceTab.SystemArray));
        Assert.False(state.IsRunning(BenchmarkSourceTab.SystemList));
    }

    private static BenchmarkResultItem Result(
        BenchmarkSourceTab sourceTab,
        string project,
        string method,
        int elementCount,
        int logicalCpu,
        JobStatus status){
        var definition=BenchmarkSourceTabs.Get(sourceTab);
        var resolvedProject=definition.LeftProject;
        var typePrefix=definition.LeftTypePrefix;
        return new(new BenchmarkJob{
            SourceTab=sourceTab,
            Project=resolvedProject,
            AssemblyName=definition.LeftAssemblyName,
            TypeFullName=string.IsNullOrWhiteSpace(typePrefix)?"Sample.Type":typePrefix+".Sample",
            MethodName=method,
            ArgumentKey="Count=4096",
            ElementCount=elementCount,
            LoopCount=1,
            AssignedLogicalCpu=logicalCpu,
            Status=status,
            StartedAt=DateTimeOffset.UtcNow,
            MeanNanoseconds=status==JobStatus.Completed ? 100 : null,
        });
    }
}
