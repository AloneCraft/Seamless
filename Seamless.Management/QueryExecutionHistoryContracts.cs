using MemoryPack;

namespace Seamless.Management;

[MemoryPackable]public sealed partial record QueryExecutionObservation(Guid ExecutionId,string DefinitionId,string ObjectId,long? Version,string? ArtifactHash,bool IdentityResolved,
    DateTimeOffset StartedAtUtc,DateTimeOffset CompletedAtUtc,TimeSpan Elapsed,TimeSpan DatabaseGateWait,TimeSpan? TimeToFirstOutput,
    long ObservedOutputCount,long ReturnedOutputCount,bool IsPartial,string Outcome,string Phase,string? ErrorCode){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? GateWaitStartedOffset{get;init;}
}
[MemoryPackable]public sealed partial record QueryExecutionHistoryEntry(Guid SessionId,long Sequence,QueryExecutionObservation Observation);
public sealed record QueryExecutionHistoryQuery(string DefinitionId,string ObjectId,int Limit=100,string? Before=null);
public sealed record QueryExecutionHistoryStatus(Guid SessionId,long Accepted,long Saved,long Dropped,long SaveFailures,long PendingBytes,int PendingCount,string? ErrorCode,bool Closed){
    public long ObservationFailures{get;init;}
}
public sealed record QueryExecutionHistoryPage(QueryExecutionHistoryEntry[] Items,string? NextBefore,bool HasMore,QueryExecutionHistoryStatus Status,long MissingSequences,int InvalidFiles,bool ScanLimited,bool DirectoryMissing){
    public bool CompletenessGuaranteed=>false;
}
