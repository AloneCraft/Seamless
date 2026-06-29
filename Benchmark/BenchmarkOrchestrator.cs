using System.Collections.Concurrent;
using System.Globalization;

namespace Benchmark;

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal sealed class BenchmarkOrchestrator(
    BenchmarkStateManager stateManager,
    BenchmarkResultWriter resultWriter,
    IBenchmarkJobRunner jobRunner) {
    private readonly Lock stateGate = new();
    public async Task<BenchmarkOrchestratorResult> RunAsync(
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var state = !string.IsNullOrWhiteSpace(request.RunRootOverride)
            ? stateManager.Load(request.RunRootOverride)??this.CreateState(request)
            : this.CreateState(request);
        this.ReconcileStateWithRequest(state,request);
        stateManager.SaveLatestRun(state.RunRoot);
        stateManager.Save(state);

        try {
            if(state.Mode==BenchmarkRunMode.Adaptive) {
                await this.RunAdaptiveAsync(state,request,progress,statusChanged,cancellationToken);
            } else {
                await this.RunFixedAsync(state,request,progress,statusChanged,cancellationToken);
            }
        } catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            foreach(var job in state.Jobs.Where(static job => job.Status==JobStatus.Running)) {
                job.Status=JobStatus.Pending;
                job.AssignedLogicalCpu=null;
                job.StartedAt=null;
                job.FailureReason=string.Empty;
            }
            stateManager.Save(state);
            throw;
        }

        var paths = resultWriter.WriteOutputs(state);
        return new BenchmarkOrchestratorResult(state.RunRoot,paths);
    }

    private async Task RunFixedAsync(
        ProgressState state,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var queue = new ConcurrentQueue<BenchmarkJob>(state.Jobs.Where(static job => job.Status==JobStatus.Pending));
        var total = state.Jobs.Count(static job => job.Status is JobStatus.Pending or JobStatus.Completed or JobStatus.Failed);
        var completed = state.Jobs.Count(static job => job.Status is JobStatus.Completed or JobStatus.Failed);
        await this.RunDynamicJobBatchAsync(
            state,
            queue,
            request,
            progress,
            (job,cpu) => {
                var current = Interlocked.Increment(ref completed);
                statusChanged($"Running {current}/{total}: {job.MethodName} N={job.ElementCount} / CPU {cpu}");
            },
            cancellationToken);
    }

    private async Task RunAdaptiveAsync(
        ProgressState state,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var benchmarks = request.Benchmarks
            .Concat(state.Jobs.Select(static job => new BenchmarkCase(job.Project,job.AssemblyName,job.TypeFullName,job.MethodName)))
            .GroupBy(CreateBenchmarkKey,StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
        var methodGroups = benchmarks
            .Select((benchmark,index) => new { Benchmark=benchmark, Index=index })
            .GroupBy(static item => item.Benchmark.MethodName,StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Min(static item => item.Index))
            .Select(static group => group.OrderBy(static item => item.Index).Select(static item => item.Benchmark).ToArray())
            .ToArray();
        var rows = methodGroups
            .Select(static methodCases => new AdaptiveMethodRow(
                methodCases[0].MethodName,
                methodCases))
            .ToArray();
        var queue = new ConcurrentQueue<AdaptiveMethodRow>(rows);
        await this.RunDynamicAdaptiveRowsAsync(
            state,
            queue,
            request,
            progress,
            statusChanged,
            cancellationToken);
    }

    private async Task RunDynamicAdaptiveRowsAsync(
        ProgressState state,
        ConcurrentQueue<AdaptiveMethodRow> queue,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var running = 0;
        var workers = new Dictionary<int,Task>();
        while(true) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach(var worker in workers.Where(static item => item.Value.IsCompleted).ToArray()) {
                if(worker.Value.IsFaulted) {
                    await worker.Value;
                }

                workers.Remove(worker.Key);
            }

            var currentCpus = GetCurrentCpus(request);
            if(jobRunner is IBenchmarkDynamicCpuSetRunner dynamicCpuSetRunner) {
                await dynamicCpuSetRunner.ApplyCpuSetAsync(currentCpus);
            }

            foreach(var cpu in currentCpus) {
                if(workers.ContainsKey(cpu)) {
                    continue;
                }

                if(!queue.TryDequeue(out var row)) {
                    continue;
                }

                workers.Add(cpu,Task.Run(async () => {
                    Interlocked.Increment(ref running);
                    try {
                        await this.RunAdaptiveMethodRowAsync(state,row,cpu,request,progress,statusChanged,cancellationToken);
                    } finally {
                        Interlocked.Decrement(ref running);
                    }
                },cancellationToken));
            }

            if(queue.IsEmpty&&Volatile.Read(ref running)==0) {
                break;
            }

            await Task.Delay(25,cancellationToken);
        }

        await Task.WhenAll(workers.Values);
    }

    private async Task RunAdaptiveMethodRowAsync(
        ProgressState state,
        AdaptiveMethodRow row,
        int logicalCpu,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var warmedUp = false;
        foreach(var elementCount in state.ElementCounts) {
            cancellationToken.ThrowIfCancellationRequested();
            if(IsAdaptiveRowStopped(state,row.Cases)) {
                break;
            }

            var existingJobs = this.GetExistingAdaptiveJobs(state,row.Cases,elementCount);
            if(existingJobs.Count==row.Cases.Count&&existingJobs.All(static job => job.Status!=JobStatus.Pending)) {
                continue;
            }

            if(!warmedUp&&ShouldRunWarmup(request.ExecutionProfile)) {
                warmedUp=true;
                var warmupFailure = await this.RunWarmupRowAsync(
                    state,
                    row,
                    logicalCpu,
                    request,
                    statusChanged,
                    cancellationToken);
                if(warmupFailure is not null) {
                    this.MarkWarmupFailure(
                        state,
                        warmupFailure.Benchmark,
                        elementCount,
                        logicalCpu,
                        warmupFailure.FailureReason,
                        progress);
                    break;
                }
            }

            var jobs = this.GetOrCreateAdaptiveJobs(state,row.Cases,elementCount);
            var pending = jobs
                .Where(static job => job.Status==JobStatus.Pending)
                .OrderBy(job => IndexOfCase(row.Cases,job))
                .ToArray();
            if(pending.Length==0) {
                continue;
            }

            foreach(var job in pending) {
                cancellationToken.ThrowIfCancellationRequested();
                statusChanged($"Running {job.MethodName} N={job.ElementCount}: {job.Project} / CPU {logicalCpu}");
                await this.RunJobAsync(state,job,logicalCpu,request.JobTimeout,progress,cancellationToken);
            }

            if(jobs.Any(static job => job.Status==JobStatus.Pending||job.Status==JobStatus.Running)) {
                break;
            }
        }
    }

    private async Task<WarmupFailure?> RunWarmupRowAsync(
        ProgressState state,
        AdaptiveMethodRow row,
        int logicalCpu,
        BenchmarkRunRequest request,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        foreach(var benchmark in row.Cases) {
            cancellationToken.ThrowIfCancellationRequested();
            var warmupJob = CreateJob(benchmark,1,state.SourceTab,state.ElementCounts);
            warmupJob.JobId=warmupJob.JobId+"|warmup";
            statusChanged($"Warmup {benchmark.MethodName} N=1: {benchmark.Project} / CPU {logicalCpu}");
            ForceGcBeforeJob();
            try {
                var result = await jobRunner.RunAsync(
                    warmupJob,
                    state.RunRoot,
                    logicalCpu,
                    request.JobTimeout,
                    cancellationToken);
                if(!IsCompletedMeasurement(result)) {
                    return new WarmupFailure(
                        benchmark,
                        FirstText(
                            result.Log,
                            string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                                ? "Warmup completed without benchmark statistics."
                                : $"Warmup returned status {result.Status}."));
                }

                TryDeleteDirectory(result.ArtifactFolder);
            } catch(Exception ex) when(ex is not OperationCanceledException) {
                return new WarmupFailure(benchmark,ex.ToString());
            }
        }

        return null;
    }

    private async Task RunJobBatchAsync(
        ProgressState state,
        IReadOnlyList<BenchmarkJob> jobs,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        CancellationToken cancellationToken) {
        var queue = new ConcurrentQueue<BenchmarkJob>(jobs);
        await this.RunDynamicJobBatchAsync(state,queue,request,progress,null,cancellationToken);
    }

    private async Task RunDynamicJobBatchAsync(
        ProgressState state,
        ConcurrentQueue<BenchmarkJob> queue,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<BenchmarkJob,int>? jobStarting,
        CancellationToken cancellationToken) {
        var running = 0;
        var workers = new Dictionary<int,Task>();
        while(true) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach(var worker in workers.Where(static item => item.Value.IsCompleted).ToArray()) {
                if(worker.Value.IsFaulted) {
                    await worker.Value;
                }

                workers.Remove(worker.Key);
            }

            var currentCpus = GetCurrentCpus(request);
            if(jobRunner is IBenchmarkDynamicCpuSetRunner dynamicCpuSetRunner) {
                await dynamicCpuSetRunner.ApplyCpuSetAsync(currentCpus);
            }

            foreach(var cpu in currentCpus) {
                if(!workers.ContainsKey(cpu)) {
                    workers.Add(cpu,Task.Run(() => this.RunDynamicCpuWorkerAsync(
                        state,
                        queue,
                        request,
                        cpu,
                        progress,
                        jobStarting,
                        () => Interlocked.Increment(ref running),
                        () => Interlocked.Decrement(ref running),
                        cancellationToken),cancellationToken));
                }
            }

            if(queue.IsEmpty&&Volatile.Read(ref running)==0) {
                break;
            }

            await Task.Delay(25,cancellationToken);
        }

        await Task.WhenAll(workers.Values);
    }

    private async Task RunDynamicCpuWorkerAsync(
        ProgressState state,
        ConcurrentQueue<BenchmarkJob> queue,
        BenchmarkRunRequest request,
        int logicalCpu,
        Action<BenchmarkProgressUpdate> progress,
        Action<BenchmarkJob,int>? jobStarting,
        Action started,
        Action finished,
        CancellationToken cancellationToken) {
        while(GetCurrentCpus(request).Contains(logicalCpu)) {
            if(!queue.TryDequeue(out var job)) {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            started();
            try {
                jobStarting?.Invoke(job,logicalCpu);
                await this.RunJobAsync(state,job,logicalCpu,request.JobTimeout,progress,cancellationToken);
            } finally {
                finished();
            }
        }
    }

    private async Task RunJobAsync(
        ProgressState state,
        BenchmarkJob job,
        int logicalCpu,
        TimeSpan timeout,
        Action<BenchmarkProgressUpdate> progress,
        CancellationToken cancellationToken) {
        ForceGcBeforeJob();
        job.Status=JobStatus.Running;
        job.AssignedLogicalCpu=logicalCpu;
        job.StartedAt=DateTimeOffset.UtcNow;
        job.FinishedAt=null;
        job.FailureReason=string.Empty;
        stateManager.Save(state);
        progress(new BenchmarkProgressUpdate(state,job));

        try {
            var result = await jobRunner.RunAsync(job,state.RunRoot,logicalCpu,timeout,cancellationToken);
            var completed = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null;
            job.Status=completed
                ? JobStatus.Completed
                : JobStatus.Failed;
            job.Mean=result.Mean;
            job.Error=result.Error;
            job.StdDev=result.StdDev;
            job.MeanNanoseconds=result.MeanNanoseconds;
            job.ErrorNanoseconds=result.ErrorNanoseconds;
            job.StdDevNanoseconds=result.StdDevNanoseconds;
            job.AllocatedBytes=result.AllocatedBytes;
            job.RandomSeed=result.RandomSeed;
            job.ResultDigest=result.ResultDigest;
            job.ExitCode=result.ExitCode;
            job.ArtifactFolder=result.ArtifactFolder;
            if(job.Status==JobStatus.Failed) {
                job.FailureReason=FirstText(
                    result.Log,
                    string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                        ? "Worker completed without benchmark statistics."
                        : $"Worker returned status {result.Status}.");
            }
        } catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            job.Status=JobStatus.Pending;
            job.AssignedLogicalCpu=null;
            job.StartedAt=null;
            throw;
        } catch(Exception ex) {
            job.Status=JobStatus.Failed;
            job.ExitCode=1;
            job.FailureReason=ex.ToString();
        } finally {
            if(job.Status!=JobStatus.Pending) {
                job.FinishedAt=DateTimeOffset.UtcNow;
            }
            stateManager.Save(state);
            try{
                _ = resultWriter.WriteOutputs(state);
            }catch(Exception ex){
                System.Diagnostics.Trace.WriteLine("Benchmark result output failed. "+ex);
            }
            progress(new BenchmarkProgressUpdate(state,job));
        }
    }

    private ProgressState CreateState(BenchmarkRunRequest request) {
        var now = DateTimeOffset.UtcNow;
        var runRoot = string.IsNullOrWhiteSpace(request.RunRootOverride)
            ? this.CreateNextRunRoot()
            : request.RunRootOverride;
        var state = new ProgressState {
            RunId=Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            Mode=request.Mode,
            ExecutionProfile=request.ExecutionProfile,
            RunRoot=runRoot,
            RunNumber=BenchmarkStateManager.TryParseRunNumber(runRoot),
            SourceTab=request.SourceTab,
            ElementCounts=request.ElementCounts.Distinct().Order().ToList(),
            AdaptiveThresholdNanoseconds=request.AdaptiveThreshold.TotalMilliseconds*1_000_000,
            CreatedAt=now,
            UpdatedAt=now,
        };
        if(state.ElementCounts.Count==0) {
            state.ElementCounts=BenchmarkElementCounts.DefaultInitial.ToList();
        }

        if(request.Mode==BenchmarkRunMode.Adaptive) {
            state.AdaptiveStates=request.Benchmarks
                .Select(static item => item.MethodName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static method => method,StringComparer.OrdinalIgnoreCase)
                .Select(static method => new AdaptiveBenchmarkState { MethodName=method })
                .ToList();
        } else {
            state.Jobs=this.CreateJobs(request.Benchmarks,state.ElementCounts,request.SourceTab).ToList();
        }

        return state;
    }

    private string CreateNextRunRoot() => stateManager.CreateNextRunRoot();

    private void ReconcileStateWithRequest(ProgressState state,BenchmarkRunRequest request) {
        if(state.RunNumber<=0){
            state.RunNumber=BenchmarkStateManager.TryParseRunNumber(state.RunRoot);
        }
        state.Mode=request.Mode;
        state.ExecutionProfile=request.ExecutionProfile;
        state.SourceTab=request.SourceTab;
        state.AdaptiveThresholdNanoseconds=request.AdaptiveThreshold.TotalMilliseconds*1_000_000;
        state.ElementCounts=request.ElementCounts.Distinct().Order().ToList();
        if(state.ElementCounts.Count==0){
            state.ElementCounts=BenchmarkElementCounts.DefaultInitial.ToList();
        }
        if(state.Jobs.Count>0) {
            state.Jobs=state.Jobs
                .Where(job => request.Benchmarks.Any(benchmark => SameCase(benchmark,job)))
                .ToList();
        }

        if(request.RestartFromRequestedElementCount&&state.ElementCounts.Count>0){
            ResetJobsFromElementCount(state,state.ElementCounts[0]);
        }

        if(state.Mode==BenchmarkRunMode.Adaptive) {
            var requestedMethods = request.Benchmarks
                .Select(static benchmark => benchmark.MethodName)
                .ToHashSet(StringComparer.Ordinal);
            state.AdaptiveStates=state.AdaptiveStates
                .Where(adaptiveState => requestedMethods.Contains(adaptiveState.MethodName))
                .ToList();
            foreach(var method in requestedMethods.Where(method => state.AdaptiveStates.All(item => !string.Equals(item.MethodName,method,StringComparison.Ordinal)))) {
                state.AdaptiveStates.Add(new AdaptiveBenchmarkState { MethodName=method });
            }
        }
    }

    private static void ResetJobsFromElementCount(ProgressState state,int elementCount){
        foreach(var job in state.Jobs.Where(job => job.ElementCount>=elementCount)){
            job.Status=JobStatus.Pending;
            job.AssignedLogicalCpu=null;
            job.StartedAt=null;
            job.FinishedAt=null;
            job.FailureReason=string.Empty;
            job.Mean=string.Empty;
            job.Error=string.Empty;
            job.StdDev=string.Empty;
            job.MeanNanoseconds=null;
            job.ErrorNanoseconds=null;
            job.StdDevNanoseconds=null;
            job.AllocatedBytes=null;
            job.ExitCode=0;
            job.ArtifactFolder=string.Empty;
        }
    }

    private IEnumerable<BenchmarkJob> CreateJobs(IEnumerable<BenchmarkCase> benchmarks,IEnumerable<int> elementCounts,BenchmarkSourceTab sourceTab) {
        foreach(var benchmark in benchmarks) {
            foreach(var elementCount in elementCounts) {
                yield return CreateJob(benchmark,elementCount,sourceTab,elementCounts);
            }
        }
    }

    private IReadOnlyList<BenchmarkJob> GetOrCreateAdaptiveJobs(ProgressState state,IReadOnlyList<BenchmarkCase> cases,int elementCount) {
        lock(this.stateGate) {
            var existing = state.Jobs
                .Where(job => job.ElementCount==elementCount&&cases.Any(item => SameCase(item,job)))
                .ToArray();
            if(existing.Length==cases.Count) {
                return existing;
            }

            foreach(var benchmark in cases) {
                if(state.Jobs.Any(job => job.ElementCount==elementCount&&SameCase(benchmark,job))) {
                    continue;
                }

                state.Jobs.Add(CreateJob(benchmark,elementCount,state.SourceTab,state.ElementCounts));
            }

            stateManager.Save(state);
            return state.Jobs
                .Where(job => job.ElementCount==elementCount&&cases.Any(item => SameCase(item,job)))
                .ToArray();
        }
    }

    private IReadOnlyList<BenchmarkJob> GetExistingAdaptiveJobs(ProgressState state,IReadOnlyList<BenchmarkCase> cases,int elementCount) {
        lock(this.stateGate) {
            return state.Jobs
                .Where(job => job.ElementCount==elementCount&&cases.Any(item => SameCase(item,job)))
                .ToArray();
        }
    }

    private void MarkWarmupFailure(
        ProgressState state,
        BenchmarkCase benchmark,
        int elementCount,
        int logicalCpu,
        string failureReason,
        Action<BenchmarkProgressUpdate> progress) {
        BenchmarkJob job;
        lock(this.stateGate) {
            job=state.Jobs.FirstOrDefault(item => item.ElementCount==elementCount&&SameCase(benchmark,item))
                ??CreateJob(benchmark,elementCount,state.SourceTab,state.ElementCounts);
            if(!state.Jobs.Contains(job)) {
                state.Jobs.Add(job);
            }

            job.Status=JobStatus.Failed;
            job.AssignedLogicalCpu=logicalCpu;
            job.StartedAt=DateTimeOffset.UtcNow;
            job.FinishedAt=DateTimeOffset.UtcNow;
            job.Mean="Warmup failed";
            job.Error=string.Empty;
            job.StdDev=string.Empty;
            job.MeanNanoseconds=null;
            job.ErrorNanoseconds=null;
            job.StdDevNanoseconds=null;
            job.AllocatedBytes=null;
            job.ExitCode=1;
            job.FailureReason=FirstText(failureReason,"Warmup failed.");
            stateManager.Save(state);
            try{
                _ = resultWriter.WriteOutputs(state);
            }catch(Exception ex){
                System.Diagnostics.Trace.WriteLine("Benchmark result output failed. "+ex);
            }
        }

        progress(new BenchmarkProgressUpdate(state,job));
    }

    private static BenchmarkJob CreateJob(
        BenchmarkCase benchmark,
        int elementCount,
        BenchmarkSourceTab sourceTab,
        IEnumerable<int> preparedElementCounts) =>
        new() {
            JobId=CreateJobId(benchmark,elementCount),
            Project=benchmark.Project,
            AssemblyName=benchmark.AssemblyName,
            TypeFullName=benchmark.TypeFullName,
            MethodName=benchmark.MethodName,
            SourceTab=sourceTab,
            ElementCount=elementCount,
            PreparedElementCounts=preparedElementCounts.Distinct().Order().ToList(),
        };

    private static string CreateJobId(BenchmarkCase benchmark,int elementCount) =>
        string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,elementCount);

    private static string CreateBenchmarkKey(BenchmarkCase benchmark) =>
        string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName);

    private static bool SameCase(BenchmarkCase benchmark,BenchmarkJob job) =>
        string.Equals(benchmark.Project,job.Project,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(benchmark.AssemblyName,job.AssemblyName,StringComparison.Ordinal)
        &&string.Equals(benchmark.TypeFullName,job.TypeFullName,StringComparison.Ordinal)
        &&string.Equals(benchmark.MethodName,job.MethodName,StringComparison.Ordinal);

    private static int IndexOfCase(IReadOnlyList<BenchmarkCase> cases,BenchmarkJob job) {
        for(var index=0;index<cases.Count;index++) {
            if(SameCase(cases[index],job)) {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static bool IsAdaptiveRowStopped(ProgressState state,IReadOnlyList<BenchmarkCase> cases) =>
        cases.Any(benchmark => IsAdaptiveCaseFailed(state,benchmark)||IsAdaptiveCaseOverThreshold(state,benchmark));

    private static bool IsAdaptiveCaseFailed(ProgressState state,BenchmarkCase benchmark) =>
        state.Jobs.Any(job => SameCase(benchmark,job)&&job.Status==JobStatus.Failed);

    private static bool IsAdaptiveCaseOverThreshold(ProgressState state,BenchmarkCase benchmark) =>
        state.Jobs.Any(job =>
            SameCase(benchmark,job)
            &&job.Status==JobStatus.Completed
            &&job.MeanNanoseconds is { } mean
            &&mean>state.AdaptiveThresholdNanoseconds);

    private static IReadOnlyList<int> NormalizeCpus(IReadOnlyList<int> cpus) =>
        cpus.Count==0 ? [0] : cpus.Distinct().Order().ToArray();

    private static IReadOnlyList<int> GetCurrentCpus(BenchmarkRunRequest request) =>
        NormalizeCpus(request.LogicalCpuProvider?.Invoke()??request.LogicalCpus);

    private static bool ShouldRunWarmup(BenchmarkExecutionProfile executionProfile) =>
        executionProfile is BenchmarkExecutionProfile.Fast or BenchmarkExecutionProfile.Intermediate;

    private static bool IsCompletedMeasurement(BenchmarkJobRunResult result) =>
        string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
        &&result.MeanNanoseconds is not null;

    private static void ForceGcBeforeJob() {
        GC.Collect();
    }

    private static void TryDeleteDirectory(string path) {
        try{
            if(!string.IsNullOrWhiteSpace(path)&&Directory.Exists(path)){
                Directory.Delete(path,recursive:true);
            }
        }catch(IOException){
        }catch(UnauthorizedAccessException){
        }
    }

    private static string FirstText(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;

    private sealed record AdaptiveMethodRow(
        string MethodName,
        IReadOnlyList<BenchmarkCase> Cases);

    private sealed record WarmupFailure(
        BenchmarkCase Benchmark,
        string FailureReason);
}

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal static class BenchmarkElementCounts {
    internal const int BaseElementCount = 4096;

    public static IReadOnlyList<int> All { get; } =
        Enumerable.Range(10,14).Select(static exponent => 1<<exponent).ToArray();

    public static IReadOnlyList<int> DefaultInitial { get; } =
        [BaseElementCount];
}
