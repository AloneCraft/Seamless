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
                    Project="System.Array.FLinq",
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
                [Case("System.Array.FLinq","Where"),Case("System.Array.ZLinq","Where")],
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
    public async Task Orchestrator_RunAsync_CreatesNumberedRunRootAndNFiles(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => CompletedResult(job));
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        var result = await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where"),Case("System.Array.ZLinq","Where")],
                [0],
                [1,2],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(Path.Combine(root,"000001"),result.RunRoot);
        Assert.True(File.Exists(Path.Combine(result.RunRoot,"System.Array","N1.csv")));
        Assert.True(File.Exists(Path.Combine(result.RunRoot,"System.Array","N2.csv")));
        Assert.False(File.Exists(Path.Combine(result.RunRoot,"BenchmarkComparison-N1.csv")));
        var state = new BenchmarkStateManager(root).Load(result.RunRoot)!;
        Assert.Equal(1,state.RunNumber);
    }

    [Fact]
    public async Task Orchestrator_RunAsync_UsesSelectedRunRoot(){
        var root = CreateTempRoot();
        var runRoot = Path.Combine(root,"000001");
        var runner = new FakeRunner((job,_) => CompletedResult(job));
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        var result = await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where")],
                [0],
                [1],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                RunRootOverride: runRoot),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(runRoot,result.RunRoot);
        Assert.Single(Directory.GetDirectories(root));
        Assert.True(File.Exists(Path.Combine(runRoot,"System.Array","N1.csv")));
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
                    Case("System.Array.FLinq","Where"),
                    Case("System.Array.ZLinq","Where"),
                    Case("System.Array.FLinq","Select"),
                    Case("System.Array.ZLinq","Select"),
                    Case("System.Array.FLinq","Count"),
                    Case("System.Array.ZLinq","Count"),
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
    public async Task Orchestrator_RunAsync_PersistsExecutionProfileInStateAndSummary(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => CompletedResult(job));
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where")],
                [0],
                [128],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                ExecutionProfile: BenchmarkExecutionProfile.Fast),
            _ => { },
            _ => { },
            CancellationToken.None);

        var state = LoadOnlyState(root);
        var summary = File.ReadAllText(Path.Combine(state.RunRoot,"BenchmarkRunSummary.txt"));
        Assert.Equal(BenchmarkExecutionProfile.Fast,state.ExecutionProfile);
        Assert.Contains("ExecutionProfile: Fast",summary,StringComparison.Ordinal);
        Assert.Contains("AllocatedBytesSource: GC.GetAllocatedBytesForCurrentThread",summary,StringComparison.Ordinal);
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_AssignsMethodRowsToCpusAndRunsRowInElementProjectOrder(){
        var root = CreateTempRoot();
        var observed = new List<(string Method,string Project,int ElementCount,int Cpu)>();
        var gate = new object();
        var runner = new FakeRunner((job,cpu) => {
            lock(gate) {
                observed.Add((job.MethodName,job.Project,job.ElementCount,cpu));
            }

            return CompletedResult(job);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [
                    Case("System.Array.FLinq","Where"),
                    Case("System.Array.ZLinq","Where"),
                    Case("System.Array.FLinq","Select"),
                    Case("System.Array.ZLinq","Select"),
                    Case("System.Array.FLinq","Count"),
                    Case("System.Array.ZLinq","Count"),
                ],
                [10,20],
                [1,2],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                ExecutionProfile: BenchmarkExecutionProfile.Accurate),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.All(observed.Where(static item => item.Method=="Where"),static item => Assert.Equal(10,item.Cpu));
        Assert.All(observed.Where(static item => item.Method=="Select"),static item => Assert.Equal(20,item.Cpu));
        Assert.All(observed.Where(static item => item.Method=="Count"),static item => Assert.Equal(10,item.Cpu));
        Assert.Equal(
            [
                ("System.Array.FLinq",1),
                ("System.Array.ZLinq",1),
                ("System.Array.FLinq",2),
                ("System.Array.ZLinq",2),
            ],
            observed
                .Where(static item => item.Method=="Where")
                .Select(static item => (item.Project,item.ElementCount))
                .ToArray());
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_FastRunsUnpersistedWarmupBeforeMeasuredN(){
        var root = CreateTempRoot();
        var observed = new List<(bool Warmup,string Project,int ElementCount)>();
        var runner = new FakeRunner((job,_) => {
            observed.Add((job.JobId.EndsWith("|warmup",StringComparison.Ordinal),job.Project,job.ElementCount));
            return CompletedResult(job);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where"),Case("System.Array.ZLinq","Where")],
                [0],
                [1,2],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                ExecutionProfile: BenchmarkExecutionProfile.Fast),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(
            [
                (true,"System.Array.FLinq",1),
                (true,"System.Array.ZLinq",1),
                (false,"System.Array.FLinq",1),
                (false,"System.Array.ZLinq",1),
                (false,"System.Array.FLinq",2),
                (false,"System.Array.ZLinq",2),
            ],
            observed);
        var state = LoadOnlyState(root);
        Assert.Equal(4,state.Jobs.Count);
        Assert.DoesNotContain(state.Jobs,static job => job.JobId.EndsWith("|warmup",StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(state.RunRoot,"System.Array","N1.csv")));
        Assert.True(File.Exists(Path.Combine(state.RunRoot,"System.Array","N2.csv")));
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_AccurateDoesNotRunWarmup(){
        var root = CreateTempRoot();
        var observed = new List<bool>();
        var runner = new FakeRunner((job,_) => {
            observed.Add(job.JobId.EndsWith("|warmup",StringComparison.Ordinal));
            return CompletedResult(job);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where"),Case("System.Array.ZLinq","Where")],
                [0],
                [1],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                ExecutionProfile: BenchmarkExecutionProfile.Accurate),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal([false,false],observed);
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_WarmupFailurePersistsMeasuredNAsFailedAndStopsRow(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => job.JobId.EndsWith("|warmup",StringComparison.Ordinal)
            ? new BenchmarkJobRunResult(
                job.Project,
                job.MethodName,
                job.TypeFullName,
                job.ElementCount,
                "Failed",
                "Warmup failed",
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                1,
                job.ArtifactFolder,
                "warmup boom")
            : CompletedResult(job));
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where"),Case("System.Array.ZLinq","Where")],
                [0],
                [1,2],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                ExecutionProfile: BenchmarkExecutionProfile.Fast),
            _ => { },
            _ => { },
            CancellationToken.None);

        var state = LoadOnlyState(root);
        var job = Assert.Single(state.Jobs);
        Assert.Equal("System.Array.FLinq",job.Project);
        Assert.Equal(1,job.ElementCount);
        Assert.Equal(JobStatus.Failed,job.Status);
        Assert.Contains("warmup boom",job.FailureReason,StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(state.RunRoot,"System.Array","N1.csv")));
    }

    [Fact]
    public async Task Orchestrator_FixedMode_UsesChangedCpuSelectionForLaterJobs(){
        var root = CreateTempRoot();
        var cpus = new[] { 0 };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new List<int>();
        var gate = new object();
        var runner = new DelayedFakeRunner(async (job,cpu) => {
            lock(gate) {
                observed.Add(cpu);
            }

            if(job.MethodName=="A") {
                started.SetResult();
                await release.Task;
            }

            return CompletedResult(job);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);
        var run = orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","A"),Case("System.Array.FLinq","B")],
                [0],
                [1],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => cpus),
            _ => { },
            _ => { },
            CancellationToken.None);

        await started.Task;
        cpus=[1];
        release.SetResult();
        await run;

        Assert.Equal([0,1],observed);
    }

    [Fact]
    public async Task Orchestrator_FixedMode_RemovedCpuFinishesCurrentJobOnly(){
        var root = CreateTempRoot();
        var cpus = new[] { 0,1 };
        var cpuOneStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCpuOne = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new List<int>();
        var gate = new object();
        var runner = new DelayedFakeRunner(async (job,cpu) => {
            lock(gate) {
                observed.Add(cpu);
            }

            if(cpu==1) {
                cpuOneStarted.SetResult();
                await releaseCpuOne.Task;
            } else {
                await Task.Delay(40);
            }

            return CompletedResult(job);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);
        var run = orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","A"),Case("System.Array.FLinq","B"),Case("System.Array.FLinq","C"),Case("System.Array.FLinq","D")],
                [0,1],
                [1],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => cpus),
            _ => { },
            _ => { },
            CancellationToken.None);

        await cpuOneStarted.Task;
        cpus=[0];
        releaseCpuOne.SetResult();
        await run;

        var cpuOneRuns = observed.Count(static cpu => cpu==1);
        Assert.Equal(1,cpuOneRuns);
        Assert.All(LoadOnlyState(root).Jobs,static job => Assert.Equal(JobStatus.Completed,job.Status));
    }

    [Fact]
    public async Task Orchestrator_FixedMode_NotifiesDynamicRunnerWhenCpuSelectionChanges(){
        var root = CreateTempRoot();
        var cpus = new[] { 0,1 };
        var cpuOneStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCpuOne = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new DynamicFakeRunner(async (job,cpu) => {
            if(cpu==1) {
                cpuOneStarted.SetResult();
                await releaseCpuOne.Task;
            } else {
                await Task.Delay(40);
            }

            return CompletedResult(job);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);
        var run = orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","A"),Case("System.Array.FLinq","B"),Case("System.Array.FLinq","C"),Case("System.Array.FLinq","D")],
                [0,1],
                [1],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => cpus),
            _ => { },
            _ => { },
            CancellationToken.None);

        await cpuOneStarted.Task;
        cpus=[0];
        releaseCpuOne.SetResult();
        await run;

        Assert.Contains(runner.AppliedCpuSets,static item => item.SequenceEqual([0]));
    }

    [Fact]
    public async Task Orchestrator_FixedMode_AddedCpuJoinsRemainingJobs(){
        var root = CreateTempRoot();
        var cpus = new[] { 0 };
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new List<int>();
        var gate = new object();
        var runner = new DelayedFakeRunner(async (job,cpu) => {
            lock(gate) {
                observed.Add(cpu);
            }

            if(job.MethodName=="A") {
                firstStarted.SetResult();
                await releaseFirst.Task;
            } else {
                await Task.Delay(40);
            }

            return CompletedResult(job);
        });
        var orchestrator = new BenchmarkOrchestrator(new BenchmarkStateManager(root),new BenchmarkResultWriter(),runner);
        var run = orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","A"),Case("System.Array.FLinq","B"),Case("System.Array.FLinq","C"),Case("System.Array.FLinq","D")],
                [0],
                [1],
                BenchmarkRunMode.Fixed,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => cpus),
            _ => { },
            _ => { },
            CancellationToken.None);

        await firstStarted.Task;
        cpus=[0,1];
        releaseFirst.SetResult();
        await run;

        Assert.Contains(1,observed);
        Assert.All(LoadOnlyState(root).Jobs,static job => Assert.Equal(JobStatus.Completed,job.Status));
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
                CompletedJob("System.Array.FLinq","Where",128),
                CompletedJob("System.Array.ZLinq","Where",128),
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
                CompletedJob("System.Array.FLinq","Where",128),
                CompletedJob("System.Array.ZLinq","Where",128),
                CompletedJob("System.Array.FLinq","Where",256),
                CompletedJob("System.Array.ZLinq","Where",256),
            ],
        };

        await Task.WhenAll(Enumerable.Range(0,16).Select(_ => Task.Run(() => writer.WriteOutputs(state))));

        Assert.True(File.Exists(Path.Combine(runRoot,"System.Array","N128.csv")));
        Assert.True(File.Exists(Path.Combine(runRoot,"System.Array","N256.csv")));
        Assert.True(File.Exists(Path.Combine(runRoot,"BenchmarkRunSummary.txt")));
        Assert.Empty(Directory.GetFiles(runRoot,"*.tmp"));
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_ResumedStateDoesNotRunUnselectedProjects(){
        var root = CreateTempRoot();
        var stateManager = new BenchmarkStateManager(root);
        var runRoot = Path.Combine(root,"run");
        var state = new ProgressState{
            RunId="run",
            RunRoot=runRoot,
            Mode=BenchmarkRunMode.Adaptive,
            CreatedAt=DateTimeOffset.UtcNow,
            UpdatedAt=DateTimeOffset.UtcNow,
            ElementCounts=[1],
            AdaptiveThresholdNanoseconds=1_000_000,
            AdaptiveStates=[
                new AdaptiveBenchmarkState { MethodName="Where" },
            ],
            Jobs=[
                new BenchmarkJob{
                    JobId="system",
                    Project="System.Array.FLinq",
                    AssemblyName="Assembly",
                    TypeFullName="Type",
                    MethodName="Where",
                    ElementCount=1,
                    Status=JobStatus.Pending,
                },
                new BenchmarkJob{
                    JobId="persisted",
                    Project="Seamless.Persisted.FLinq",
                    AssemblyName="Assembly",
                    TypeFullName="Type",
                    MethodName="Where",
                    ElementCount=1,
                    Status=JobStatus.Pending,
                },
            ],
        };
        var observedProjects = new List<string>();
        var runner = new FakeRunner((job,_) => {
            observedProjects.Add(job.Project);
            return CompletedResult(job);
        });
        stateManager.Save(state);
        stateManager.SaveLatestRun(runRoot);
        var orchestrator = new BenchmarkOrchestrator(stateManager,new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where")],
                [0],
                [1],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                RunRootOverride: runRoot),
            _ => { },
            _ => { },
            CancellationToken.None);

        var loaded = stateManager.Load(runRoot)!;
        Assert.Equal(["System.Array.FLinq"],observedProjects);
        Assert.All(loaded.Jobs,static job => Assert.Equal("System.Array.FLinq",job.Project));
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_ResumedStateUsesRequestedThreshold(){
        var root = CreateTempRoot();
        var stateManager = new BenchmarkStateManager(root);
        var runRoot = Path.Combine(root,"run");
        var completedJob = CompletedJob("System.Array.FLinq","Where",1);
        completedJob.Mean="10.56 ms";
        completedJob.MeanNanoseconds=10_560_000;
        var state = new ProgressState{
            RunId="run",
            RunRoot=runRoot,
            Mode=BenchmarkRunMode.Adaptive,
            CreatedAt=DateTimeOffset.UtcNow,
            UpdatedAt=DateTimeOffset.UtcNow,
            ElementCounts=[1,2],
            AdaptiveThresholdNanoseconds=1_000_000,
            AdaptiveStates=[
                new AdaptiveBenchmarkState { MethodName="Where" },
            ],
            Jobs=[
                completedJob,
            ],
        };
        var observedElementCounts = new List<int>();
        var runner = new FakeRunner((job,_) => {
            observedElementCounts.Add(job.ElementCount);
            return CompletedResult(job);
        });
        stateManager.Save(state);
        stateManager.SaveLatestRun(runRoot);
        var orchestrator = new BenchmarkOrchestrator(stateManager,new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where")],
                [0],
                [1,2],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromMilliseconds(1024),
                TimeSpan.FromSeconds(30),
                RunRootOverride: runRoot),
            _ => { },
            _ => { },
            CancellationToken.None);

        var loaded = stateManager.Load(runRoot)!;
        Assert.Equal(1_024_000_000,loaded.AdaptiveThresholdNanoseconds);
        Assert.Equal([2],observedElementCounts);
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_RestartFromRequestedElementCountRerunsCompletedJobs(){
        var root = CreateTempRoot();
        var stateManager = new BenchmarkStateManager(root);
        var runRoot = Path.Combine(root,"run");
        var state = new ProgressState{
            RunId="run",
            RunRoot=runRoot,
            Mode=BenchmarkRunMode.Adaptive,
            CreatedAt=DateTimeOffset.UtcNow,
            UpdatedAt=DateTimeOffset.UtcNow,
            ElementCounts=[1,2],
            AdaptiveThresholdNanoseconds=1_000_000_000,
            AdaptiveStates=[
                new AdaptiveBenchmarkState { MethodName="Where" },
            ],
            Jobs=[
                CompletedJob("System.Array.FLinq","Where",1),
                CompletedJob("System.Array.FLinq","Where",2),
            ],
        };
        var observedElementCounts = new List<int>();
        var runner = new FakeRunner((job,_) => {
            observedElementCounts.Add(job.ElementCount);
            return CompletedResult(job);
        });
        stateManager.Save(state);
        stateManager.SaveLatestRun(runRoot);
        var orchestrator = new BenchmarkOrchestrator(stateManager,new BenchmarkResultWriter(),runner);

        await orchestrator.RunAsync(
            new BenchmarkRunRequest(
                [Case("System.Array.FLinq","Where")],
                [0],
                [1,2],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                RestartFromRequestedElementCount: true,
                RunRootOverride: runRoot),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal([1,2],observedElementCounts);
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_StopsPairWhenEitherSideExceedsThreshold(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => {
            var mean = job.ElementCount>=256&&job.Project=="System.Array.ZLinq" ? 2_000_000_000d : 100d;
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
                [Case("System.Array.FLinq","Where"),Case("System.Array.ZLinq","Where")],
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
        Assert.DoesNotContain(state.Jobs,static job => job.Project=="System.Array.FLinq"&&job.ElementCount==512);
        Assert.DoesNotContain(state.Jobs,static job => job.Project=="System.Array.ZLinq"&&job.ElementCount==512);
    }

    [Fact]
    public async Task Orchestrator_AdaptiveMode_StopsPairAfterBothSidesExceedThreshold(){
        var root = CreateTempRoot();
        var runner = new FakeRunner((job,_) => {
            var mean = job.ElementCount>=256&&job.Project.StartsWith("System",StringComparison.Ordinal)
                ? 2_000_000d
                : 100d;
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
                [Case("System.Array.FLinq","Where"),Case("System.Array.ZLinq","Where")],
                [0],
                [128,256,512],
                BenchmarkRunMode.Adaptive,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var state = LoadOnlyState(root);
        Assert.Contains(state.Jobs,static job => job.Project=="System.Array.FLinq"&&job.ElementCount==256);
        Assert.Contains(state.Jobs,static job => job.Project=="System.Array.ZLinq"&&job.ElementCount==256);
        Assert.DoesNotContain(state.Jobs,static job => job.Project=="System.Array.FLinq"&&job.ElementCount==512);
        Assert.DoesNotContain(state.Jobs,static job => job.Project=="System.Array.ZLinq"&&job.ElementCount==512);
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
                [Case("System.Array.FLinq","Where")],
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
                [Case("System.Array.FLinq","Where")],
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

    [Fact]
    public void BenchmarkSkippedDisplayLogic_RequiresBothPairSidesBeforeSelectedElementCount(){
        BenchmarkElementMeasurement[] measurements = [
            new(BenchmarkComparisonFormatter.SystemFLinq,"Where",128,"Completed",2_000_000),
            new(BenchmarkComparisonFormatter.SystemZLinq,"Where",256,"Completed",2_000_000),
            new(BenchmarkComparisonFormatter.PersistedFLinq,"Where",128,"Completed",2_000_000),
        ];

        var stoppedAt = BenchmarkSkippedDisplayLogic.FindPairStoppedAt(
            measurements,
            "Where",
            BenchmarkComparisonFormatter.SystemFLinq,
            512,
            1_000_000);
        var oneSided = BenchmarkSkippedDisplayLogic.FindPairStoppedAt(
            measurements,
            "Where",
            BenchmarkComparisonFormatter.PersistedFLinq,
            512,
            1_000_000);

        Assert.Equal(256,stoppedAt);
        Assert.Null(oneSided);
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

    private static BenchmarkJobRunResult CompletedResult(BenchmarkJob job) =>
        new(
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

    private sealed class DynamicFakeRunner(Func<BenchmarkJob,int,Task<BenchmarkJobRunResult>> run):IBenchmarkJobRunner,IBenchmarkDynamicCpuSetRunner{
        private readonly object gate = new();

        public List<int[]> AppliedCpuSets { get; } = [];

        public Task ApplyCpuSetAsync(IReadOnlyList<int> logicalCpus){
            lock(gate) {
                this.AppliedCpuSets.Add(logicalCpus.ToArray());
            }
            return Task.CompletedTask;
        }

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
