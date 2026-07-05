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

    public void QueueSaveResult(BenchmarkResultItem result,long? runId,string rowKey,BenchmarkViewStateSnapshot viewState){
        if(runId is not { } value){
            return;
        }

        var isCompletedResult = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase);
        var isFailedResult = string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);
        if(!isCompletedResult&&!isFailedResult){
            return;
        }

        var snapshot = CreateResultSnapshot(result,rowKey,isCompletedResult,viewState);
        this.QueuePersistence(
            () => this.SaveResult(snapshot,value),
            "UI DB benchmark result save failed.");
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
        var cases = this.store.ListCases(runId).ToDictionary(static item => item.CaseId);
        var arguments = cases.Values.ToDictionary(static item => item.CaseId,item => this.store.ListCaseArguments(item.CaseId));
        var metrics = this.store.ListMetrics(runId)
            .GroupBy(static item => item.JobId)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .GroupBy(static item => item.MetricName,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(static item => item.Key,static item => item.Last(),StringComparer.OrdinalIgnoreCase));
        var items = new List<BenchmarkResultItem>();
        foreach(var job in this.store.ListJobs(runId)){
            if(!cases.TryGetValue(job.CaseId,out var testCase)){
                continue;
            }
            if(sourceTab is not null){
                if(!string.IsNullOrWhiteSpace(testCase.SourceTabId)){
                    if(!string.Equals(testCase.SourceTabId,sourceTab.Id,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                }else if(!sourceTab.ContainsProject(testCase.Project)){
                    continue;
                }
            }

            var caseArguments = arguments.TryGetValue(testCase.CaseId,out var values)
                ? values
                : new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var visibleArguments = caseArguments
                .Where(static item => !string.Equals(item.Key,"ElementCount",StringComparison.OrdinalIgnoreCase)
                    &&!string.Equals(item.Key,"LoopCount",StringComparison.OrdinalIgnoreCase)
                    &&!string.Equals(item.Key,"SourceTab",StringComparison.OrdinalIgnoreCase))
                .OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)
                .Select(static item => new BenchmarkArgument(item.Key,item.Value))
                .ToArray();
            metrics.TryGetValue(job.JobId,out var jobMetrics);
            var mean = GetNullableMetric(jobMetrics,"MeanNanoseconds");
            var allocated = GetNullableMetric(jobMetrics,"AllocatedBytes");
            items.Add(new BenchmarkResultItem(new BenchmarkStoredResult(
                testCase.Project,
                testCase.Method,
                testCase.TypeFullName,
                (int)(job.ElementCount??0),
                (int)(job.LoopCount??BenchmarkWorkloadAxis.DefaultLoopCount),
                job.Status.ToString(),
                mean?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
                mean,
                allocated is null ? null : (long)allocated.Value,
                job.FinishedAt??job.StartedAt??runUpdatedAt,
                null,
                string.Empty){
                ArgumentKey=BenchmarkArgumentFormatting.CreateKey(visibleArguments),
                Arguments=visibleArguments,
                SourceTabId=testCase.SourceTabId,
                AssignedLogicalCpu=job.AssignedLogicalCpu,
            }));
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
            result.Method,
            typeFullName,
            arguments,
            caseKey+"|N="+result.ElementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|Loop="+result.LoopCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|CPU="+(result.AssignedLogicalCpu?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-"),
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

    private void SaveResult(BenchmarkResultPersistenceSnapshot result,long runId){
        var testCase = this.store.UpsertCase(
            runId,
            result.CaseKey,
            result.RowKey,
            result.Project,
            result.Method,
            result.TypeFullName,
            result.Arguments,
            result.SourceTabId,
            result.ArgumentKey,
            result.Side,
            result.PairDiscriminator);
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

    private void SaveViewState(long runId,BenchmarkViewStateSnapshot snapshot){
        this.store.SaveUiViewState(runId,"BenchmarkProgressStateJson",snapshot.ProgressStateJson.Value);
        this.store.SaveUiViewState(runId,"SelectedSourceTab",snapshot.SelectedSourceTab);
        this.store.SaveUiViewState(runId,"SelectedResultElementCount",snapshot.SelectedResultElementCount);
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

// ProgressStateJson は Lazy にして、実際のJSONシリアライズを保存キューのバックグラウンド
// スレッドまで遅延させる(UIスレッド上のflushで全stateのシリアライズに数百msかかっていた)。
// ProgressState は構築時に全て新規DTOへ値コピーされるため、遅延評価しても内容は変わらない。
internal sealed record BenchmarkViewStateSnapshot(
    Lazy<string> ProgressStateJson,
    string SelectedSourceTab,
    string SelectedResultElementCount){
    public BenchmarkViewStateSnapshot(string progressStateJson,string selectedSourceTab,string selectedResultElementCount)
        : this(new Lazy<string>(progressStateJson),selectedSourceTab,selectedResultElementCount){
    }
}

internal sealed record BenchmarkResultPersistenceSnapshot(
    string CaseKey,
    string RowKey,
    string Project,
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
