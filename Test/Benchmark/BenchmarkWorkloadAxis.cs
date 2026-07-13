namespace Test.Benchmark;

public sealed class BenchmarkWorkloadAxisTests {
    [Fact]
    public void StandardBenchmark_SeparatesElementsAndActualLoopCount() {
        Assert.Equal(4_096,global::UI.BenchmarkWorkloadAxis.ResolveElementCount(global::UI.BenchmarkSourceTab.SystemArray,1));
        Assert.Equal(4,global::UI.BenchmarkWorkloadAxis.ResolveLoopCount(global::UI.BenchmarkSourceTab.SystemArray,4));
        Assert.Equal([4_096],global::UI.BenchmarkWorkloadAxis.ResolvePreparedElementCounts(global::UI.BenchmarkSourceTab.SystemArray,[1,2,4]));
    }

    [Fact]
    public async Task Orchestrator_CalibratesToHashSetFromOneToFourCalls() {
        var root=Path.Combine(Path.GetTempPath(),"SeamlessLoopAxis",Guid.NewGuid().ToString("N"));
        try {
            var tab=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
            var runner=new CalibratedRunner(tab.LeftProject,332_000,114_000);
            var orchestrator=new global::UI.BenchmarkOrchestrator(new global::UI.BenchmarkStateManager(root),runner);
            var result=await orchestrator.RunAsync(
                new global::UI.BenchmarkRunRequest(
                    [
                        new global::UI.BenchmarkCase(tab.LeftProject,tab.LeftAssemblyName,tab.LeftTypePrefix+".Sample","ToHashSet"),
                        new global::UI.BenchmarkCase(tab.RightProject,tab.RightAssemblyName,tab.RightTypePrefix+".Sample","ToHashSet"),
                    ],
                    [0],
                    [1],
                    TimeSpan.FromMilliseconds(1),
                    TimeSpan.FromSeconds(5),
                    SourceTab:global::UI.BenchmarkSourceTab.SystemArray),
                _=>{},
                _=>{},
                CancellationToken.None);

            Assert.Equal([1,2,4],result.State.Jobs.Where(job=>job.Project==tab.LeftProject).Select(job=>job.LoopCount).Order().ToArray());
            Assert.Equal([4],result.State.Jobs.Where(job=>job.Project==tab.RightProject).Select(job=>job.LoopCount).ToArray());
            Assert.All(result.State.Jobs,job=>{
                Assert.Equal(4_096,job.ElementCount);
                Assert.Equal([4_096],job.PreparedElementCounts);
            });
        } finally {
            if(Directory.Exists(root)) {
                Directory.Delete(root,true);
            }
        }
    }

    private sealed class CalibratedRunner(string leftProject,double leftNanoseconds,double rightNanoseconds):global::UI.IBenchmarkJobRunner {
        public Task<global::UI.BenchmarkJobRunResult>RunAsync(global::UI.BenchmarkJob job,string runRoot,int logicalCpu,TimeSpan timeout,CancellationToken cancellationToken) {
            var perCall=job.Project==leftProject?leftNanoseconds:rightNanoseconds;
            return Task.FromResult(new global::UI.BenchmarkJobRunResult(
                job.Project,
                job.MethodName,
                job.TypeFullName,
                job.ElementCount,
                "Completed",
                string.Empty,
                string.Empty,
                string.Empty,
                perCall*job.LoopCount,
                null,
                null,
                0,
                0,
                runRoot,
                string.Empty,
                LoopCount:job.LoopCount));
        }
    }
}
