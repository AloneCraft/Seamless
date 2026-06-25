using System.Globalization;

namespace Benchmark.Worker;

public sealed record BenchmarkCase(
    string Project,
    string AssemblyName,
    string TypeFullName,
    string MethodName) {
    public string DisplayName => $"{this.Project} / {this.MethodName}";
}

public enum JobStatus {
    Pending,
    Running,
    Completed,
    Failed,
}

public enum BenchmarkRunMode {
    Fixed,
    Adaptive,
}

public sealed class BenchmarkJob {
    public string JobId { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
    public string TypeFullName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public int ElementCount { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int? AssignedLogicalCpu { get; set; }
    public string Mean { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string StdDev { get; set; } = string.Empty;
    public double? MeanNanoseconds { get; set; }
    public double? ErrorNanoseconds { get; set; }
    public double? StdDevNanoseconds { get; set; }
    public long? AllocatedBytes { get; set; }
    public int? ExitCode { get; set; }
    public string ArtifactFolder { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public BenchmarkCase ToBenchmarkCase() =>
        new(this.Project,this.AssemblyName,this.TypeFullName,this.MethodName);

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
            this.ExitCode);
}

public sealed class AdaptiveBenchmarkState {
    public string MethodName { get; set; } = string.Empty;
    public bool IsStopped { get; set; }
    public bool IsCompleted { get; set; }
    public int NextElementCountIndex { get; set; }
    public int? StoppedAtElementCount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string TriggerProject { get; set; } = string.Empty;
    public double? TriggerMeanNanoseconds { get; set; }
}

public sealed class ProgressState {
    public int SchemaVersion { get; set; } = 1;
    public string RunId { get; set; } = string.Empty;
    public BenchmarkRunMode Mode { get; set; }
    public string RunRoot { get; set; } = string.Empty;
    public List<int> ElementCounts { get; set; } = [];
    public double AdaptiveThresholdNanoseconds { get; set; }
    public List<BenchmarkJob> Jobs { get; set; } = [];
    public List<AdaptiveBenchmarkState> AdaptiveStates { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool HasUnfinishedJobs =>
        this.Jobs.Any(static job => job.Status is JobStatus.Pending or JobStatus.Running);
}

public sealed record BenchmarkRunRequest(
    IReadOnlyList<BenchmarkCase> Benchmarks,
    IReadOnlyList<int> LogicalCpus,
    IReadOnlyList<int> ElementCounts,
    BenchmarkRunMode Mode,
    TimeSpan AdaptiveThreshold,
    TimeSpan JobTimeout);

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
    string Log);

public interface IBenchmarkJobRunner {
    Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed record BenchmarkProgressUpdate(ProgressState State,BenchmarkJob Job);

public sealed record BenchmarkOrchestratorResult(string RunRoot,IReadOnlyList<string> CsvPaths);
