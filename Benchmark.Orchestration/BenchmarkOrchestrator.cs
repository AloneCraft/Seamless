using System.Collections.Concurrent;
using System.Globalization;

namespace Benchmark.Worker;

public sealed class BenchmarkOrchestrator {
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
        var cpus = NormalizeCpus(request.LogicalCpus);
        var workers = cpus.Select(cpu => Task.Run(async () => {
            while(queue.TryDequeue(out var job)) {
                cancellationToken.ThrowIfCancellationRequested();
                var current = Interlocked.Increment(ref completed);
                statusChanged($"Running {current}/{total}: {job.MethodName} N={job.ElementCount} / CPU {cpu}");
                await this.RunJobAsync(state,job,cpu,request.JobTimeout,progress,cancellationToken);
            }
        },cancellationToken)).ToArray();

        await Task.WhenAll(workers);
    }

    private async Task RunAdaptiveAsync(
        ProgressState state,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var casesByMethod = request.Benchmarks
            .GroupBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static item => item.Key,static item => item.ToArray(),StringComparer.OrdinalIgnoreCase);
        var cpus = NormalizeCpus(request.LogicalCpus);
        var methodIndex = 0;
        foreach(var methodState in state.AdaptiveStates.OrderBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)) {
            cancellationToken.ThrowIfCancellationRequested();
            methodIndex++;
            if(methodState.IsStopped||methodState.IsCompleted) {
                continue;
            }

            if(!casesByMethod.TryGetValue(methodState.MethodName,out var cases)) {
                methodState.IsStopped=true;
                methodState.Reason="Benchmark method is no longer selected.";
                this.stateManager.Save(state);
                continue;
            }

            while(methodState.NextElementCountIndex<state.ElementCounts.Count&&!methodState.IsStopped) {
                var elementCount = state.ElementCounts[methodState.NextElementCountIndex];
                var jobs = this.GetOrCreateAdaptiveJobs(state,cases,elementCount);
                var pending = jobs.Where(static job => job.Status==JobStatus.Pending).ToArray();
                if(pending.Length>0) {
                    statusChanged($"Adaptive {methodIndex}/{state.AdaptiveStates.Count}: {methodState.MethodName} N={elementCount}");
                    await this.RunJobBatchAsync(state,pending,cpus,request.JobTimeout,progress,cancellationToken);
                }

                var currentJobs = jobs.Where(job => job.ElementCount==elementCount).ToArray();
                if(currentJobs.Any(static job => job.Status==JobStatus.Pending||job.Status==JobStatus.Running)) {
                    break;
                }

                var stopJob = currentJobs.FirstOrDefault(job => job.Status==JobStatus.Failed)
                    ??currentJobs.FirstOrDefault(job =>
                        job.MeanNanoseconds is { } mean
                        &&mean>state.AdaptiveThresholdNanoseconds);
                if(stopJob is not null) {
                    methodState.IsStopped=true;
                    methodState.StoppedAtElementCount=elementCount;
                    methodState.TriggerProject=stopJob.Project;
                    methodState.TriggerMeanNanoseconds=stopJob.MeanNanoseconds;
                    methodState.Reason=stopJob.Status==JobStatus.Failed
                        ? FirstText(stopJob.FailureReason,"Job failed before the next element count.")
                        : $"Mean exceeded adaptive threshold: {FormatNanoseconds(stopJob.MeanNanoseconds)} > {FormatNanoseconds(state.AdaptiveThresholdNanoseconds)}.";
                    this.stateManager.Save(state);
                    break;
                }

                methodState.NextElementCountIndex++;
                methodState.IsCompleted=methodState.NextElementCountIndex>=state.ElementCounts.Count;
                this.stateManager.Save(state);
            }
        }
    }

    private async Task RunJobBatchAsync(
        ProgressState state,
        IReadOnlyList<BenchmarkJob> jobs,
        IReadOnlyList<int> cpus,
        TimeSpan timeout,
        Action<BenchmarkProgressUpdate> progress,
        CancellationToken cancellationToken) {
        var queue = new ConcurrentQueue<BenchmarkJob>(jobs);
        var workers = cpus.Select(cpu => Task.Run(async () => {
            while(queue.TryDequeue(out var job)) {
                cancellationToken.ThrowIfCancellationRequested();
                await this.RunJobAsync(state,job,cpu,timeout,progress,cancellationToken);
            }
        },cancellationToken)).ToArray();
        await Task.WhenAll(workers);
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

        try {
            var result = await this.jobRunner.RunAsync(job,state.RunRoot,logicalCpu,timeout,cancellationToken);
            job.Status=string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
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
                job.FailureReason=FirstText(result.Log,$"Worker returned status {result.Status}.");
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
            _ = this.resultWriter.WriteOutputs(state);
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

    private static bool SameCase(BenchmarkCase benchmark,BenchmarkJob job) =>
        string.Equals(benchmark.Project,job.Project,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(benchmark.AssemblyName,job.AssemblyName,StringComparison.Ordinal)
        &&string.Equals(benchmark.TypeFullName,job.TypeFullName,StringComparison.Ordinal)
        &&string.Equals(benchmark.MethodName,job.MethodName,StringComparison.Ordinal);

    private static IReadOnlyList<int> NormalizeCpus(IReadOnlyList<int> cpus) =>
        cpus.Count==0 ? [0] : cpus.Distinct().Order().ToArray();

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

public static class BenchmarkElementCounts {
    public static IReadOnlyList<int> All { get; } =
        Enumerable.Range(7,17).Select(static exponent => 1<<exponent).ToArray();

    public static IReadOnlyList<int> DefaultInitial { get; } =
        Enumerable.Range(7,8).Select(static exponent => 1<<exponent).ToArray();
}
