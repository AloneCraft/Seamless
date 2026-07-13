using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class BenchmarkRunSession{
    private readonly object gate = new();
    private readonly Dictionary<BenchmarkRunResultKey,BenchmarkResultItem> results = [];

    public bool HasAnyResults {
        get {
            lock(this.gate) {
                return this.results.Count>0;
            }
        }
    }

    public int Count {
        get {
            lock(this.gate) {
                return this.results.Count;
            }
        }
    }

    /// <summary>件数チェック専用。GetResults はソート+配列化を伴うため、存在確認に使わないこと。</summary>
    public bool HasResults(BenchmarkSourceTab sourceTab){
        lock(this.gate) {
            foreach(var key in this.results.Keys){
                if(key.SourceTab==sourceTab){
                    return true;
                }
            }
        }

        return false;
    }

    public void UpsertResult(BenchmarkResultItem result){
        lock(this.gate) {
            this.results[BenchmarkRunResultKey.From(result)]=result;
        }
    }

    public void Clear(){
        lock(this.gate) {
            this.results.Clear();
        }
    }

    public void ClearSourceTab(BenchmarkSourceTab sourceTab){
        lock(this.gate) {
            foreach(var key in this.results.Keys.Where(key => key.SourceTab==sourceTab).ToArray()){
                this.results.Remove(key);
            }
        }
    }

    public void ClearRunningResults(){
        lock(this.gate) {
            foreach(var key in this.results
                .Where(static item => string.Equals(item.Value.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase))
                .Select(static item => item.Key)
                .ToArray()){
                this.results.Remove(key);
            }
        }
    }

    public IReadOnlyList<BenchmarkResultItem> GetResults(BenchmarkSourceTab sourceTab){
        lock(this.gate) {
            return this.results
                .Where(item => item.Key.SourceTab==sourceTab)
                .Select(static item => item.Value)
                .OrderBy(static item => item.StartedAt)
                .ThenBy(static item => item.AxisCount)
                .ToArray();
        }
    }

    public IReadOnlyList<BenchmarkResultItem> GetAllResults(){
        lock(this.gate) {
            return this.results.Values
                .OrderBy(static item => item.SourceTab)
                .ThenBy(static item => item.StartedAt)
                .ThenBy(static item => item.AxisCount)
                .ToArray();
        }
    }

    public IReadOnlyList<int> GetElementCounts(BenchmarkSourceTab sourceTab) =>
        this.GetResults(sourceTab)
            .Select(static item => item.AxisCount)
            .Where(static item => item>0)
            .Distinct()
            .Order()
            .ToArray();

    public int CountCompleted(){
        lock(this.gate) {
            return this.results.Values.Count(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase));
        }
    }

    public ProgressState ToProgressState(
        string runRoot,
        BenchmarkExecutionProfile executionProfile,
        BenchmarkSourceTabDefinition selectedSourceTab,
        TimeSpan adaptiveThreshold,
        IReadOnlyDictionary<string,bool> projectCellSelections,
        IReadOnlyDictionary<string,bool> rowSelections,
        Func<BenchmarkResultItem,BenchmarkCase?> benchmarkResolver){
        var now = DateTimeOffset.UtcNow;
        var sourceResults = this.HasAnyResults
            ? this.GetAllResults()
            : [];
        return CreateProgressState(
            runRoot,
            executionProfile,
            selectedSourceTab,
            adaptiveThreshold,
            projectCellSelections,
            rowSelections,
            sourceResults,
            benchmarkResolver,
            now);
    }

    public static ProgressState CreateProgressState(
        string runRoot,
        BenchmarkExecutionProfile executionProfile,
        BenchmarkSourceTabDefinition selectedSourceTab,
        TimeSpan adaptiveThreshold,
        IReadOnlyDictionary<string,bool> projectCellSelections,
        IReadOnlyDictionary<string,bool> rowSelections,
        IEnumerable<BenchmarkResultItem> sourceResults,
        Func<BenchmarkResultItem,BenchmarkCase?> benchmarkResolver,
        DateTimeOffset? timestamp = null){
        var now = timestamp??DateTimeOffset.UtcNow;
        var results = sourceResults
            .Where(static result => result.AxisCount>0)
            .GroupBy(static result => CreateResultIdentityKey(result.Project,result.TypeFullName,result.Method,result.AxisCount,result.ArgumentKey),StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .ToArray();
        var elementCounts = results
            .Select(static result => result.AxisCount)
            .Where(static item => item>0)
            .Distinct()
            .Order()
            .ToArray();
        var state = new ProgressState{
            RunId=Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            ExecutionProfile=executionProfile,
            RunRoot=runRoot,
            RunNumber=BenchmarkStateManager.TryParseRunNumber(runRoot),
            SourceTab=selectedSourceTab.Tab,
            ElementCounts=elementCounts.ToList(),
            AdaptiveThresholdNanoseconds=adaptiveThreshold.TotalMilliseconds*1_000_000,
            Jobs=results.Select(result => CreateProgressJobFromResolvedResult(
                    result,
                    selectedSourceTab,
                    elementCounts,
                    benchmarkResolver))
                .Where(static job => !string.IsNullOrWhiteSpace(job.TypeFullName))
                .ToList(),
            BenchmarkSelection=new ProgressBenchmarkSelectionState{
                ProjectCellSelections=new Dictionary<string,bool>(projectCellSelections,StringComparer.OrdinalIgnoreCase),
                RowSelections=new Dictionary<string,bool>(rowSelections,StringComparer.OrdinalIgnoreCase),
            },
            CreatedAt=now,
            UpdatedAt=now,
        };
        state.AdaptiveStates=state.Jobs
            .Select(static job => BenchmarkRowGroupingLogic.CreateCaseKey(job.Project,job.TypeFullName,job.MethodName,job.ArgumentKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key,StringComparer.OrdinalIgnoreCase)
            .Select(static key => new AdaptiveBenchmarkState{
                MethodName=key,
                IsCompleted=true,
            })
            .ToList();
        return state;
    }

    private static BenchmarkJob CreateProgressJobFromResolvedResult(
        BenchmarkResultItem result,
        BenchmarkSourceTabDefinition selectedSourceTab,
        IReadOnlyList<int> preparedElementCounts,
        Func<BenchmarkResultItem,BenchmarkCase?> benchmarkResolver){
        var benchmark = benchmarkResolver(result)
            ??throw new InvalidDataException(
                "Benchmark result could not be matched to a catalog case. Project="+result.Project
                +" Method="+result.Method
                +" Type="+result.TypeFullName
                +" ArgumentKey="+result.ArgumentKey);
        var sourceTab=BenchmarkSourceTabs.GetByProjectAndAssembly(result.Project,benchmark.AssemblyName);
        if(!sourceTab.ContainsCase(benchmark)){
            throw new InvalidDataException(
                "Benchmark case does not belong to the resolved SourceTab. SourceTab="+sourceTab.Id
                +" Project="+benchmark.Project
                +" Assembly="+benchmark.AssemblyName
                +" Type="+benchmark.TypeFullName);
        }
        if(sourceTab.Tab!=selectedSourceTab.Tab||result.SourceTab!=sourceTab.Tab){
            throw new InvalidDataException(
                "Benchmark result SourceTab is inconsistent. Project="+result.Project
                +" Assembly="+benchmark.AssemblyName
                +" SelectedSourceTab="+selectedSourceTab.Id
                +" ResultSourceTab="+BenchmarkSourceTabs.Get(result.SourceTab).Id
                +" ResolvedSourceTab="+sourceTab.Id);
        }
        return CreateProgressJobFromResult(result,sourceTab,preparedElementCounts,benchmark);
    }

    private static BenchmarkJob CreateProgressJobFromResult(
        BenchmarkResultItem result,
        BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyList<int> preparedElementCounts,
        BenchmarkCase? benchmark){
        var argumentKey = FirstText(result.ArgumentKey,benchmark?.ArgumentKey??string.Empty);
        var arguments = result.Arguments.Count>0
            ? result.Arguments.ToList()
            : benchmark?.Arguments.ToList()??[];
        var status = ParseJobStatus(result.Status);
        var finishedAt = result.StartedAt??DateTimeOffset.UtcNow;
        return new BenchmarkJob{
            JobId=string.Join("|",result.Project,benchmark?.AssemblyName??string.Empty,benchmark?.TypeFullName??string.Empty,result.Method,argumentKey,result.AxisCount),
            Project=result.Project,
            AssemblyName=benchmark?.AssemblyName??string.Empty,
            TypeFullName=benchmark?.TypeFullName??string.Empty,
            MethodName=result.Method,
            ArgumentKey=argumentKey,
            Arguments=arguments,
            SourceTab=sourceTab.Tab,
            ElementCount=result.ElementCount,
            LoopCount=result.LoopCount,
            PreparedElementCounts=BenchmarkWorkloadAxis.ResolvePreparedElementCounts(sourceTab.Tab,preparedElementCounts).ToList(),
            Status=status,
            AssignedLogicalCpu=result.AssignedLogicalCpu,
            Mean=result.Mean,
            Error=result.ErrorNanoseconds?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            StdDev=result.StdDevNanoseconds?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            MeanNanoseconds=result.MeanNanoseconds,
            ErrorNanoseconds=result.ErrorNanoseconds,
            StdDevNanoseconds=result.StdDevNanoseconds,
            AllocatedBytes=result.AllocatedBytesValue,
            RandomSeed=result.RandomSeed,
            ResultDigest=result.ResultDigest,
            ResultCheckText=result.ResultCheckText,
            ExitCode=result.ExitCode,
            ArtifactFolder=result.ArtifactFolder,
            FailureReason=result.FailureReason,
            StartedAt=result.StartedAt??finishedAt,
            FinishedAt=status is JobStatus.Completed or JobStatus.Failed ? finishedAt : null,
        };
    }

    private static JobStatus ParseJobStatus(string status) =>
        Enum.TryParse<JobStatus>(status,true,out var value)
            ? value
            : JobStatus.Pending;

    private static string FirstText(string left,string right) =>
        !string.IsNullOrWhiteSpace(left) ? left : right;

    private static string CreateResultIdentityKey(string project,string typeFullName,string method,int elementCount,string argumentKey) =>
        string.Join("|",project,typeFullName,method,elementCount.ToString(CultureInfo.InvariantCulture),argumentKey);
}

internal readonly record struct BenchmarkRunResultKey(
    BenchmarkSourceTab SourceTab,
    string Project,
    string TypeFullName,
    string Method,
    string ArgumentKey,
    int ElementCount){
    public static BenchmarkRunResultKey From(BenchmarkResultItem result) =>
        new(
            result.SourceTab,
            result.Project,
            result.TypeFullName,
            result.Method,
            result.ArgumentKey,
            result.AxisCount);
}
