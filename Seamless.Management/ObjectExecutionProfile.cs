namespace Seamless.Management;

public sealed record ObjectExecutionProfileCallSite(string Path,int Start,int Length,string Caller);
public sealed record ObjectExecutionProfileTarget(string MethodKey,string SourceHash,string GeneratedBodyHash,ObjectExecutionProfileCallSite CallSite,string NodeId);
public sealed record ObjectExecutionProfileRegion(long InstanceId,ObjectExecutionProfileTarget Target,long StartTimestamp,long EndTimestamp,int ManagedThreadId,
    long InputCount,long OutputCount,bool InputCountKnown,bool OutputCountKnown,bool Complete,string Outcome,string Scope);
/// <summary>生成された物理消費区間。InputCountはその区間へ届いた値であり、元ソースの全走査件数ではない。</summary>
public sealed record ObjectExecutionProfile(Guid ExecutionId,string DefinitionId,string ObjectId,long Version,string ArtifactHash,long StopwatchFrequency,
    DateTimeOffset StartedAtUtc,long StartedTimestamp,ObjectExecutionProfileRegion[] Regions,long DroppedRegions,bool Complete,string[] Diagnostics){
    public long QueryStartedTimestamp{get;init;}
}
