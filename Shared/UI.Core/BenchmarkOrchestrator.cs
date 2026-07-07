using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class BenchmarkOrchestrator(
    BenchmarkStateManager stateManager,
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
            await this.RunAdaptiveAsync(state,request,progress,statusChanged,cancellationToken);
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

        return new BenchmarkOrchestratorResult(state.RunRoot,state);
    }

    private async Task RunAdaptiveAsync(
        ProgressState state,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var queue = new ConcurrentQueue<AdaptiveMethodRow>();
        var queuedKeys = new global::System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completedKeys = new global::System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await this.RunDynamicAdaptiveRowsAsync(
            state,
            queue,
            queuedKeys,
            completedKeys,
            request,
            progress,
            statusChanged,
            cancellationToken);
    }

    private async Task RunDynamicAdaptiveRowsAsync(
        ProgressState state,
        ConcurrentQueue<AdaptiveMethodRow> queue,
        global::System.Collections.Generic.HashSet<string> queuedKeys,
        global::System.Collections.Generic.HashSet<string> completedKeys,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        var running = 0;
        var workers = new Dictionary<int,AdaptiveWorker>();
        try {
            while(true) {
                cancellationToken.ThrowIfCancellationRequested();
                foreach(var worker in workers.Where(static item => item.Value.IsCompleted).ToArray()) {
                    if(worker.Value.Task.IsFaulted) {
                        await worker.Value.Task;
                    }

                    lock(queuedKeys) {
                        completedKeys.Add(worker.Value.RowKey);
                    }

                    workers.Remove(worker.Key);
                }

                this.EnqueueCurrentAdaptiveRows(state,queue,queuedKeys,completedKeys,request);
                var currentCpus = GetCurrentCpus(request);
                if(jobRunner is IBenchmarkDynamicCpuSetRunner dynamicCpuSetRunner) {
                    await dynamicCpuSetRunner.ApplyCpuSetAsync(currentCpus);
                }

                foreach(var cpu in currentCpus) {
                    if(workers.ContainsKey(cpu)) {
                        continue;
                    }

                    if(!this.TryDequeueCurrentAdaptiveRow(state,queue,queuedKeys,request,out var row)) {
                        continue;
                    }

                    workers.Add(cpu,new AdaptiveWorker(row.Key,Task.Run(async () => {
                        Interlocked.Increment(ref running);
                        Trace.WriteLine($"[Orchestrator] worker start cpu={cpu} row={row.Key}");
                        try {
                            await this.RunAdaptiveMethodRowAsync(state,row,cpu,request,progress,statusChanged,cancellationToken);
                            Trace.WriteLine($"[Orchestrator] worker end cpu={cpu} row={row.Key}");
                        } catch(Exception ex) {
                            Trace.WriteLine($"[Orchestrator] worker FAULTED cpu={cpu} row={row.Key}: {ex}");
                            throw;
                        } finally {
                            Interlocked.Decrement(ref running);
                        }
                    },cancellationToken)));
                }

                if(queue.IsEmpty&&Volatile.Read(ref running)==0&&workers.Count==0) {
                    break;
                }

                await Task.Delay(25,cancellationToken);
            }

            await Task.WhenAll(workers.Values.Select(static worker => worker.Task));
        } finally {
            // キャンセルで while を抜けた場合でも、置き去りのワーカーが自身の状態復帰
            // (Pendingへの巻き戻し・state保存)を終えるまで待つ。待たずに戻ると、呼び出し元の
            // Pause完了直後にResumeした際、まだRunningのジョブをBenchmarkStateManager.Loadの
            // クラッシュ復旧ロジックがFailedへ誤確定させてしまう。
            try {
                await Task.WhenAll(workers.Values.Select(static worker => worker.Task)).ConfigureAwait(false);
            } catch {
                // 個々のworkerの例外はここでは無視する(状態復帰は各worker自身で完了済み)。
                // 元の例外(キャンセル等)を隠さないため握りつぶす。
            }
        }
    }

    private async Task RunAdaptiveMethodRowAsync(
        ProgressState state,
        AdaptiveMethodRow row,
        int logicalCpu,
        BenchmarkRunRequest request,
        Action<BenchmarkProgressUpdate> progress,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        // 1行=1(プロジェクト×メソッド)ケース。割り当てられた1コア上で、その1ケースを軸(LoopCount/ElementCount)
        // 昇順に自身が閾値超過/失敗するまで実行する。他ケースは空きコアが独立に担当する。
        foreach(var benchmark in row.Cases) {
            cancellationToken.ThrowIfCancellationRequested();
            var warmedUp = false;
            foreach(var elementCount in EnumerateAdaptiveElementCounts(state,row)) {
                cancellationToken.ThrowIfCancellationRequested();
                var benchmarkCase = ResolveAdaptiveCase(row,benchmark,elementCount);
                if(benchmarkCase is null) {
                    continue;
                }

                EnsureAdaptiveElementCount(state,elementCount);
                if(this.IsAdaptiveCaseStopped(state,row.SourceTab,benchmarkCase)) {
                    break;
                }

                var existingJobs = this.GetExistingAdaptiveJobs(state,[benchmarkCase],elementCount);
                if(existingJobs.Count>0&&existingJobs.All(static job => job.Status!=JobStatus.Pending)) {
                    continue;
                }

                if(!warmedUp&&!IsScalableSourceTab(row.SourceTab)&&ShouldRunWarmup(request.ExecutionProfile)) {
                    warmedUp=true;
                    var warmupFailure = await this.RunWarmupCaseAsync(
                        state,
                        benchmarkCase,
                        row.SourceTab,
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

                var jobs = this.GetOrCreateAdaptiveJobs(state,[benchmarkCase],elementCount);
                var job = jobs.FirstOrDefault(static job => job.Status==JobStatus.Pending);
                if(job is null) {
                    continue;
                }

                statusChanged($"実行中 {job.MethodName} {CreateAxisStatusText(job)}: {job.Project} / CPU {logicalCpu}");
                await this.RunJobAsync(state,job,logicalCpu,request.JobTimeout,progress,cancellationToken);
                if(job.Status is JobStatus.Pending or JobStatus.Running) {
                    break;
                }
            }
        }
    }

    private static BenchmarkCase? ResolveAdaptiveCase(AdaptiveMethodRow row,BenchmarkCase benchmark,int elementCount) {
        if(!IsScalableSourceTab(row.SourceTab)) {
            return benchmark;
        }

        if(benchmark.CountArgument==elementCount) {
            return benchmark;
        }

        return benchmark.CountArgument>0
            ? CloneWithCountArgument(benchmark,elementCount)
            : null;
    }

    private static IEnumerable<int> EnumerateAdaptiveElementCounts(ProgressState state,AdaptiveMethodRow row) {
        var seeds = GetAdaptiveElementCounts(state,row)
            .Where(static count => count>0)
            .Distinct()
            .Order()
            .ToArray();
        var last = 0;
        foreach(var count in seeds) {
            last=count;
            yield return count;
        }

        if(last!=BenchmarkElementCounts.All[^1]) {
            yield break;
        }

        while(last>0&&last<int.MaxValue) {
            if(last>int.MaxValue/2) {
                last=int.MaxValue;
            }else{
                last*=2;
            }

            yield return last;
        }
    }

    private static IReadOnlyList<int> GetAdaptiveElementCounts(ProgressState state,AdaptiveMethodRow row) {
        if(!IsScalableSourceTab(row.SourceTab)) {
            return state.ElementCounts;
        }

        var counts = row.Cases
            .Select(static benchmark => benchmark.CountArgument)
            .Where(static count => count>0)
            .Distinct()
            .Order()
            .ToArray();
        return counts.Length>0 ? counts : state.ElementCounts;
    }

    private static BenchmarkCase CloneWithCountArgument(BenchmarkCase benchmark,int elementCount) {
        var arguments = benchmark.Arguments
            .Select(argument => string.Equals(argument.Name,"Count",StringComparison.OrdinalIgnoreCase)
                ? new BenchmarkArgument(argument.Name,elementCount.ToString(CultureInfo.InvariantCulture))
                : argument)
            .ToArray();
        return new BenchmarkCase(
            benchmark.Project,
            benchmark.AssemblyName,
            benchmark.TypeFullName,
            benchmark.MethodName,
            BenchmarkArgumentFormatting.CreateKey(arguments),
            arguments);
    }

    private void EnsureAdaptiveElementCount(ProgressState state,int elementCount) {
        lock(this.stateGate) {
            if(state.ElementCounts.Contains(elementCount)) {
                return;
            }

            state.ElementCounts.Add(elementCount);
            state.ElementCounts=state.ElementCounts.Distinct().Order().ToList();
            stateManager.Save(state);
        }
    }

    private async Task<WarmupFailure?> RunWarmupCaseAsync(
        ProgressState state,
        BenchmarkCase benchmark,
        BenchmarkSourceTab sourceTab,
        int logicalCpu,
        BenchmarkRunRequest request,
        Action<string> statusChanged,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var warmupJob = CreateJob(benchmark,1,sourceTab,state.ElementCounts,-1);
        warmupJob.JobId=warmupJob.JobId+"|warmup";
        statusChanged($"ウォームアップ {benchmark.MethodName} "+CreateAxisStatusText(warmupJob)+$": {benchmark.Project} / CPU {logicalCpu}");
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

        return null;
    }

    private async Task RunJobAsync(
        ProgressState state,
        BenchmarkJob job,
        int logicalCpu,
        TimeSpan timeout,
        Action<BenchmarkProgressUpdate> progress,
        CancellationToken cancellationToken) {
        job.Status=JobStatus.Running;
        job.AssignedLogicalCpu=logicalCpu;
        job.StartedAt=DateTimeOffset.UtcNow;
        job.FinishedAt=null;
        job.FailureReason=string.Empty;
        stateManager.Save(state);
        progress(new BenchmarkProgressUpdate(state,job));

        try {
            Trace.WriteLine($"[Orchestrator] RunJobAsync start cpu={logicalCpu} method={job.MethodName} elements={job.ElementCount}");
            var jobStopwatch = Stopwatch.StartNew();
            var result = await jobRunner.RunAsync(job,state.RunRoot,logicalCpu,timeout,cancellationToken);
            jobStopwatch.Stop();
            Trace.WriteLine($"[Orchestrator] RunJobAsync end cpu={logicalCpu} method={job.MethodName} status={result.Status} elapsed={jobStopwatch.Elapsed.TotalMilliseconds:0.0}ms");
            var completed = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null;
            job.Status=completed
                ? JobStatus.Completed
                : JobStatus.Failed;
            job.Mean=result.Mean;
            if(result.LoopCount>0){
                job.LoopCount=result.LoopCount;
            }
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

        state.AdaptiveStates=this.CreateAdaptiveRows(state,request)
            .Select(static row => row.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key,StringComparer.OrdinalIgnoreCase)
            .Select(static key => new AdaptiveBenchmarkState { MethodName=key })
            .ToList();
        return state;
    }

    private string CreateNextRunRoot() => stateManager.CreateNextRunRoot();

    private void ReconcileStateWithRequest(ProgressState state,BenchmarkRunRequest request) {
        if(state.RunNumber<=0){
            state.RunNumber=BenchmarkStateManager.TryParseRunNumber(state.RunRoot);
        }
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

        var requestedMethods = this.CreateAdaptiveRows(state,request)
            .Select(static row => row.Key)
            .ToHashSet(StringComparer.Ordinal);
        state.AdaptiveStates=state.AdaptiveStates
            .Where(adaptiveState => requestedMethods.Contains(adaptiveState.MethodName))
            .ToList();
        foreach(var key in requestedMethods.Where(key => state.AdaptiveStates.All(item => !string.Equals(item.MethodName,key,StringComparison.Ordinal)))) {
            state.AdaptiveStates.Add(new AdaptiveBenchmarkState { MethodName=key });
        }
    }

    private static void ResetJobsFromElementCount(ProgressState state,int elementCount){
        foreach(var job in state.Jobs.Where(job => BenchmarkWorkloadAxis.GetAxisCount(job)>=elementCount)){
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

    private IReadOnlyList<BenchmarkJob> GetOrCreateAdaptiveJobs(ProgressState state,IReadOnlyList<BenchmarkCase> cases,int elementCount) {
        lock(this.stateGate) {
            var existing = state.Jobs
                .Where(job => BenchmarkWorkloadAxis.GetAxisCount(job)==elementCount&&cases.Any(item => SameCase(item,job)))
                .ToArray();
            if(existing.Length==cases.Count) {
                return existing;
            }

            foreach(var benchmark in cases) {
                if(state.Jobs.Any(job => BenchmarkWorkloadAxis.GetAxisCount(job)==elementCount&&SameCase(benchmark,job))) {
                    continue;
                }

                state.Jobs.Add(CreateJob(benchmark,elementCount,GetBenchmarkSourceTab(benchmark,state.SourceTab),state.ElementCounts,state.NextRunItemIndex++));
            }

            stateManager.Save(state);
            return state.Jobs
                .Where(job => BenchmarkWorkloadAxis.GetAxisCount(job)==elementCount&&cases.Any(item => SameCase(item,job)))
                .ToArray();
        }
    }

    private IReadOnlyList<BenchmarkJob> GetExistingAdaptiveJobs(ProgressState state,IReadOnlyList<BenchmarkCase> cases,int elementCount) {
        lock(this.stateGate) {
            return state.Jobs
                .Where(job => BenchmarkWorkloadAxis.GetAxisCount(job)==elementCount&&cases.Any(item => SameCase(item,job)))
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
            job=state.Jobs.FirstOrDefault(item => BenchmarkWorkloadAxis.GetAxisCount(item)==elementCount&&SameCase(benchmark,item))
                ??CreateJob(benchmark,elementCount,GetBenchmarkSourceTab(benchmark,state.SourceTab),state.ElementCounts,state.NextRunItemIndex++);
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
        }

        progress(new BenchmarkProgressUpdate(state,job));
    }

    private static BenchmarkJob CreateJob(
        BenchmarkCase benchmark,
        int elementCount,
        BenchmarkSourceTab sourceTab,
        IEnumerable<int> preparedElementCounts,
        int runItemIndex) =>
        new() {
            JobId=CreateJobId(benchmark,sourceTab,elementCount),
            Project=benchmark.Project,
            AssemblyName=benchmark.AssemblyName,
            TypeFullName=benchmark.TypeFullName,
            MethodName=benchmark.MethodName,
            ArgumentKey=benchmark.ArgumentKey,
            Arguments=benchmark.Arguments.ToList(),
            SourceTab=sourceTab,
            ElementCount=BenchmarkWorkloadAxis.ResolveElementCount(sourceTab,elementCount),
            LoopCount=BenchmarkWorkloadAxis.ResolveLoopCount(sourceTab,elementCount),
            PreparedElementCounts=preparedElementCounts.Distinct().Order().ToList(),
            RunItemIndex=runItemIndex,
        };

    private static string CreateJobId(BenchmarkCase benchmark,BenchmarkSourceTab sourceTab,int axisCount) =>
        string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey,BenchmarkWorkloadAxis.ResolveElementCount(sourceTab,axisCount),BenchmarkWorkloadAxis.ResolveLoopCount(sourceTab,axisCount));

    private static string CreateBenchmarkKey(BenchmarkCase benchmark) =>
        string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey);

    private void EnqueueCurrentAdaptiveRows(
        ProgressState state,
        ConcurrentQueue<AdaptiveMethodRow> queue,
        global::System.Collections.Generic.HashSet<string> queuedKeys,
        global::System.Collections.Generic.HashSet<string> completedKeys,
        BenchmarkRunRequest request) {
        foreach(var row in this.CreateAdaptiveRows(state,request)) {
            lock(queuedKeys) {
                if(queuedKeys.Contains(row.Key)||completedKeys.Contains(row.Key)) {
                    continue;
                }

                queuedKeys.Add(row.Key);
                queue.Enqueue(row);
            }
        }
    }

    private bool TryDequeueCurrentAdaptiveRow(
        ProgressState state,
        ConcurrentQueue<AdaptiveMethodRow> queue,
        global::System.Collections.Generic.HashSet<string> queuedKeys,
        BenchmarkRunRequest request,
        out AdaptiveMethodRow row) {
        while(queue.TryDequeue(out var candidate)) {
            var current = this.CreateAdaptiveRows(state,request)
                .FirstOrDefault(item => string.Equals(item.Key,candidate.Key,StringComparison.OrdinalIgnoreCase));
            if(current is not null) {
                row=current;
                return true;
            }

            lock(queuedKeys) {
                queuedKeys.Remove(candidate.Key);
            }
        }

        row=null!;
        return false;
    }

    private IReadOnlyList<AdaptiveMethodRow> CreateAdaptiveRows(ProgressState state,BenchmarkRunRequest request) {
        var jobs = this.GetJobsSnapshot(state);
        var benchmarks = (request.BenchmarkProvider?.Invoke()??request.Benchmarks)
            .Concat(jobs.Select(static job => new BenchmarkCase(job.Project,job.AssemblyName,job.TypeFullName,job.MethodName,job.ArgumentKey,job.Arguments)))
            .GroupBy(CreateBenchmarkKey,StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
        return benchmarks
            .Select((benchmark,index) => new { Benchmark=benchmark, Index=index })
            .GroupBy(item => GetBenchmarkSourceTab(item.Benchmark,request.SourceTab))
            .OrderBy(static group => group.Min(static item => item.Index))
            .SelectMany(group => {
                var sourceTab=BenchmarkSourceTabs.Get(group.Key);
                return BenchmarkRowGroupingLogic.CreateRows(group.OrderBy(static item => item.Index).Select(static item => item.Benchmark).ToArray(),sourceTab)
                    .Select(row => new AdaptiveMethodRow(
                        row.RowKey,
                        sourceTab.Tab,
                        row.MethodName,
                        sourceTab.ProjectOrder
                            .Where(project => row.CasesByProject.ContainsKey(project))
                            .Select(project => row.CasesByProject[project])
                            .ToArray()));
            })
            .ToArray();
    }

    private static BenchmarkSourceTab GetBenchmarkSourceTab(BenchmarkCase benchmark,BenchmarkSourceTab fallbackSourceTab) =>
        BenchmarkSourceTabs.TryGetByProjectAndAssembly(benchmark.Project,benchmark.AssemblyName)?.Tab??fallbackSourceTab;

    private static bool SameCase(BenchmarkCase benchmark,BenchmarkJob job) =>
        string.Equals(benchmark.Project,job.Project,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(benchmark.AssemblyName,job.AssemblyName,StringComparison.Ordinal)
        &&string.Equals(benchmark.TypeFullName,job.TypeFullName,StringComparison.Ordinal)
        &&string.Equals(benchmark.MethodName,job.MethodName,StringComparison.Ordinal)
        &&string.Equals(benchmark.ArgumentKey,job.ArgumentKey,StringComparison.Ordinal);

    private bool IsAdaptiveCaseStopped(ProgressState state,BenchmarkSourceTab sourceTab,BenchmarkCase benchmark) {
        var jobs = this.GetJobsSnapshot(state);
        return IsAdaptiveCaseFailed(jobs,sourceTab,benchmark)
            ||IsAdaptiveCaseOverThreshold(state,jobs,sourceTab,benchmark);
    }

    private static bool IsAdaptiveCaseFailed(IReadOnlyList<BenchmarkJob> jobs,BenchmarkSourceTab sourceTab,BenchmarkCase benchmark) =>
        jobs.Any(job => SameAdaptiveSeries(sourceTab,benchmark,job)&&job.Status==JobStatus.Failed);

    private static bool IsAdaptiveCaseOverThreshold(ProgressState state,IReadOnlyList<BenchmarkJob> jobs,BenchmarkSourceTab sourceTab,BenchmarkCase benchmark) =>
        jobs.Any(job =>
            SameAdaptiveSeries(sourceTab,benchmark,job)
            &&job.Status==JobStatus.Completed
            &&job.MeanNanoseconds is { } mean
            &&mean>state.AdaptiveThresholdNanoseconds);

    private static bool SameAdaptiveSeries(BenchmarkSourceTab sourceTab,BenchmarkCase benchmark,BenchmarkJob job) {
        if(!IsScalableSourceTab(sourceTab)) {
            return SameCase(benchmark,job);
        }

        return string.Equals(benchmark.Project,job.Project,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(benchmark.AssemblyName,job.AssemblyName,StringComparison.Ordinal)
            &&string.Equals(benchmark.TypeFullName,job.TypeFullName,StringComparison.Ordinal)
            &&string.Equals(benchmark.MethodName,job.MethodName,StringComparison.Ordinal);
    }

    private IReadOnlyList<BenchmarkJob> GetJobsSnapshot(ProgressState state) {
        lock(this.stateGate) {
            return state.Jobs.ToArray();
        }
    }

    private static bool IsScalableSourceTab(BenchmarkSourceTab sourceTab) =>
        BenchmarkWorkloadAxis.IsScalableSourceTab(sourceTab);

    private static string CreateAxisStatusText(BenchmarkJob job) =>
        BenchmarkWorkloadAxis.UsesLoopCount(job.SourceTab)
            ? "Loop="+job.LoopCount.ToString(CultureInfo.InvariantCulture)+" / N="+job.ElementCount.ToString(CultureInfo.InvariantCulture)
            : "N="+job.ElementCount.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<int> NormalizeCpus(IReadOnlyList<int> cpus) =>
        cpus.Count==0 ? [0] : cpus.Distinct().Order().ToArray();

    private static IReadOnlyList<int> GetCurrentCpus(BenchmarkRunRequest request) =>
        NormalizeCpus(request.LogicalCpuProvider?.Invoke()??request.LogicalCpus);

    private static bool ShouldRunWarmup(BenchmarkExecutionProfile executionProfile) =>
        executionProfile is BenchmarkExecutionProfile.Fast or BenchmarkExecutionProfile.Intermediate;

    private static bool IsCompletedMeasurement(BenchmarkJobRunResult result) =>
        string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
        &&result.MeanNanoseconds is not null;

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
        string Key,
        BenchmarkSourceTab SourceTab,
        string MethodName,
        IReadOnlyList<BenchmarkCase> Cases);

    private sealed record AdaptiveWorker(string RowKey,Task Task) {
        public bool IsCompleted => this.Task.IsCompleted;
    }

    private sealed record WarmupFailure(
        BenchmarkCase Benchmark,
        string FailureReason);
}

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkElementCounts {
#if SCALABLE_BENCHMARK
    private const int BaseElementCountExponent = 16;
    private const int FirstElementCountExponent = 16;
    private const int ElementCountExponentCount = 8;
#else
    private const int BaseElementCountExponent = 12;
#endif
    internal const int BaseElementCount = 1<<BaseElementCountExponent;

#if SCALABLE_BENCHMARK
    public static IReadOnlyList<int> All { get; } =
        Enumerable.Range(FirstElementCountExponent,ElementCountExponentCount).Select(static exponent => 1<<exponent).ToArray();

    public static IReadOnlyList<int> DefaultInitial { get; } =
        [BaseElementCount];
#else
    public static IReadOnlyList<int> All { get; } =
        [1,2,4,8,16,32,64,128];

    public static IReadOnlyList<int> DefaultInitial { get; } =
        [1];
#endif
}
