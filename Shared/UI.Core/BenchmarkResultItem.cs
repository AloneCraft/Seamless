using System.Globalization;

namespace UI;

public sealed record BenchmarkStoredResult(
    string Project,
    string Method,
    string TypeFullName,
    int ElementCount,
    int LoopCount,
    string Status,
    string Mean,
    double? MeanNanoseconds,
    long? AllocatedBytes,
    DateTimeOffset MeasuredAt,
    DateTimeOffset? SourceUpdatedAt,
    string SourcePath){
    public string ArgumentKey { get; init; } = string.Empty;
    public IReadOnlyList<BenchmarkArgument> Arguments { get; init; } = [];
    public string SourceTabId { get; init; } = string.Empty;
    /// <summary>計測を実行した論理CPU。DB(Jobs.AssignedLogicalCpu)から復元し、表の論理コア列表示に使う。</summary>
    public int? AssignedLogicalCpu { get; init; }

    public ProjectMetrics ToProjectMetrics() =>
        string.Equals(this.Status,"Completed",StringComparison.OrdinalIgnoreCase)&&this.MeanNanoseconds is not null
            ? ProjectMetrics.FromMeasurement(this.MeanNanoseconds,this.AllocatedBytes)
            : ProjectMetrics.Missing;
}

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkResultItem{
    public BenchmarkResultItem(BenchmarkJob job){
        this.Project=job.Project;
        this.Method=job.MethodName;
        this.TypeFullName=job.TypeFullName;
        this.SourceTab=job.SourceTab;
        this.ElementCount=job.ElementCount;
        this.LoopCount=job.LoopCount;
        this.Name=job.Project+" / "+job.MethodName+" / "+this.AxisCount.ToString(CultureInfo.InvariantCulture);
        this.ArgumentKey=job.ArgumentKey;
        this.Arguments=job.Arguments.ToArray();
        this.Status=job.Status.ToString();
        this.Mean=job.MeanNanoseconds is { } mean
            ? BenchmarkFormatting.FormatNanoseconds(mean)
            : job.Status==JobStatus.Failed&&string.IsNullOrWhiteSpace(job.Mean) ? "Failed" : job.Mean;
        this.AllocatedBytes=job.AllocatedBytes is { } allocated
            ? BenchmarkFormatting.FormatBytes(allocated)
            : string.Empty;
        this.AllocatedBytesValue=job.AllocatedBytes;
        this.MeanNanoseconds=job.MeanNanoseconds;
        this.ErrorNanoseconds=job.ErrorNanoseconds;
        this.StdDevNanoseconds=job.StdDevNanoseconds;
        this.RandomSeed=job.RandomSeed;
        this.ResultDigest=job.ResultDigest;
        this.ResultCheckText=job.ResultCheckText;
        this.ArtifactFolder=job.ArtifactFolder;
        this.ExitCode=job.ExitCode;
        this.AssignedLogicalCpu=job.AssignedLogicalCpu;
        this.StartedAt=job.StartedAt;
        this.FailureReason=job.FailureReason;
        this.RunItemIndex=job.RunItemIndex;
    }

    public BenchmarkResultItem(
        string project,
        string method,
        ProjectMetrics metrics,
        int elementCount = 0,
        string typeFullName = "",
        string argumentKey = "",
        IReadOnlyList<BenchmarkArgument>? arguments = null,
        BenchmarkSourceTab? sourceTab = null){
        this.Project=project;
        this.Method=method;
        this.TypeFullName=typeFullName;
        this.SourceTab=sourceTab??ResolveSourceTab(project);
        this.ElementCount=BenchmarkWorkloadAxis.ResolveElementCount(this.SourceTab,elementCount);
        this.LoopCount=BenchmarkWorkloadAxis.ResolveLoopCount(this.SourceTab,elementCount);
        this.ArgumentKey=argumentKey;
        this.Arguments=arguments??[];
        this.Name=project+" / "+method;
        this.Status=metrics.HasMeasuredValue?"Completed":"Missing";
        this.Mean=metrics.HasMeasuredValue&&metrics.TimeNanoseconds is { } time ? BenchmarkFormatting.FormatNanoseconds(time) : string.Empty;
        this.AllocatedBytes=metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } allocated ? BenchmarkFormatting.FormatBytes(allocated) : string.Empty;
        this.AllocatedBytesValue=metrics.AllocatedBytes;
        this.MeanNanoseconds=metrics.TimeNanoseconds;
        this.ErrorNanoseconds=null;
        this.StdDevNanoseconds=null;
        this.RandomSeed=null;
        this.ResultDigest=string.Empty;
        this.ResultCheckText=string.Empty;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=metrics.HasMeasuredValue?0:null;
        this.AssignedLogicalCpu=null;
        this.StartedAt=null;
        this.FailureReason=string.Empty;
    }

    public BenchmarkResultItem(BenchmarkStoredResult result)
        : this(result.Project,result.Method,result.TypeFullName,result.ToProjectMetrics(),result.ElementCount,result.LoopCount,result.Status,result.MeasuredAt,result.ArgumentKey,result.Arguments,result.SourceTabId){
        this.AssignedLogicalCpu=result.AssignedLogicalCpu;
    }

    private BenchmarkResultItem(
        string project,
        string method,
        string typeFullName,
        ProjectMetrics metrics,
        int elementCount,
        int loopCount,
        string status,
        DateTimeOffset measuredAt,
        string argumentKey = "",
        IReadOnlyList<BenchmarkArgument>? arguments = null,
        string sourceTabId = ""){
        this.Project=project;
        this.Method=method;
        this.TypeFullName=typeFullName;
        this.SourceTab=ResolveSourceTab(project,sourceTabId:sourceTabId);
        this.ElementCount=elementCount;
        this.LoopCount=loopCount>0 ? loopCount : BenchmarkWorkloadAxis.DefaultLoopCount;
        this.ArgumentKey=argumentKey;
        this.Arguments=arguments??[];
        this.Name=project+" / "+method;
        this.Status=status;
        this.Mean=metrics.HasMeasuredValue&&metrics.TimeNanoseconds is { } time ? BenchmarkFormatting.FormatNanoseconds(time) : string.Empty;
        this.AllocatedBytes=metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } allocated ? BenchmarkFormatting.FormatBytes(allocated) : string.Empty;
        this.AllocatedBytesValue=metrics.AllocatedBytes;
        this.MeanNanoseconds=metrics.TimeNanoseconds;
        this.ErrorNanoseconds=null;
        this.StdDevNanoseconds=null;
        this.RandomSeed=null;
        this.ResultDigest=string.Empty;
        this.ResultCheckText=string.Empty;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=metrics.HasMeasuredValue?0:null;
        this.AssignedLogicalCpu=null;
        this.StartedAt=measuredAt;
        this.FailureReason=string.Empty;
    }

    private BenchmarkResultItem(BenchmarkCase benchmark,Exception exception){
        this.Project=benchmark.Project;
        this.Method=benchmark.MethodName;
        this.TypeFullName=benchmark.TypeFullName;
        this.SourceTab=ResolveSourceTab(benchmark.Project,benchmark.AssemblyName);
        this.ElementCount=0;
        this.LoopCount=BenchmarkWorkloadAxis.DefaultLoopCount;
        this.ArgumentKey=benchmark.ArgumentKey;
        this.Arguments=benchmark.Arguments;
        this.Name=benchmark.Project+" / "+benchmark.MethodName;
        this.Status="Failed";
        this.Mean=exception.GetType().Name;
        this.AllocatedBytes=string.Empty;
        this.AllocatedBytesValue=null;
        this.MeanNanoseconds=null;
        this.ErrorNanoseconds=null;
        this.StdDevNanoseconds=null;
        this.RandomSeed=null;
        this.ResultDigest=string.Empty;
        this.ResultCheckText=string.Empty;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=1;
        this.AssignedLogicalCpu=null;
        this.StartedAt=null;
        this.FailureReason=exception.ToString();
    }

    public string Project { get; }
    public string Method { get; }
    public string TypeFullName { get; }
    public BenchmarkSourceTab SourceTab { get; }
    public int ElementCount { get; }
    public int LoopCount { get; }
    public int AxisCount => BenchmarkWorkloadAxis.GetAxisCount(this);
    public string ArgumentKey { get; }
    public IReadOnlyList<BenchmarkArgument> Arguments { get; }
    public string Name { get; }
    public string Status { get; }
    public string Mean { get; }
    public string AllocatedBytes { get; }
    public long? AllocatedBytesValue { get; }
    public double? MeanNanoseconds { get; }
    public double? ErrorNanoseconds { get; }
    public double? StdDevNanoseconds { get; }
    public int? RandomSeed { get; }
    public string ResultDigest { get; }
    public string ResultCheckText { get; set; }
    public string ResultSeedText => this.RandomSeed?.ToString(CultureInfo.InvariantCulture)??string.Empty;
    public string ArtifactFolder { get; }
    public int? ExitCode { get; }
    public int? AssignedLogicalCpu { get; }
    public DateTimeOffset? StartedAt { get; }
    public string FailureReason { get; }
    // 実行内で一意な生成順index。既定値(-1)は「対応する実行中ジョブがない」(履歴/比較表示由来など)を示す。
    public int RunItemIndex { get; } = -1;

    public static BenchmarkResultItem Failed(BenchmarkCase benchmark,Exception exception) => new(benchmark,exception);

    private static BenchmarkSourceTab ResolveSourceTab(string project,string assemblyName = "",string sourceTabId = ""){
        if(!string.IsNullOrWhiteSpace(sourceTabId)){
            if(BenchmarkSourceTabs.TryGetById(sourceTabId) is { } sourceTab){
                return sourceTab.Tab;
            }

            if(Enum.TryParse<BenchmarkSourceTab>(sourceTabId,true,out var tab)){
                return tab;
            }
        }

        return BenchmarkSourceTabs.TryGetByProjectAndAssembly(project,assemblyName)?.Tab??BenchmarkSourceTab.SystemArray;
    }
}
