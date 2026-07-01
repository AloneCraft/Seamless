namespace Seamless.UI.Storage;

public enum UiRunAppKind{
    Benchmark,
    ScalableBenchmark,
    ParameterSweep,
}

public enum UiRunStatus{
    Queued,
    Running,
    Paused,
    Stopped,
    Completed,
    Failed,
}

public sealed record UiRunCreateRequest(
    UiRunAppKind AppKind,
    string DisplayName,
    string ExecutionProfile,
    string SourceTab,
    DateTimeOffset CreatedAt);

public sealed record UiRunRecord(
    long RunId,
    UiRunAppKind AppKind,
    string DisplayName,
    UiRunStatus Status,
    string ExecutionProfile,
    string SourceTab,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UiCaseRecord(
    long CaseId,
    long RunId,
    string CaseKey,
    string Name,
    string Project,
    string Method,
    string TypeFullName);

public sealed record UiJobRecord(
    long JobId,
    long RunId,
    long CaseId,
    string JobKey,
    UiRunStatus Status,
    long? ElementCount,
    int? AssignedLogicalCpu,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string FailureKind,
    string FailureReason);

public sealed record UiMetricValueWrite(string MetricName,double NumericValue,string Unit);

public sealed record UiMetricValueRecord(
    long JobId,
    string MetricName,
    double NumericValue,
    string Unit,
    DateTimeOffset MeasuredAt);

public sealed record UiRunStateRecord(
    long RunStateId,
    long RunId,
    UiRunStatus Status,
    string Message,
    DateTimeOffset ChangedAt);

public sealed record UiSelectionRecord(
    long RunId,
    string Scope,
    string SelectionKey,
    bool IsSelected);

public sealed record UiViewStateRecord(
    long RunId,
    string StateKey,
    string StateValue);
