namespace Verification.Core;

public enum CaseStatus{NotRun,Running,Passed,Failed,Cancelled,Interrupted}
public enum ComparisonStatus{Passed,Regressed,Incompatible,Incomplete,Inconclusive,CorrectnessFailure}
public sealed record ComparisonConditions(string CaseVersion,string InputHash,string Semantics,string CodecVersion,string MeasurementVersion,string Environment,string Cpu,string Configuration);
public sealed record CaseResult(string CaseId,CaseStatus Status,ComparisonConditions Conditions,double[] ElapsedNanoseconds,long? AllocatedBytes,string? ResultDigest,string? Diagnostic,long PeakPrivateBytes=0,RawMeasurement[]? Measurements=null,ArtifactReference[]? Artifacts=null,string? BuildFingerprint=null,IReadOnlyDictionary<string,double>? Metrics=null);
public sealed record RawMeasurement(string Implementation,int SampleIndex,long ElapsedNanoseconds,long OperationCount,long AllocatedBytes,int Gen0Collections=0,int Gen1Collections=0,int Gen2Collections=0);
public sealed record ArtifactReference(string RelativePath,string Sha256,long Length);
public sealed record ComparisonResult(string CaseId,ComparisonStatus Status,string Reason,double? TimeRatio=null,double? CandidateShare=null);
public sealed record VerificationCase(string Id,string Name,string Area,string Adapter,string Project,string[] PrepareArguments,string[] Arguments,ComparisonConditions Conditions,string SelectionReason,bool IsPerformance=false);
public sealed record VerificationPlan(int Version,string RepositoryRoot,string SourceFingerprint,string Profile,int BudgetSeconds,VerificationCase[] Cases);
public sealed record VerificationRun(string Id,string? ParentId,DateTimeOffset StartedAt,CaseStatus Status,VerificationPlan Plan);
public sealed record VerificationProgress(string RunId,string CaseId,CaseStatus Status,string Message);
public interface IVerificationAdapter{
    string Name{get;}
    Task PrepareAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken);
    Task<CaseResult> ExecuteAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken);
}
public interface IVerificationRepository{
    void CreateRun(VerificationRun run);
    void SaveResult(string runId,CaseResult result);
    void CompleteRun(string runId,CaseStatus status);
    VerificationRun GetRun(string runId);
    IReadOnlyList<VerificationRun> GetRuns(int limit=100);
    IReadOnlyList<CaseResult> GetResults(string runId);
    void SetBaseline(string runId);
    string? GetBaseline();
}
