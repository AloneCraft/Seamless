using Benchmark.Worker;

namespace Test.BenchmarkWorker;

public sealed class BenchmarkOrchestratorTests{
    [Fact]
    public void StateManager_Load_MarksRunningJobsAsFailed(){
        var root = CreateTempRoot();
        var stateManager = new Benchmark.Worker.BenchmarkStateManager(root);
        var runRoot = Path.Combine(root,"run");
        var state = new ProgressState{
            RunId="run",
            RunRoot=runRoot,
            Mode=BenchmarkRunMode.Fixed,
            CreatedAt=DateTimeOffset.UtcNow,
            UpdatedAt=DateTimeOffset.UtcNow,
            ElementCounts=[128],
            Jobs=[
                new BenchmarkJob{
                    JobId="job",
                    Project="SystemFLinq",
                    AssemblyName="Assembly",
                    TypeFullName="Type",
                    MethodName="Where",
                    ElementCount=128,
                    Status=JobStatus.Running,
                },
            ],
        };

        stateManager.Save(state);
        var loaded = stateManager.Load(runRoot);

        var job = Assert.Single(loaded!.Jobs);
        Assert.Equal(JobStatus.Failed,job.Status);
        Assert.Contains("Crash/OOM",job.FailureReason,StringComparison.Ordinal);
    }

    [Fact]
    public async Task Orchestrator_FixedMode_CreatesBenchmarkByElementMatrix(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => new BenchmarkJobRunResult(
            job.Project,
            job.MethodName,
            job.TypeFullName,
            job.ElementCount,
            "Completed",
            "100 ns",
            string.Empty,
            string.Empty,
            100,
            0,
            0,
            0,
            0,
            job.ArtifactFolder,
            string.Empty));
        var orchestrator = new BenchmarkOrchestrator(new Benchmark.Worker.BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("SystemFLinq","Where"),Case("SystemZLinq","Where")],
                [0],
                [128,256],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var state = LoadOnlyState(root);
        Assert.Equal(4,state.Jobs.Count);
        Assert.All(state.Jobs,static job => Assert.Equal(JobStatus.Completed,job.Status));
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_StopsMethodAfterThresholdExceeded(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => {
            var mean = job.ElementCount>=256&&job.Project=="SystemZLinq" ? 2_000_000_000d : 100d;
            return new BenchmarkJobRunResult(
                job.Project,
                job.MethodName,
                job.TypeFullName,
                job.ElementCount,
                "Completed",
                mean+" ns",
                string.Empty,
                string.Empty,
                mean,
                0,
                0,
                0,
                0,
                job.ArtifactFolder,
                string.Empty);
        });
        var orchestrator = new BenchmarkOrchestrator(new Benchmark.Worker.BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("SystemFLinq","Where"),Case("SystemZLinq","Where")],
                [0],
                [128,256,512],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var state = LoadOnlyState(root);
        Assert.Equal([128,256],state.Jobs.Select(static job => job.ElementCount).Distinct().Order().ToArray());
        var adaptive = Assert.Single(state.AdaptiveStates);
        Assert.True(adaptive.IsStopped);
        Assert.Equal(256,adaptive.StoppedAtElementCount);
        Assert.Equal("SystemZLinq",adaptive.TriggerProject);
    }

    private static BenchmarkCase Case(string project,string method) =>
        new(project,"Assembly","Type",method);

    private static string CreateTempRoot(){
        var root = Path.Combine(AppContext.BaseDirectory,"BenchmarkOrchestratorTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static ProgressState LoadOnlyState(string root){
        var runRoot = Directory.GetDirectories(root).Single();
        return new Benchmark.Worker.BenchmarkStateManager(root).Load(runRoot)!;
    }

    private sealed class FakeRunner(Func<BenchmarkJob,int,BenchmarkJobRunResult> run):IBenchmarkJobRunner{
        public Task<BenchmarkJobRunResult> RunAsync(
            BenchmarkJob job,
            string runRoot,
            int logicalCpu,
            TimeSpan timeout,
            CancellationToken cancellationToken){
            job.ArtifactFolder=Path.Combine(runRoot,"artifacts",job.JobId);
            return Task.FromResult(run(job,logicalCpu));
        }
    }
}
