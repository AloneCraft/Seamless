using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class BenchmarkRunSession{
    private readonly Dictionary<BenchmarkRunResultKey,BenchmarkResultItem> results = [];

    public bool HasAnyResults => this.results.Count>0;

    public int Count => this.results.Count;

    public void UpsertResult(BenchmarkResultItem result){
        this.results[BenchmarkRunResultKey.From(result)]=result;
    }

    public void Clear(){
        this.results.Clear();
    }

    public void ClearSourceTab(BenchmarkSourceTab sourceTab){
        foreach(var key in this.results.Keys.Where(key => key.SourceTab==sourceTab).ToArray()){
            this.results.Remove(key);
        }
    }

    public IReadOnlyList<BenchmarkResultItem> GetResults(BenchmarkSourceTab sourceTab) =>
        this.results
            .Where(item => item.Key.SourceTab==sourceTab)
            .Select(static item => item.Value)
            .OrderBy(static item => item.StartedAt)
            .ThenBy(static item => item.AxisCount)
            .ToArray();

    public IReadOnlyList<BenchmarkResultItem> GetAllResults() =>
        this.results.Values
            .OrderBy(static item => item.SourceTab)
            .ThenBy(static item => item.StartedAt)
            .ThenBy(static item => item.AxisCount)
            .ToArray();

    public IReadOnlyList<int> GetElementCounts(BenchmarkSourceTab sourceTab) =>
        this.GetResults(sourceTab)
            .Select(static item => item.AxisCount)
            .Where(static item => item>0)
            .Distinct()
            .Order()
            .ToArray();

    public int CountCompleted() =>
        this.results.Values.Count(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase));

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
            Jobs=results.Select(result => CreateProgressJobFromResult(
                result,
                BenchmarkSourceTabs.TryGetByProject(result.Project)??selectedSourceTab,
                elementCounts,
                benchmarkResolver(result))).ToList(),
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
            PreparedElementCounts=preparedElementCounts.ToList(),
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
