using MemoryPack;

namespace Seamless.Management;

public enum QueryTraceRecordMode{TimeSampled,BoundaryEvents}
public enum QueryTraceReliability{BestEffort,Complete}
public sealed record QueryTraceOptions(QueryTraceRecordMode RecordMode=QueryTraceRecordMode.TimeSampled,QueryTraceReliability Reliability=QueryTraceReliability.BestEffort,TimeSpan? SampleInterval=null){
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan EffectiveSampleInterval=>this.SampleInterval??TimeSpan.FromMilliseconds(100);
    public void Validate(){
        if(!Enum.IsDefined(this.RecordMode)||!Enum.IsDefined(this.Reliability)||this.EffectiveSampleInterval<TimeSpan.FromMilliseconds(100)||this.EffectiveSampleInterval>TimeSpan.FromMinutes(1)){throw new ManagementException("InvalidRequest","トレースの記録方式または間隔が不正です。");}
        if(this.RecordMode==QueryTraceRecordMode.TimeSampled&&this.Reliability==QueryTraceReliability.Complete){throw new ManagementException("Unsupported","時間サンプリングの完全記録には対応していません。境界イベントを選択してください。");}
    }
}
[MemoryPackable]public sealed partial record QueryTraceFrame(Guid ExecutionId,long Sequence,string DefinitionId,string ObjectId,long? Version,string? ArtifactHash,bool IdentityResolved,
    DateTimeOffset StartedAtUtc,TimeSpan Elapsed,string Kind,string Phase,long ObservedOutputCount,string? Outcome,
    QueryTraceRecordMode RecordMode,QueryTraceReliability Reliability);
/// <param name="BackpressureTicks">同期容量待ちの合計。TimeSpan ticks（100ナノ秒）単位。</param>
public sealed record QueryTraceStatus(Guid SessionId,long Captured,long Saved,long Dropped,long SaveFailures,long PendingBytes,int ActiveCount,long BackpressureTicks,long LateSampleIntervals,string? ErrorCode,bool Closed);
public sealed record QueryTraceQuery(string DefinitionId,string ObjectId,Guid? ExecutionId=null,long AfterSequence=0,int Limit=256);
public sealed record QueryTracePage(QueryTraceFrame[] Frames,QueryTraceStatus Status,bool HasMore,int InvalidFiles,bool ScanLimited){
    public string Scope=>"QueryBoundariesAndSamples";
}
public sealed record QueryTraceWatchQuery(string DefinitionId,string ObjectId,Guid HubSession=default,long AfterRevision=0,int TimeoutMilliseconds=30000);
public sealed record QueryTraceWatchResult(Guid HubSession,long Revision,bool Changed);
