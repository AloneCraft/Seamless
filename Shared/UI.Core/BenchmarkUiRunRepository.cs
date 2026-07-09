using Seamless.UI.Storage;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class BenchmarkUiRunRepository{
    private readonly UiRunStore store;
    private readonly object queueGate = new();
    private Task queueTail = Task.CompletedTask;
    private readonly Action<string,Exception?> log;

    public BenchmarkUiRunRepository(UiRunStore store,Action<string,Exception?>? log = null){
        this.store=store;
        this.log=log??((_,_) => {
        });
    }

    public async Task<long?> CreateRunAsync(
        UiRunAppKind appKind,
        string displayName,
        string executionProfile,
        BenchmarkSourceTab sourceTab){
        try{
            return await this.RunPersistenceAsync(() => {
                this.store.EnsureSchema();
                var run = this.store.CreateRun(new(
                    appKind,
                    displayName,
                    executionProfile,
                    sourceTab.ToString(),
                    DateTimeOffset.UtcNow));
                return (long?)run.RunId;
            });
        }
        catch(Exception ex){
            this.log("UI DB run create failed.",ex);
            return null;
        }
    }

    public async Task SetRunStatusAsync(long? runId,UiRunStatus status,string message){
        if(runId is not { } value){
            return;
        }

        try{
            await this.RunPersistenceAsync(() => {
                this.store.SetRunStatus(value,status,message);
                return true;
            });
        }
        catch(Exception ex){
            this.log("UI DB run status update failed.",ex);
        }
    }

    public void QueueSaveDetectedBenchmarkItems(
        long? runId,
        IReadOnlyList<BenchmarkCase> catalogCases,
        IReadOnlyList<BenchmarkSourceTabDefinition> sourceTabs,
        IReadOnlySet<string> selectedCaseKeys,
        IReadOnlyList<int> elementCounts){
        if(runId is not { } value){
            return;
        }

        var items = CreateDetectedBenchmarkItems(catalogCases,sourceTabs,selectedCaseKeys,elementCounts);
        this.QueuePersistence(
            () => this.store.SaveDetectedBenchmarkItems(value,items),
            "UI DB detected benchmark item save failed.");
    }

    /// <summary>
    /// Run 状態を直列保存キューへ投入して即座に戻る。終了処理が SQLite のロック等で
    /// ブロックされても UI の状態復帰を妨げないための fire-and-forget 版。
    /// QueueSaveResult と同じキューに載るため書き込み順序は保証される。
    /// </summary>
    public void QueueRunStatus(long? runId,UiRunStatus status,string message){
        if(runId is not { } value){
            return;
        }

        this.QueuePersistence(
            () => this.store.SetRunStatus(value,status,message),
            "UI DB run status update failed.");
    }

    public void QueueSaveRunSettings(long? runId,string runRoot,IReadOnlyList<int> elementCounts,TimeSpan adaptiveThreshold,IReadOnlyList<int> logicalCpus){
        if(runId is not { } value){
            return;
        }

        var thresholdNanoseconds=adaptiveThreshold.TotalMilliseconds*1_000_000;
        this.QueuePersistence(
            () => this.store.SaveRunSettings(value,runRoot,elementCounts,thresholdNanoseconds,logicalCpus),
            "UI DB benchmark run settings save failed.");
    }

    public void QueueSaveResult(BenchmarkResultItem result,long? runId,string rowKey,BenchmarkViewStateSnapshot viewState){
        if(runId is not { } value){
            return;
        }

        var isCompletedResult = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase);
        if(!isCompletedResult){
            return;
        }

        var snapshot = CreateResultSnapshot(result,rowKey,isCompletedResult,viewState);
        this.QueuePersistence(
            () => {
                this.SaveResult(snapshot,value);
                this.SaveResultDiagnostics(result,value);
            },
            "UI DB benchmark result save failed.");
    }

    public void QueueSaveBenchmarkSnapshotItems(long? runId,IReadOnlyList<BenchmarkGridSnapshotItem> items){
        if(runId is not { } value){
            return;
        }

        var writes = items.Select(static item => new UiBenchmarkSnapshotItemWrite(
            item.SourceTabId,
            item.TypeName,
            item.BenchmarkName,
            item.CpuNumber,
            item.IsSelected,
            CreateSideWrite(item.Left),
            CreateSideWrite(item.Right))).ToArray();
        this.QueuePersistence(
            () => this.store.SaveBenchmarkSnapshotItems(value,writes),
            "UI DB benchmark snapshot item save failed.");
    }

    public void QueueSaveFallbackDiagnostics(long? runId,IReadOnlyList<UiFallbackDiagnosticWrite> diagnostics){
        if(runId is not { } value||diagnostics.Count==0){
            return;
        }

        var writes = diagnostics.ToArray();
        this.QueuePersistence(
            () => this.store.SaveFallbackDiagnostics(value,writes),
            "UI DB fallback diagnostic save failed.");
    }

    public void QueueSaveViewState(long? runId,BenchmarkViewStateSnapshot snapshot){
        if(runId is not { } value){
            return;
        }

        this.QueuePersistence(
            () => this.SaveViewState(value,snapshot),
            "UI DB benchmark snapshot save failed.");
    }

    public void QueueSaveUiState(
        long? runId,
        BenchmarkViewStateSnapshot snapshot,
        IReadOnlyList<KeyValuePair<string,bool>> cellSelections,
        IReadOnlyList<KeyValuePair<string,bool>> rowSelections){
        if(runId is not { } value){
            return;
        }

        this.QueuePersistence(
            () => {
                this.SaveViewState(value,snapshot);
                foreach(var item in cellSelections){
                    this.store.SaveUiSelection(value,"BenchmarkCell",item.Key,item.Value);
                }
                foreach(var item in rowSelections){
                    this.store.SaveUiSelection(value,"BenchmarkRow",item.Key,item.Value);
                }
            },
            "UI DB state save failed.");
    }

    public IReadOnlyList<BenchmarkResultItem> LoadResultItems(UiRunAppKind appKind,BenchmarkSourceTabDefinition sourceTab) =>
        this.store.ListRuns(appKind)
            .Where(run => string.Equals(run.SourceTab,sourceTab.Tab.ToString(),StringComparison.OrdinalIgnoreCase))
            .SelectMany(run => this.LoadResultItems(run.RunId,run.UpdatedAt,sourceTab))
            .ToArray();

    public IReadOnlyList<BenchmarkResultItem> LoadResultItems(long runId,DateTimeOffset runUpdatedAt,BenchmarkSourceTabDefinition? sourceTab = null){
        var snapshots = this.LoadBenchmarkSnapshotItems(runId);
        var items = new List<BenchmarkResultItem>();
        foreach(var snapshot in snapshots){
            if(sourceTab is not null&&!string.Equals(snapshot.SourceTabId,sourceTab.Id,StringComparison.OrdinalIgnoreCase)){
                continue;
            }

            items.Add(snapshot.Left.ResultItem);
            items.Add(snapshot.Right.ResultItem);
        }

        return items;
    }

    public BenchmarkStoredResult? FindPreviousResult(
        UiRunAppKind appKind,
        BenchmarkSourceTabDefinition sourceTab,
        string method,
        string project,
        string typeFullName,
        int elementCount,
        DateTimeOffset? before,
        string argumentKey) =>
        this.LoadResultItems(appKind,sourceTab)
            .Where(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(result.TypeFullName,typeFullName)
                &&BenchmarkArgumentKeyMatchLogic.IsExactMatch(result.ArgumentKey,argumentKey)
                &&result.AxisCount==elementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null
                &&result.StartedAt is { } measuredAt
                &&(before is null||measuredAt<before.Value))
            .OrderByDescending(static result => result.StartedAt)
            .Select(static result => new BenchmarkStoredResult(
                result.Project,
                result.Method,
                result.TypeFullName,
                result.ElementCount,
                result.LoopCount,
                result.Status,
                result.Mean,
                result.MeanNanoseconds,
                result.AllocatedBytesValue,
                result.StartedAt??DateTimeOffset.MinValue,
                null,
                result.ArtifactFolder){
                ArgumentKey=result.ArgumentKey,
                Arguments=result.Arguments,
                AssignedLogicalCpu=result.AssignedLogicalCpu,
            })
            .FirstOrDefault();

    private static BenchmarkResultPersistenceSnapshot CreateResultSnapshot(
        BenchmarkResultItem result,
        string rowKey,
        bool isCompletedResult,
        BenchmarkViewStateSnapshot viewState){
        var caseKey = result.SourceTab+"|"+result.Project+"|"+rowKey+"|"+result.ArgumentKey;
        var definition = BenchmarkSourceTabs.Get(result.SourceTab);
        string side;
        if(string.Equals(result.Project,definition.LeftProject,StringComparison.OrdinalIgnoreCase)){
            side="Left";
        }else if(string.Equals(result.Project,definition.RightProject,StringComparison.OrdinalIgnoreCase)){
            side="Right";
        }else{
            side="";
        }
        var typeFullName = result.TypeFullName??string.Empty;
        // Scalable 系は同一 method+argumentKey が型違いで重複するため、型短名を discriminator にする。
        string pairDiscriminator;
        if(definition.Id.StartsWith("Scalable",StringComparison.OrdinalIgnoreCase)){
            var lastDot = typeFullName.LastIndexOf('.');
            if(lastDot>=0&&lastDot<typeFullName.Length-1){
                pairDiscriminator=typeFullName.Substring(lastDot+1);
            }else{
                pairDiscriminator=typeFullName;
            }
        }else{
            pairDiscriminator="";
        }
        var arguments = result.Arguments
            .GroupBy(static item => item.Name,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static item => item.Key,static item => item.Last().Value,StringComparer.OrdinalIgnoreCase);
        arguments["ElementCount"]=result.ElementCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        arguments["LoopCount"]=result.LoopCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        arguments["SourceTab"]=result.SourceTab.ToString();
        var jobKey = caseKey
            +"|N="+result.ElementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|Loop="+result.LoopCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|CPU="+(result.AssignedLogicalCpu is { } cpu ? cpu.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
        var metrics = new List<UiMetricValueWrite>();
        if(result.MeanNanoseconds is { } meanNanoseconds){
            metrics.Add(new("MeanNanoseconds",meanNanoseconds,"ns"));
        }
        if(result.ErrorNanoseconds is { } errorNanoseconds){
            metrics.Add(new("ErrorNanoseconds",errorNanoseconds,"ns"));
        }
        if(result.StdDevNanoseconds is { } stdDevNanoseconds){
            metrics.Add(new("StdDevNanoseconds",stdDevNanoseconds,"ns"));
        }
        if(result.AllocatedBytesValue is { } allocatedBytes){
            metrics.Add(new("AllocatedBytes",allocatedBytes,"bytes"));
        }

        return new(
            caseKey,
            rowKey,
            result.Project,
            definition.GetAssemblyName(result.Project),
            result.Method,
            typeFullName,
            arguments,
            jobKey,
            isCompletedResult ? UiRunStatus.Completed : UiRunStatus.Failed,
            result.ElementCount,
            result.LoopCount,
            result.AssignedLogicalCpu,
            result.StartedAt,
            DateTimeOffset.UtcNow,
            result.Status,
            result.FailureReason,
            metrics,
            viewState,
            definition.Id,
            result.ArgumentKey,
            side,
            pairDiscriminator);
    }

    private static IReadOnlyList<UiBenchmarkItemWrite> CreateDetectedBenchmarkItems(
        IReadOnlyList<BenchmarkCase> catalogCases,
        IReadOnlyList<BenchmarkSourceTabDefinition> sourceTabs,
        IReadOnlySet<string> selectedCaseKeys,
        IReadOnlyList<int> elementCounts){
        var axes = elementCounts.Count==0 ? [0] : elementCounts;
        var items = new List<UiBenchmarkItemWrite>();
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var sourceTab in sourceTabs){
            foreach(var axis in axes){
                var rows = BenchmarkRowGroupingLogic.CreateRows(catalogCases,sourceTab,axis);
                foreach(var row in rows){
                    foreach(var project in sourceTab.ProjectOrder){
                        if(!row.CasesByProject.TryGetValue(project,out var benchmark)){
                            continue;
                        }

                        var elementCount = BenchmarkWorkloadAxis.ResolveElementCount(sourceTab.Tab,axis);
                        var loopCount = BenchmarkWorkloadAxis.ResolveLoopCount(sourceTab.Tab,axis);
                        var itemKey = string.Join("|",sourceTab.Id,project,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey,elementCount,loopCount);
                        if(!seen.Add(itemKey)){
                            continue;
                        }

                        var side = string.Equals(project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)
                            ? "Left"
                            : string.Equals(project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase) ? "Right" : "";
                        items.Add(new(
                            sourceTab.Id,
                            project,
                            benchmark.AssemblyName,
                            ScalableBenchmarkNameFormatter.CreateGenericTypeText(benchmark.TypeFullName),
                            benchmark.TypeFullName,
                            benchmark.MethodName,
                            selectedCaseKeys.Contains(CreateSelectedCaseKey(benchmark)),
                            elementCount,
                            loopCount,
                            null,
                            benchmark.ArgumentKey,
                            CreateArgumentDisplay(benchmark.Arguments),
                            row.RowKey,
                            side,
                            CreatePairDiscriminator(sourceTab,benchmark.TypeFullName)));
                    }
                }
            }
        }

        return items;
    }

    public IReadOnlyList<BenchmarkGridSnapshotItem> LoadBenchmarkSnapshotItems(long runId){
        var records = this.store.ListBenchmarkSnapshotItems(runId);
        const int axisCount = BenchmarkElementCounts.BaseElementCount;
        var items = new List<BenchmarkGridSnapshotItem>(records.Count);
        for(var index = 0;index<records.Count;index++){
            var record = records[index];
            var sourceTab = BenchmarkSourceTabs.TryGetById(record.TabName);
            if(sourceTab is null){
                this.log("UI DB benchmark snapshot source tab is unknown: "+record.TabName,null);
                continue;
            }

            items.Add(new(
                record.RunId,
                index,
                record.TabName,
                record.TypeName,
                record.BenchmarkName,
                record.CpuNumber,
                record.IsSelected,
                CreateSnapshotSide(record,sourceTab.LeftProject,sourceTab.LeftAssemblyName,record.Left,axisCount),
                CreateSnapshotSide(record,sourceTab.RightProject,sourceTab.RightAssemblyName,record.Right,axisCount)));
        }

        return items;
    }

    private static string CreateSelectedCaseKey(BenchmarkCase benchmark) =>
        string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey);

    private static string CreateArgumentDisplay(IReadOnlyList<BenchmarkArgument> arguments) =>
        arguments.Count==0
            ? string.Empty
            : string.Join(",",arguments.OrderBy(static item => item.Name,StringComparer.OrdinalIgnoreCase).Select(static item => item.Name+"="+item.Value));

    private static string CreatePairDiscriminator(BenchmarkSourceTabDefinition sourceTab,string typeFullName){
        if(!sourceTab.Id.StartsWith("Scalable",StringComparison.OrdinalIgnoreCase)){
            return string.Empty;
        }

        var lastDot = typeFullName.LastIndexOf('.');
        return lastDot>=0&&lastDot<typeFullName.Length-1 ? typeFullName[(lastDot+1)..] : typeFullName;
    }

    private void SaveResult(BenchmarkResultPersistenceSnapshot result,long runId){
        var testCase = this.store.UpsertCase(
            runId,
            result.CaseKey,
            result.RowKey,
            result.Project,
            result.AssemblyName,
            result.Method,
            result.TypeFullName,
            result.Arguments,
            result.SourceTabId,
            result.ArgumentKey,
            result.Side,
            result.PairDiscriminator,
            result.ElementCount,
            result.LoopCount);
        var job = this.store.UpsertJob(
            runId,
            testCase.CaseId,
            result.JobKey,
            result.Status,
            result.ElementCount,
            result.LoopCount,
            result.AssignedLogicalCpu,
            result.StartedAt,
            result.FinishedAt,
            result.FailureKind,
            result.FailureReason);
        if(result.Metrics.Count>0){
            this.store.SaveMeasurement(job.JobId,result.Metrics,result.FinishedAt);
        }
        this.SaveViewState(runId,result.ViewState);
    }

    private void SaveResultDiagnostics(BenchmarkResultItem result,long runId){
        if(result.DiagnosticCounters.Count==0){
            return;
        }

        var writes = result.DiagnosticCounters.Select(counter => new UiFallbackDiagnosticWrite(
            result.Project,
            result.Method,
            counter.CounterName,
            counter.CounterValue){
            SourceTab=BenchmarkSourceTabs.Get(result.SourceTab).Id,
            ArgumentKey=result.ArgumentKey,
            ElementCount=result.ElementCount,
        }).ToArray();
        this.store.SaveFallbackDiagnostics(runId,writes,DateTimeOffset.UtcNow);
    }

    private void SaveViewState(long runId,BenchmarkViewStateSnapshot snapshot){
        this.store.SaveUiViewState(runId,"SelectedSourceTab",snapshot.SelectedSourceTab);
    }

    private static UiBenchmarkGridSideWrite CreateSideWrite(BenchmarkGridSnapshotSide side) =>
        new(
            side.Status,
            side.FailureReason,
            side.MeasuredAt,
            side.MeanNanoseconds,
            side.AllocatedBytes);

    private static BenchmarkGridSnapshotSide CreateSnapshotSide(
        UiBenchmarkSnapshotItemRecord item,
        string project,
        string assemblyName,
        UiBenchmarkGridSideRecord side,
        int axisCount){
        var resultItem = CreateResultItem(item,project,assemblyName,side,axisCount);
        return new(
            side.Status,
            side.FailureReason,
            side.MeasuredAt,
            side.MeanNanoseconds,
            side.AllocatedBytes,
            resultItem);
    }

    private static BenchmarkResultItem CreateResultItem(
        UiBenchmarkSnapshotItemRecord item,
        string project,
        string assemblyName,
        UiBenchmarkGridSideRecord side,
        int axisCount){
        var sourceTab = BenchmarkSourceTabs.TryGetById(item.TabName)?.Tab??BenchmarkSourceTab.SystemArray;
        return new BenchmarkResultItem(new BenchmarkJob{
            Project=project,
            AssemblyName=assemblyName,
            TypeFullName=item.TypeName,
            MethodName=item.BenchmarkName,
            SourceTab=sourceTab,
            ElementCount=BenchmarkWorkloadAxis.ResolveElementCount(sourceTab,axisCount),
            LoopCount=BenchmarkWorkloadAxis.ResolveLoopCount(sourceTab,axisCount),
            Status=ToJobStatus(side.Status),
            Mean=side.MeanNanoseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            MeanNanoseconds=side.MeanNanoseconds,
            AllocatedBytes=side.AllocatedBytes,
            StartedAt=side.MeasuredAt,
            FinishedAt=side.MeasuredAt,
            AssignedLogicalCpu=item.CpuNumber,
            FailureReason=side.FailureReason,
        });
    }

    private static JobStatus ToJobStatus(UiRunStatus status) =>
        status switch{
            UiRunStatus.Completed => JobStatus.Completed,
            UiRunStatus.Failed => JobStatus.Failed,
            UiRunStatus.Running => JobStatus.Running,
            UiRunStatus.Paused => JobStatus.Paused,
            _ => JobStatus.Pending,
        };

    private static IReadOnlyList<BenchmarkArgument> ParseArgumentDisplay(string value){
        if(string.IsNullOrWhiteSpace(value)){
            return [];
        }

        return value
            .Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Select(static item => {
                var separator = item.IndexOf('=');
                if(separator<=0||separator>=item.Length-1){
                    return new BenchmarkArgument(item,string.Empty);
                }

                return new BenchmarkArgument(item[..separator],item[(separator+1)..]);
            })
            .ToArray();
    }

    // 保存は単一の直列タスクチェーンで実行し、投入順序を保証する。
    // FlushAsync でチェーン末尾を待てるため、テストやシャットダウンで確定的に同期できる。
    private void QueuePersistence(Action action,string failureMessage){
        lock(this.queueGate){
            this.queueTail=this.queueTail.ContinueWith(
                _ => {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try{
                        action();
                        if(stopwatch.Elapsed.TotalMilliseconds>100){
                            System.Diagnostics.Trace.WriteLine($"[Db] Done slow ({stopwatch.Elapsed.TotalMilliseconds:0.0}ms): {failureMessage}");
                        }
                    }
                    catch(Exception ex){
                        System.Diagnostics.Trace.WriteLine($"[Db] FAILED ({stopwatch.Elapsed.TotalMilliseconds:0.0}ms): {failureMessage}: {ex}");
                        this.log(failureMessage,ex);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    /// <summary>キュー済みの保存がすべて完了するまで待つ。</summary>
    public Task FlushAsync(){
        lock(this.queueGate){
            return this.queueTail;
        }
    }

    private Task<T> RunPersistenceAsync<T>(Func<T> action){
        lock(this.queueGate){
            var task = this.queueTail.ContinueWith(
                _ => action(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            this.queueTail=task;
            return task;
        }
    }

    private static double? GetNullableMetric(IReadOnlyDictionary<string,UiMetricValueRecord>? metrics,string key) =>
        metrics is not null&&metrics.TryGetValue(key,out var value)
            ? value.NumericValue
            : null;

    private static bool IsSameType(string left,string right) =>
        string.IsNullOrWhiteSpace(left)
        ||string.IsNullOrWhiteSpace(right)
        ||string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
}

internal sealed record BenchmarkViewStateSnapshot(
    string SelectedSourceTab,
    string SelectedResultElementCount);

internal sealed record BenchmarkGridSnapshotSide(
    UiRunStatus Status,
    string FailureReason,
    DateTimeOffset? MeasuredAt,
    double? MeanNanoseconds,
    long? AllocatedBytes,
    BenchmarkResultItem ResultItem);

internal sealed record BenchmarkGridSnapshotItem(
    long RunId,
    int Order,
    string SourceTabId,
    string TypeName,
    string BenchmarkName,
    int? CpuNumber,
    bool IsSelected,
    BenchmarkGridSnapshotSide Left,
    BenchmarkGridSnapshotSide Right);

internal sealed record BenchmarkResultPersistenceSnapshot(
    string CaseKey,
    string RowKey,
    string Project,
    string AssemblyName,
    string Method,
    string TypeFullName,
    IReadOnlyDictionary<string,string> Arguments,
    string JobKey,
    UiRunStatus Status,
    long ElementCount,
    long LoopCount,
    int? AssignedLogicalCpu,
    DateTimeOffset? StartedAt,
    DateTimeOffset FinishedAt,
    string FailureKind,
    string FailureReason,
    IReadOnlyList<UiMetricValueWrite> Metrics,
    BenchmarkViewStateSnapshot ViewState,
    string SourceTabId,
    string ArgumentKey,
    string Side,
    string PairDiscriminator);
