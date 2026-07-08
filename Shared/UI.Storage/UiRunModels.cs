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

/// <summary>スキーマ不一致(旧バージョンDB)を検出したときの扱い。</summary>
public enum UiRunSchemaMismatchPolicy{
    /// <summary>例外を投げる。Release/通常運用の既定。無警告にDBを破棄しない。</summary>
    Throw,
    /// <summary>既存DBをバックアップリネームして新規作成する。開発用。</summary>
    BackupAndRecreate,
}

public sealed record UiCaseRecord(
    long CaseId,
    long RunId,
    string CaseKey,
    string Name,
    string Project,
    string AssemblyName,
    string Method,
    string TypeFullName,
    string SourceTabId,
    string ArgumentKey,
    string Side,
    string PairDiscriminator);

/// <summary>FLinq fallback 実行時診断の書き込み値。</summary>
public sealed record UiFallbackDiagnosticWrite(
    string Project,
    string Method,
    string CounterName,
    double CounterValue);

public sealed record UiFallbackDiagnosticRecord(
    long RunId,
    string Project,
    string Method,
    string CounterName,
    double CounterValue,
    DateTimeOffset RecordedAt);

public sealed record UiJobRecord(
    long JobId,
    long RunId,
    long CaseId,
    string JobKey,
    UiRunStatus Status,
    long? ElementCount,
    long? LoopCount,
    int? AssignedLogicalCpu,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string FailureKind,
    string FailureReason);

public sealed record UiMetricValueWrite(string MetricName,double NumericValue,string Unit);

public sealed record UiBenchmarkItemWrite(
    string TabName,
    string ProjectName,
    string AssemblyName,
    string TypeName,
    string TypeFullName,
    string BenchmarkName,
    bool IsSelected,
    long? ElementCount,
    long? LoopCount,
    int? CpuNumber,
    string ArgumentKey,
    string ArgumentDisplay,
    string RowKey,
    string Side,
    string PairDiscriminator);

public sealed record UiBenchmarkGridSideWrite(
    bool IsSelected,
    UiRunStatus Status,
    string FailureReason,
    DateTimeOffset? MeasuredAt,
    double? MeanNanoseconds,
    long? AllocatedBytes);

public sealed record UiBenchmarkSnapshotItemWrite(
    string TabName,
    string TypeName,
    string BenchmarkName,
    int? CpuNumber,
    UiBenchmarkGridSideWrite Left,
    UiBenchmarkGridSideWrite Right);

public sealed record UiBenchmarkGridSideRecord(
    bool IsSelected,
    UiRunStatus Status,
    string FailureReason,
    DateTimeOffset? MeasuredAt,
    double? MeanNanoseconds,
    long? AllocatedBytes);

public sealed record UiBenchmarkSnapshotItemRecord(
    long RunId,
    string TabName,
    string TypeName,
    string BenchmarkName,
    int? CpuNumber,
    UiBenchmarkGridSideRecord Left,
    UiBenchmarkGridSideRecord Right);

public sealed record UiRunSettingsRecord(
    long RunId,
    string RunRoot,
    string ElementCounts,
    double AdaptiveThresholdNanoseconds,
    string LogicalCpus);

public sealed record UiMetricValueRecord(
    long JobId,
    string MetricName,
    double NumericValue,
    string Unit,
    DateTimeOffset MeasuredAt);

public sealed record UiComparisonWrite(
    long CaseId,
    long? BaselineRunId,
    string MetricName,
    double? Ratio,
    double? Delta,
    string Judgement);

public sealed record UiComparisonRecord(
    long RunId,
    long CaseId,
    long? BaselineRunId,
    string MetricName,
    double? Ratio,
    double? Delta,
    string Judgement);

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
