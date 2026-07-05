using System.Globalization;

namespace UI;

public sealed record BenchmarkArgument(string Name,string Value);

public sealed class BenchmarkCase {
    public BenchmarkCase(
        string project,
        string assemblyName,
        string typeFullName,
        string methodName)
        : this(project,assemblyName,typeFullName,methodName,string.Empty,[]) {
    }

    public BenchmarkCase(
        string project,
        string assemblyName,
        string typeFullName,
        string methodName,
        string argumentKey,
        IReadOnlyList<BenchmarkArgument> arguments) {
        this.Project=project;
        this.AssemblyName=assemblyName;
        this.TypeFullName=typeFullName;
        this.MethodName=methodName;
        this.ArgumentKey=argumentKey;
        this.Arguments=arguments;
    }

    public string Project { get; }
    public string AssemblyName { get; }
    public string TypeFullName { get; }
    public string MethodName { get; }
    public string ArgumentKey { get; }
    public IReadOnlyList<BenchmarkArgument> Arguments { get; }
    public string DisplayName => $"{this.Project} / {this.MethodName}";
    public int CountArgument => BenchmarkArgumentFormatting.GetInt(this.Arguments,"Count");
}

public enum JobStatus {
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
}

public enum BenchmarkExecutionProfile {
    Accurate,
    Intermediate,
    Fast,
    Test,
}

internal static class BenchmarkExecutionProfileNames {
    public static string ToFolderName(BenchmarkExecutionProfile profile) =>
        profile switch{
            BenchmarkExecutionProfile.Accurate => "Accurate",
            BenchmarkExecutionProfile.Intermediate => "Intermediate",
            BenchmarkExecutionProfile.Fast => "Fast",
            BenchmarkExecutionProfile.Test => "Test",
            _ => profile.ToString(),
        };

    public static string GetAllocatedBytesSource(BenchmarkExecutionProfile profile) =>
        profile switch{
            BenchmarkExecutionProfile.Fast => "GC.GetAllocatedBytesForCurrentThread",
            BenchmarkExecutionProfile.Test => "Thread.Sleep test runner",
            BenchmarkExecutionProfile.Accurate or BenchmarkExecutionProfile.Intermediate => "BenchmarkDotNet MemoryDiagnoser",
            _ => "Unknown",
        };
}

public sealed class BenchmarkJob {
    public string JobId { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
    public string TypeFullName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string ArgumentKey { get; set; } = string.Empty;
    public List<BenchmarkArgument> Arguments { get; set; } = [];
    public BenchmarkSourceTab SourceTab { get; set; } = BenchmarkSourceTab.SystemArray;
    public int ElementCount { get; set; }
    public int LoopCount { get; set; } = BenchmarkWorkloadAxis.DefaultLoopCount;
    public List<int> PreparedElementCounts { get; set; } = [];
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int? AssignedLogicalCpu { get; set; }
    public string Mean { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string StdDev { get; set; } = string.Empty;
    public double? MeanNanoseconds { get; set; }
    public double? ErrorNanoseconds { get; set; }
    public double? StdDevNanoseconds { get; set; }
    public long? AllocatedBytes { get; set; }
    public int? RandomSeed { get; set; }
    public string ResultDigest { get; set; } = string.Empty;
    public string ResultCheckText { get; set; } = string.Empty;
    public int? ExitCode { get; set; }
    public string ArtifactFolder { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    // 実行内で一意な生成順index。実行中は文字列キー照合の代わりにこの値でUI行を更新する。
    public int RunItemIndex { get; set; } = -1;

    public BenchmarkCase ToBenchmarkCase() =>
        new(this.Project,this.AssemblyName,this.TypeFullName,this.MethodName,this.ArgumentKey,this.Arguments);

    public ReportRow ToReportRow() =>
        new(
            this.Project,
            this.MethodName,
            this.TypeFullName,
            this.MethodName,
            this.Status==JobStatus.Completed ? "Completed" : this.Status.ToString(),
            this.Mean,
            this.Error,
            this.StdDev,
            this.AllocatedBytes?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            this.MeanNanoseconds??0,
            this.ErrorNanoseconds??0,
            this.StdDevNanoseconds??0,
            this.AllocatedBytes??0,
            0,
            0,
            this.AssignedLogicalCpu?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            this.StartedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
            this.FinishedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
            this.ArtifactFolder,
            this.ExitCode,
            this.ArgumentKey,
            this.Arguments);
}

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class AdaptiveBenchmarkState {
    public string MethodName { get; set; } = string.Empty;
    public bool IsStopped { get; set; }
    public bool IsCompleted { get; set; }
    public int NextElementCountIndex { get; set; }
    public int? StoppedAtElementCount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string TriggerProject { get; set; } = string.Empty;
    public double? TriggerMeanNanoseconds { get; set; }
}

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class ProgressState {
    public int SchemaVersion { get; set; } = 1;
    public string RunId { get; set; } = string.Empty;
    public BenchmarkExecutionProfile ExecutionProfile { get; set; } = BenchmarkExecutionProfile.Accurate;
    public string RunRoot { get; set; } = string.Empty;
    public int RunNumber { get; set; }
    public BenchmarkSourceTab SourceTab { get; set; } = BenchmarkSourceTab.SystemArray;
    public List<int> ElementCounts { get; set; } = [];
    public double AdaptiveThresholdNanoseconds { get; set; }
    public List<BenchmarkJob> Jobs { get; set; } = [];
    // アダプティブ実行中に動的生成されるジョブへ一意な RunItemIndex を割り当てるための単調増加カウンタ。
    public int NextRunItemIndex { get; set; }
    public List<AdaptiveBenchmarkState> AdaptiveStates { get; set; } = [];
    public ProgressBenchmarkSelectionState BenchmarkSelection { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool HasUnfinishedJobs =>
        this.Jobs.Any(static job => job.Status is JobStatus.Pending or JobStatus.Running);
}

internal sealed record BenchmarkRunRequest(
    IReadOnlyList<BenchmarkCase> Benchmarks,
    IReadOnlyList<int> LogicalCpus,
    IReadOnlyList<int> ElementCounts,
    TimeSpan AdaptiveThreshold,
    TimeSpan JobTimeout,
    Func<IReadOnlyList<int>>? LogicalCpuProvider = null,
    bool RestartFromRequestedElementCount = false,
    BenchmarkExecutionProfile ExecutionProfile = BenchmarkExecutionProfile.Accurate,
    string? RunRootOverride = null,
    BenchmarkSourceTab SourceTab = BenchmarkSourceTab.SystemArray,
    Func<IReadOnlyList<BenchmarkCase>>? BenchmarkProvider = null);

public sealed record BenchmarkRunExecutionOptions(bool UseInfiniteTestRunner = false);

public sealed record BenchmarkJobRunResult(
    string Project,
    string Method,
    string TypeFullName,
    int ElementCount,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    double? MeanNanoseconds,
    double? ErrorNanoseconds,
    double? StdDevNanoseconds,
    long? AllocatedBytes,
    int ExitCode,
    string ArtifactFolder,
    string Log,
    int? RandomSeed = null,
    string ResultDigest = "",
    string ArgumentKey = "",
    IReadOnlyList<BenchmarkArgument>? ArgumentValues = null,
    int LoopCount = 0,
    int RunItemIndex = -1){
    public IReadOnlyList<BenchmarkArgument> Arguments { get; } = ArgumentValues??[];
}

public interface IBenchmarkJobRunner {
    Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed class ProgressBenchmarkSelectionState {
    public Dictionary<string,bool> ProjectCellSelections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,bool> RowSelections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public interface IBenchmarkDynamicCpuSetRunner {
    Task ApplyCpuSetAsync(IReadOnlyList<int> logicalCpus);
}

internal sealed record BenchmarkProgressUpdate(ProgressState State,BenchmarkJob Job);

internal sealed record BenchmarkOrchestratorResult(string RunRoot,ProgressState State);

internal static class BenchmarkArgumentFormatting {
    public static string CreateKey(IReadOnlyList<BenchmarkArgument> arguments) =>
        string.Join(",",arguments.Select(static argument => argument.Name+"="+argument.Value));

    public static string GetValue(IReadOnlyList<BenchmarkArgument> arguments,string name) =>
        arguments.FirstOrDefault(argument => string.Equals(argument.Name,name,StringComparison.OrdinalIgnoreCase))?.Value??string.Empty;

    public static int GetInt(IReadOnlyList<BenchmarkArgument> arguments,string name) =>
        int.TryParse(GetValue(arguments,name),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)
            ? value
            : 0;

    public static object?[] ToObjects(IReadOnlyList<BenchmarkArgument> arguments) =>
        arguments.Select(static argument => ParseValue(argument.Value)).ToArray();

    private static object ParseValue(string value) {
        if(long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var longValue)){
            return longValue;
        }
        if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var doubleValue)){
            return doubleValue;
        }
        return value;
    }
}
