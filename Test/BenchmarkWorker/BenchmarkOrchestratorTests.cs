using Benchmark;

namespace Test.BenchmarkWorker;

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
public sealed class BenchmarkOrchestratorTests{
    [Fact]
    public void StateManager_Load_MarksRunningJobsAsFailed(){
        var root = CreateTempRoot();
        var stateManager = new BenchmarkStateManager(root);
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
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

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
    public async Task Orchestrator_FixedMode_DoesNotCrashWhenParallelJobsPersistProgress(){
        var root = CreateTempRoot();
        var runner = new DelayedFakeRunner(async (job,_) => {
            await Task.Delay(10);
            return new BenchmarkJobRunResult(
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
                string.Empty);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [
                    Case("SystemFLinq","Where"),
                    Case("SystemZLinq","Where"),
                    Case("PersistedFLinq","Where"),
                    Case("PersistedLinq","Where"),
                    Case("TsavoriteFLinq","Where"),
                    Case("TsavoriteLinq","Where"),
                ],
                [0,1,2,3],
                [128],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var state = LoadOnlyState(root);
        Assert.Equal(6,state.Jobs.Count);
        Assert.All(state.Jobs,static job => Assert.Equal(JobStatus.Completed,job.Status));
        Assert.Empty(Directory.GetFiles(state.RunRoot,"*.tmp"));
    }

    [Fact]
    public async Task StateManager_Save_IsSafeWhenCalledConcurrently(){
        var root = CreateTempRoot();
        var runRoot = Path.Combine(root,"run");
        var stateManager = new BenchmarkStateManager(root);
        var state = new ProgressState{
            RunId="run",
            RunRoot=runRoot,
            Mode=BenchmarkRunMode.Fixed,
            CreatedAt=DateTimeOffset.UtcNow,
            UpdatedAt=DateTimeOffset.UtcNow,
            ElementCounts=[128],
            Jobs=[
                CompletedJob("SystemFLinq","Where",128),
                CompletedJob("SystemZLinq","Where",128),
            ],
        };

        await Task.WhenAll(Enumerable.Range(0,32).Select(_ => Task.Run(() => stateManager.Save(state))));

        var loaded = stateManager.Load(runRoot);
        Assert.NotNull(loaded);
        Assert.Equal(2,loaded.Jobs.Count);
        Assert.Empty(Directory.GetFiles(runRoot,"*.tmp"));
    }

    [Fact]
    public async Task ResultWriter_WriteOutputs_IsSafeWhenCalledConcurrently(){
        var root = CreateTempRoot();
        var runRoot = Path.Combine(root,"run");
        var writer = new BenchmarkResultWriter();
        var state = new ProgressState{
            RunId="run",
            RunRoot=runRoot,
            Mode=BenchmarkRunMode.Fixed,
            CreatedAt=DateTimeOffset.UtcNow,
            UpdatedAt=DateTimeOffset.UtcNow,
            ElementCounts=[128,256],
            Jobs=[
                CompletedJob("SystemFLinq","Where",128),
                CompletedJob("SystemZLinq","Where",128),
                CompletedJob("SystemFLinq","Where",256),
                CompletedJob("SystemZLinq","Where",256),
            ],
        };

        await Task.WhenAll(Enumerable.Range(0,16).Select(_ => Task.Run(() => writer.WriteOutputs(state))));

        Assert.True(File.Exists(Path.Combine(runRoot,"BenchmarkComparison-N128.csv")));
        Assert.True(File.Exists(Path.Combine(runRoot,"BenchmarkComparison-N256.csv")));
        Assert.True(File.Exists(Path.Combine(runRoot,"BenchmarkRunSummary.txt")));
        Assert.Empty(Directory.GetFiles(runRoot,"*.tmp"));
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_StopsOnlyCaseAfterThresholdExceeded(){
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
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

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
        Assert.Equal([128,256,512],state.Jobs.Select(static job => job.ElementCount).Distinct().Order().ToArray());
        Assert.Contains(state.Jobs,static job => job.Project=="SystemFLinq"&&job.ElementCount==512);
        Assert.DoesNotContain(state.Jobs,static job => job.Project=="SystemZLinq"&&job.ElementCount==512);
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_StopsAfterThresholdExceededBeforeNextElementCount(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => {
            var mean = job.ElementCount>=4 ? 2_000_000d : 100d;
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
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("SystemFLinq","Where")],
                [0],
                [1,2,4,8],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var state = LoadOnlyState(root);
        Assert.Equal([1,2,4],state.Jobs.Select(static job => job.ElementCount).Order().ToArray());
        Assert.DoesNotContain(state.Jobs,static job => job.ElementCount==8);
    }

    [Fact]
    public async Task Orchestrator_RunAsync_TreatsCompletedWithoutStatisticsAsFailed(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => new BenchmarkJobRunResult(
            job.Project,
            job.MethodName,
            job.TypeFullName,
            job.ElementCount,
            "Completed",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            0,
            job.ArtifactFolder,
            string.Empty));
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("SystemFLinq","Where")],
                [0],
                [1],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var job = Assert.Single(LoadOnlyState(root).Jobs);
        Assert.Equal(JobStatus.Failed,job.Status);
        Assert.Contains("without benchmark statistics",job.FailureReason,StringComparison.Ordinal);
    }

    [Fact]
    public void BenchmarkElementCountLogic_BuildAdaptiveCandidates_UsesOneToSelectedMaximum(){
        var candidates = BenchmarkElementCountLogic.BuildAdaptiveCandidates(BenchmarkElementCounts.All,8192);

        Assert.Equal([1,2,4,8,16,32,64,128,256,512,1024,2048,4096,8192],candidates);
    }

    [Fact]
    public void BenchmarkElementCounts_StartsAdaptiveWorkloadAtOne(){
        Assert.Equal(1,BenchmarkElementCounts.All[0]);
        Assert.Equal([1],BenchmarkElementCounts.DefaultInitial);
    }

    private static BenchmarkCase Case(string project,string method) =>
        new(project,"Assembly","Type",method);

    private static BenchmarkJob CompletedJob(string project,string method,int elementCount) =>
        new(){
            JobId=project+"|"+method+"|"+elementCount,
            Project=project,
            AssemblyName="Assembly",
            TypeFullName="Type",
            MethodName=method,
            ElementCount=elementCount,
            Status=JobStatus.Completed,
            Mean="100 ns",
            MeanNanoseconds=100,
            AllocatedBytes=0,
            ExitCode=0,
            ArtifactFolder=string.Empty,
            StartedAt=DateTimeOffset.UtcNow,
            FinishedAt=DateTimeOffset.UtcNow,
        };

    private static string CreateTempRoot(){
        var root = Path.Combine(AppContext.BaseDirectory,"BenchmarkOrchestratorTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static ProgressState LoadOnlyState(string root){
        var runRoot = Directory.GetDirectories(root).Single();
        return new BenchmarkStateManager(root).Load(runRoot)!;
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

    private sealed class DelayedFakeRunner(Func<BenchmarkJob,int,Task<BenchmarkJobRunResult>> run):IBenchmarkJobRunner{
        public async Task<BenchmarkJobRunResult> RunAsync(
            BenchmarkJob job,
            string runRoot,
            int logicalCpu,
            TimeSpan timeout,
            CancellationToken cancellationToken){
            job.ArtifactFolder=Path.Combine(runRoot,"artifacts",job.JobId);
            return await run(job,logicalCpu);
        }
    }
}
