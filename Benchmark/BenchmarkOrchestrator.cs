using System.Collections.Concurrent;
using System.Globalization;

namespace Benchmark;

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal sealed class BenchmarkOrchestrator {
    private readonly BenchmarkStateManager stateManager;
    private readonly BenchmarkResultWriter resultWriter;
    private readonly IBenchmarkJobRunner jobRunner;

    public BenchmarkOrchestrator(
        BenchmarkStateManager stateManager,
        BenchmarkResultWriter resultWriter,
        IBenchmarkJobRunner jobRunner) {
        this.stateManager=stateManager;
        this.resultWriter=resultWriter;
        this.jobRunner=jobRunner;
    }

    public async Task<BenchmarkOrchestratorResult> RunAsync(
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var state = this.stateManager.LoadLatestUnfinished()??this.CreateState(request);
        this.ReconcileStateWithRequest(state,request);
        this.stateManager.SaveLatestRun(state.RunRoot);
        this.stateManager.Save(state);

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
            this.stateManager.Save(state);
            throw;
        }

        var paths = this.resultWriter.WriteOutputs(state);
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
        var benchmarks = state.Jobs.Count>0
            ? state.Jobs
                .Select(static job => new BenchmarkCase(job.Project,job.AssemblyName,job.TypeFullName,job.MethodName))
                .GroupBy(CreateBenchmarkKey,StringComparer.Ordinal)
                .Select(static group => group.First())
                .ToArray()
            : request.Benchmarks;
        for(var elementCountIndex=0;elementCountIndex<state.ElementCounts.Count;elementCountIndex++) {
            cancellationToken.ThrowIfCancellationRequested();
            var elementCount = state.ElementCounts[elementCountIndex];
            var activeCases = benchmarks
                .Where(benchmark => !IsAdaptiveCaseStopped(state,benchmark))
                .OrderBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if(activeCases.Length==0) {
                break;
            }

            var jobs = this.GetOrCreateAdaptiveJobs(state,activeCases,elementCount);
            var pending = jobs.Where(static job => job.Status==JobStatus.Pending).ToArray();
            if(pending.Length>0) {
                statusChanged($"Adaptive N={elementCount}: {pending.Length}/{activeCases.Length} cells");
                await this.RunJobBatchAsync(state,pending,request,progress,cancellationToken);
            }

            if(jobs.Any(static job => job.Status==JobStatus.Pending||job.Status==JobStatus.Running)) {
                break;
            }
        }
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

            foreach(var cpu in GetCurrentCpus(request)) {
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
        this.stateManager.Save(state);
        progress(new BenchmarkProgressUpdate(state,job));

        try {
            var result = await this.jobRunner.RunAsync(job,state.RunRoot,logicalCpu,timeout,cancellationToken);
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
            this.stateManager.Save(state);
            try{
                _ = this.resultWriter.WriteOutputs(state);
            }catch(Exception ex){
                System.Diagnostics.Trace.WriteLine("Benchmark result output failed. "+ex);
            }
            progress(new BenchmarkProgressUpdate(state,job));
        }
    }

    private ProgressState CreateState(BenchmarkRunRequest request) {
        var now = DateTimeOffset.UtcNow;
        var runRoot = Path.Combine(
            this.stateManager.BenchmarkRoot,
            now.ToLocalTime().ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture));
        var state = new ProgressState {
            RunId=Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            Mode=request.Mode,
            RunRoot=runRoot,
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
            state.Jobs=this.CreateJobs(request.Benchmarks,state.ElementCounts).ToList();
        }

        return state;
    }

    private void ReconcileStateWithRequest(ProgressState state,BenchmarkRunRequest request) {
        if(state.Jobs.Count>0) {
            state.Jobs=state.Jobs
                .Where(job => request.Benchmarks.Any(benchmark => SameCase(benchmark,job)))
                .ToList();
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

    private IEnumerable<BenchmarkJob> CreateJobs(IEnumerable<BenchmarkCase> benchmarks,IEnumerable<int> elementCounts) {
        foreach(var elementCount in elementCounts) {
            foreach(var benchmark in benchmarks.OrderBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)) {
                yield return CreateJob(benchmark,elementCount);
            }
        }
    }

    private IReadOnlyList<BenchmarkJob> GetOrCreateAdaptiveJobs(ProgressState state,IReadOnlyList<BenchmarkCase> cases,int elementCount) {
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

            state.Jobs.Add(CreateJob(benchmark,elementCount));
        }
        this.stateManager.Save(state);
        return state.Jobs
            .Where(job => job.ElementCount==elementCount&&cases.Any(item => SameCase(item,job)))
            .ToArray();
    }

    private static BenchmarkJob CreateJob(BenchmarkCase benchmark,int elementCount) =>
        new() {
            JobId=CreateJobId(benchmark,elementCount),
            Project=benchmark.Project,
            AssemblyName=benchmark.AssemblyName,
            TypeFullName=benchmark.TypeFullName,
            MethodName=benchmark.MethodName,
            ElementCount=elementCount,
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

    private static bool IsAdaptiveCaseStopped(ProgressState state,BenchmarkCase benchmark) =>
        state.Jobs.Any(job =>
            SameCase(benchmark,job)
            &&(job.Status==JobStatus.Failed
                ||(job.Status==JobStatus.Completed
                    &&job.MeanNanoseconds is { } mean
                    &&mean>state.AdaptiveThresholdNanoseconds)));

    private static IReadOnlyList<int> NormalizeCpus(IReadOnlyList<int> cpus) =>
        cpus.Count==0 ? [0] : cpus.Distinct().Order().ToArray();

    private static IReadOnlyList<int> GetCurrentCpus(BenchmarkRunRequest request) =>
        NormalizeCpus(request.LogicalCpuProvider?.Invoke()??request.LogicalCpus);

    private static void ForceGcBeforeJob() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string FirstText(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;

    private static string FormatNanoseconds(double? value) =>
        value is { } number
            ? number.ToString("N3",CultureInfo.InvariantCulture)+" ns"
            : string.Empty;
}

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal static class BenchmarkElementCounts {
    public static IReadOnlyList<int> All { get; } =
        Enumerable.Range(0,24).Select(static exponent => 1<<exponent).ToArray();

    public static IReadOnlyList<int> DefaultInitial { get; } =
        [1];
}
