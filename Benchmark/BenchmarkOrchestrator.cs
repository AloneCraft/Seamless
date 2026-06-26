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
        var state = !string.IsNullOrWhiteSpace(request.RunRootOverride)
            ? this.stateManager.Load(request.RunRootOverride)??this.CreateState(request)
            : this.CreateState(request);
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
        var methodGroups = benchmarks
            .Select((benchmark,index) => new { Benchmark=benchmark, Index=index })
            .GroupBy(static item => item.Benchmark.MethodName,StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Min(static item => item.Index))
            .Select(static group => group.OrderBy(static item => item.Index).Select(static item => item.Benchmark).ToArray())
            .ToArray();
        foreach(var methodCases in methodGroups) {
            var cpuAssignments = CreateCpuAssignments(request,methodCases);
            foreach(var elementCount in state.ElementCounts) {
                cancellationToken.ThrowIfCancellationRequested();
                var activeCases = methodCases
                    .Where(benchmark => !IsAdaptivePairStopped(state,benchmarks,benchmark))
                    .ToArray();
                if(activeCases.Length==0) {
                    break;
                }

                var jobs = this.GetOrCreateAdaptiveJobs(state,activeCases,elementCount);
                var pending = jobs.Where(static job => job.Status==JobStatus.Pending).ToArray();
                if(pending.Length>0) {
                    statusChanged($"Adaptive {methodCases[0].MethodName} N={elementCount}: {pending.Length}/{activeCases.Length} cells");
                    await this.RunAssignedJobBatchAsync(state,pending,cpuAssignments,request,progress,cancellationToken);
                }

                if(jobs.Any(static job => job.Status==JobStatus.Pending||job.Status==JobStatus.Running)) {
                    break;
                }
            }
        }
    }

    private async Task RunAssignedJobBatchAsync(
        ProgressState state,
        IReadOnlyList<BenchmarkJob> jobs,
        IReadOnlyDictionary<string,int> cpuAssignments,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        CancellationToken cancellationToken) {
        var currentCpus = GetCurrentCpus(request);
        if(this.jobRunner is IBenchmarkDynamicCpuSetRunner dynamicCpuSetRunner) {
            await dynamicCpuSetRunner.ApplyCpuSetAsync(currentCpus);
        }

        var tasks = jobs
            .GroupBy(job => ResolveAssignedCpu(job,cpuAssignments,currentCpus))
            .Select(group => Task.Run(async () => {
                foreach(var job in group) {
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.RunJobAsync(state,job,group.Key,request.JobTimeout,progress,cancellationToken);
                }
            },cancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);
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
            if(this.jobRunner is IBenchmarkDynamicCpuSetRunner dynamicCpuSetRunner) {
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
        var runRoot = string.IsNullOrWhiteSpace(request.RunRootOverride)
            ? this.CreateNextRunRoot()
            : request.RunRootOverride;
        var state = new ProgressState {
            RunId=Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            Mode=request.Mode,
            ExecutionProfile=request.ExecutionProfile,
            RunRoot=runRoot,
            RunNumber=TryParseRunNumber(runRoot),
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

    private string CreateNextRunRoot(){
        Directory.CreateDirectory(this.stateManager.BenchmarkRoot);
        var next = Directory.EnumerateDirectories(this.stateManager.BenchmarkRoot)
            .Select(static path => TryParseRunNumber(path))
            .DefaultIfEmpty(0)
            .Max()+1;
        while(true){
            var runRoot = Path.Combine(this.stateManager.BenchmarkRoot,next.ToString("D6",CultureInfo.InvariantCulture));
            if(!Directory.Exists(runRoot)){
                Directory.CreateDirectory(runRoot);
                return runRoot;
            }

            next++;
        }
    }

    private static int TryParseRunNumber(string runRoot){
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(runRoot));
        return name.Length==6
            &&int.TryParse(name,NumberStyles.None,CultureInfo.InvariantCulture,out var number)
                ? number
                : 0;
    }

    private void ReconcileStateWithRequest(ProgressState state,BenchmarkRunRequest request) {
        if(state.RunNumber<=0){
            state.RunNumber=TryParseRunNumber(state.RunRoot);
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
        this.stateManager.Save(state);
        return state.Jobs
            .Where(job => job.ElementCount==elementCount&&cases.Any(item => SameCase(item,job)))
            .ToArray();
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

    private static bool IsAdaptivePairStopped(
        ProgressState state,
        IReadOnlyList<BenchmarkCase> benchmarks,
        BenchmarkCase benchmark) {
        if(IsAdaptiveCaseFailed(state,benchmark)) {
            return true;
        }

        var pairProjects = BenchmarkComparisonPairs.GetPairProjects(benchmark.Project);
        if(pairProjects is null) {
            return IsAdaptiveCaseOverThreshold(state,benchmark);
        }

        var pairCases = pairProjects
            .Select(project => benchmarks.FirstOrDefault(candidate =>
                string.Equals(candidate.Project,project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(candidate.MethodName,benchmark.MethodName,StringComparison.Ordinal)))
            .Where(static candidate => candidate is not null)
            .Select(static candidate => candidate!)
            .ToArray();
        if(pairCases.Length!=pairProjects.Count) {
            return IsAdaptiveCaseOverThreshold(state,benchmark);
        }

        return pairCases.All(pairCase => IsAdaptiveCaseOverThreshold(state,pairCase));
    }

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

    private static IReadOnlyDictionary<string,int> CreateCpuAssignments(
        BenchmarkRunRequest request,
        IReadOnlyList<BenchmarkCase> series) {
        var cpus = GetCurrentCpus(request);
        var result = new Dictionary<string,int>(StringComparer.Ordinal);
        for(var index=0;index<series.Count;index++) {
            result[CreateBenchmarkKey(series[index])]=cpus[index%cpus.Count];
        }

        return result;
    }

    private static int ResolveAssignedCpu(
        BenchmarkJob job,
        IReadOnlyDictionary<string,int> cpuAssignments,
        IReadOnlyList<int> currentCpus) {
        var key = CreateBenchmarkKey(job.ToBenchmarkCase());
        if(cpuAssignments.TryGetValue(key,out var assigned)&&currentCpus.Contains(assigned)) {
            return assigned;
        }

        return currentCpus[0];
    }

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
