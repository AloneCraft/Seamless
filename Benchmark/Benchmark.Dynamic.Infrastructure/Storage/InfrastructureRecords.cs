namespace Benchmark.Dynamic.Infrastructure;

public static class DynamicInfrastructureSchema{
    public const int CurrentVersion=2;
    public const int UserTableCount=11;
    public const string DatabaseFileName="Benchmark.Dynamic.db";
}

public sealed record DynamicRunRecord(
    string RunID,
    string Status,
    long Seed,
    string Profile,
    string SettingsJSON,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string AppVersion="",
    string WorkerVersion="",
    DateTimeOffset? StartedAt=null,
    DateTimeOffset? CompletedAt=null,
    DateTimeOffset? CancelledAt=null,
    string? ReplayedFromRunID=null,
    DateTimeOffset? DeletedAt=null,
    string DeletedReason="");

public sealed record DynamicRunSourceRecord(
    string RunID,
    string SourceKey,
    bool Selected,
    int DisplayOrder);

public sealed record DynamicQueryRecord(
    string QueryID,
    string RunID,
    int Ordinal,
    string PathKey,
    long GenerationSeed,
    string CanonicalIRJSON,
    string SourceText,
    string QueryHash,
    string Status,
    string? ReplayedFromQueryID=null,
    int IrSchemaVersion=1,
    int OperatorCount=0,
    int Depth=0,
    string ResultType="",
    string OrderSemantics="",
    long IrGenerationNanoseconds=0);

public sealed record DynamicSourceExecutionRecord(
    string ExecutionID,
    string QueryID,
    string SourceKey,
    int Attempt,
    int ExecutionOrder,
    string Status,
    int LoopCount=0,
    int WarmupCount=0,
    DateTimeOffset? StartedAt=null,
    DateTimeOffset? CompletedAt=null,
    string ResultDigest="",
    string Error="",
    bool Partial=false,
    string CapabilityProfileID="",
    string CapabilityProfileJSON="null",
    string GeneratedSource="",
    string SourceHash="",
    long SourceEmissionNanoseconds=0,
    long SourceTextNanoseconds=0,
    long SyntaxTreeNanoseconds=0,
    long CompilationNanoseconds=0,
    long GeneratorNanoseconds=0,
    long EmitNanoseconds=0,
    long LoadNanoseconds=0,
    long BindNanoseconds=0,
    string RewriteSummaryJSON="null",
    string FixedPointTermination="",
    bool? AssemblyUnloaded=null);

public sealed record DynamicValidationRecord(
    string ValidationID,
    string ExecutionID,
    string ImplementationKind,
    string Status,
    DateTimeOffset ValidatedAt,
    string ResultSummary="",
    string FullResult="null",
    string ExceptionJSON="null",
    string SymbolValidationJSON="null");

public sealed record DynamicMeasurementRecord(
    string MeasurementID,
    string ExecutionID,
    string ImplementationKind,
    long OperationCount,
    int SampleCount,
    double? MedianNanoseconds=null,
    double? MeanNanoseconds=null,
    double? MinimumNanoseconds=null,
    double? MaximumNanoseconds=null,
    double? StandardDeviationNanoseconds=null,
    double? StandardErrorNanoseconds=null,
    double? AllocatedBytesPerOperation=null,
    double? Gen0Collections=null,
    double? Gen1Collections=null,
    double? Gen2Collections=null,
    DateTimeOffset? StartedAt=null,
    DateTimeOffset? CompletedAt=null);

public sealed record DynamicRawSampleRecord(
    string MeasurementID,
    string Phase,
    int SampleIndex,
    double ElapsedNanoseconds,
    long OperationCount,
    long AllocatedBytes,
    long Gen0Collections,
    long Gen1Collections,
    long Gen2Collections,
    DateTimeOffset RecordedAt);

public sealed record DynamicDiagnosticRecord(
    string DiagnosticID,
    string ExecutionID,
    string Name,
    string DetailsJSON,
    DateTimeOffset RecordedAt,
    double? NumericValue=null,
    string? TextValue=null,
    string Unit="");

public sealed record DynamicOptimizationEventRecord(
    string OptimizationEventID,
    string QueryID,
    string SourceKey,
    int Sequence,
    string Kind,
    string DetailsJSON,
    DateTimeOffset RecordedAt,
    string ExecutionID="",
    string Layer="",
    string Phase="",
    string RuleID="",
    int Round=0,
    int Pass=0,
    int Visit=0,
    string NodeID="",
    string NodePath="",
    int? Depth=null,
    int? StageIndex=null,
    string ParentKind="",
    bool IsLeft=false,
    bool IsRight=false,
    bool IsInner=false,
    string Before="",
    string After="",
    bool Changed=false,
    bool Revisited=false);

public sealed record DynamicRunEventRecord(
    string RunEventID,
    string RunID,
    int Sequence,
    string Level,
    string Kind,
    string Message,
    string DetailsJSON,
    DateTimeOffset RecordedAt,
    string? QueryID=null,
    string? ExecutionID=null);

public sealed record DynamicMeasurementBundle(
    DynamicMeasurementRecord Measurement,
    IReadOnlyList<DynamicRawSampleRecord> RawSamples);

public sealed record DynamicRunCheckpoint(
    DynamicRunRecord Run,
    IReadOnlyList<DynamicRunSourceRecord>? Sources=null,
    IReadOnlyList<DynamicQueryRecord>? Queries=null,
    IReadOnlyList<DynamicSourceExecutionRecord>? SourceExecutions=null,
    IReadOnlyList<DynamicValidationRecord>? Validations=null,
    IReadOnlyList<DynamicMeasurementBundle>? Measurements=null,
    IReadOnlyList<DynamicDiagnosticRecord>? Diagnostics=null,
    IReadOnlyList<DynamicOptimizationEventRecord>? OptimizationEvents=null,
    IReadOnlyList<DynamicRunEventRecord>? RunEvents=null);

public sealed record DynamicRunSnapshot(
    DynamicRunRecord Run,
    IReadOnlyList<DynamicRunSourceRecord> Sources,
    IReadOnlyList<DynamicQueryRecord> Queries,
    IReadOnlyList<DynamicSourceExecutionRecord> SourceExecutions,
    IReadOnlyList<DynamicValidationRecord> Validations,
    IReadOnlyList<DynamicMeasurementRecord> Measurements,
    IReadOnlyList<DynamicRawSampleRecord> RawSamples,
    IReadOnlyList<DynamicDiagnosticRecord> Diagnostics,
    IReadOnlyList<DynamicOptimizationEventRecord> OptimizationEvents,
    IReadOnlyList<DynamicRunEventRecord> RunEvents);
