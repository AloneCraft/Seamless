using UI;

namespace Test.Benchmark;

public sealed class BenchmarkTestSleepJobRunner{
    [Fact]
    public async Task RunAsync(){
        var runner = new global::UI.BenchmarkTestSleepJobRunner(TimeSpan.FromMilliseconds(1));
        var job = new global::UI.BenchmarkJob{
            Project="Test.Project",
            AssemblyName="Test.Assembly",
            TypeFullName="Test.Type",
            MethodName="Sleep",
            SourceTab=global::UI.BenchmarkSourceTab.SystemArray,
            ElementCount=1,
            LoopCount=1,
            PreparedElementCounts=[1],
        };
        var runRoot = Path.Combine(Path.GetTempPath(),"Seamless-TestSleepJobRunner-"+Guid.NewGuid().ToString("N"));

        var result = await runner.RunAsync(job,runRoot,0,TimeSpan.FromSeconds(5),CancellationToken.None);

        Assert.Equal("Completed",result.Status);
        Assert.Equal("Test.Project",result.Project);
        Assert.Equal("Sleep",result.Method);
        Assert.NotNull(result.MeanNanoseconds);
        Assert.Equal(0,result.AllocatedBytes);
        Assert.Equal("test",result.ResultDigest);
    }

    [Fact]
    public async Task RunAsync_IEnumerableSourceTabCreatesArtifactDirectory(){
        var runner=new global::UI.BenchmarkTestSleepJobRunner(TimeSpan.FromMilliseconds(1));
        var job=new global::UI.BenchmarkJob{
            Project="System.IEnumerable.FLinq",
            AssemblyName="Benchmark.System.Linq.Enumerable.IEnumerable.FLinq",
            TypeFullName="Benchmark.System.Linq.Enumerable.IEnumerable.FLinqInt32",
            MethodName="Any",
            SourceTab=global::UI.BenchmarkSourceTab.SystemIEnumerable,
            ElementCount=4_096,
            LoopCount=1,
            PreparedElementCounts=[4_096],
        };
        var runRoot=Path.Combine(Path.GetTempPath(),"Seamless-IEnumerableSourceTab-"+Guid.NewGuid().ToString("N"));
        try{
            var result=await runner.RunAsync(job,runRoot,0,TimeSpan.FromSeconds(5),CancellationToken.None);

            Assert.Equal("Completed",result.Status);
            Assert.True(Directory.Exists(result.ArtifactFolder));
            Assert.Contains("System.Collections.Generic.IEnumerable__",result.ArtifactFolder,StringComparison.Ordinal);
        }finally{
            if(Directory.Exists(runRoot)){
                Directory.Delete(runRoot,true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_ObservesCancellation(){
        var runner = new global::UI.BenchmarkTestSleepJobRunner(TimeSpan.FromSeconds(5));
        var job = new global::UI.BenchmarkJob{
            Project="Test.Project",
            AssemblyName="Test.Assembly",
            TypeFullName="Test.Type",
            MethodName="Sleep",
            SourceTab=global::UI.BenchmarkSourceTab.SystemArray,
            ElementCount=1,
            LoopCount=1,
            PreparedElementCounts=[1],
        };
        var runRoot = Path.Combine(Path.GetTempPath(),"Seamless-TestSleepJobRunner-"+Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => runner.RunAsync(job,runRoot,0,TimeSpan.FromSeconds(5),cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_InfiniteModeWaitsUntilCancellation(){
        var runner = new global::UI.BenchmarkTestSleepJobRunner(TimeSpan.FromMilliseconds(1),true);
        var job = new global::UI.BenchmarkJob{
            Project="Test.Project",
            AssemblyName="Test.Assembly",
            TypeFullName="Test.Type",
            MethodName="Sleep",
            SourceTab=global::UI.BenchmarkSourceTab.SystemArray,
            ElementCount=1,
            LoopCount=1,
            PreparedElementCounts=[1],
        };
        var runRoot = Path.Combine(Path.GetTempPath(),"Seamless-TestSleepJobRunner-"+Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();

        var runTask = runner.RunAsync(job,runRoot,0,TimeSpan.FromSeconds(5),cancellation.Token);
        var firstCompleted = await Task.WhenAny(runTask,Task.Delay(50));
        Assert.NotSame(runTask,firstCompleted);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runTask);
    }
}
