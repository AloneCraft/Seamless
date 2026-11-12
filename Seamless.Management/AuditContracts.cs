using MemoryPack;

namespace Seamless.Management;

public enum AuditAction{Read,DataChange,SchemaChange,PermissionChange,AccountChange,SourceRead,Validation,Other}
public enum AuditOutcome{Succeeded,Failed,Cancelled,Rejected,Unknown}
[MemoryPackable]public readonly partial record struct AuditEvent(Guid EventId,Guid? OperationId,DateTimeOffset StartedAt,DateTimeOffset CompletedAt,Guid AuthorityId,string SubjectId,string Database,string? ObjectId,AuditAction Action,AuditOutcome Outcome,long? PolicyVersion,string? CodeVersion,Guid? TransactionId,string ResultCode,string? Message=null,string? Namespace=null,string? TypeName=null,string? MethodName=null,string? MethodSignature=null,string? QueryTemplate=null,Guid? MethodModuleId=null,int? MethodMetadataToken=null);
[MemoryPackable]public readonly partial record struct AuditReadKey(DateTimeOffset WindowStart,DateTimeOffset WindowEnd,Guid AuthorityId,string SubjectId,string Database,string QueryId,string? CodeVersion,long? PolicyVersion);
[MemoryPackable]public readonly partial record struct AuditReadDelta(AuditReadKey Key,long Count);
public sealed record AuditQuery(DateTimeOffset? FromUtc=null,DateTimeOffset? ToUtc=null,string? Database=null,string? SubjectId=null,AuditAction? Action=null,AuditOutcome? Outcome=null,int Limit=100,long Before=0,long Ceiling=0,string? Text=null){
    public string? ObjectId{get;init;}
}
public sealed record AuditPage(AuditEvent[] Items,long NextBefore,long Ceiling,bool HasMore,int Examined);
public sealed record AuditQueryInfo(string? Namespace,string? TypeName,string? MethodName,string? MethodSignature,Guid ModuleId,int MetadataToken,string? QueryTemplate){
    public string? QualifiedName{get{if(this.Namespace is null||string.IsNullOrEmpty(this.TypeName)||string.IsNullOrEmpty(this.MethodName)){return null;}return string.Join('.',new[]{this.Namespace,this.TypeName,this.MethodName}.Where(static value=>value.Length>0));}}
}
public sealed record AuditReadEntry(long Sequence,AuditReadKey Key,long Count,DateTimeOffset SavedAt){public AuditQueryInfo? Method{get;init;}}
public sealed record AuditReadPage(AuditReadEntry[] Items,long NextBefore,long Ceiling,bool HasMore,int Examined);
public sealed record AuditDeliveryState(Guid WalId,string Database,string DefinitionId,string ArtifactHash,bool Archived,bool Drained,long? DurableLsn,long? AcknowledgedLsn,DateTimeOffset? StateUpdatedUtc,string? ErrorCode);
public sealed record AuditDeliveryPage(AuditDeliveryState[] Items,long NextCursor,bool HasMore,bool Running,string? ErrorCode);
public sealed record AuditReadBufferStatus(Guid SessionId,DateTimeOffset StartedAt,bool Enabled,double? WindowSeconds,double SaveIntervalSeconds,int PendingKeys,long PendingBytes,long AcceptedCount,long SavedCount,long DroppedCount,long SaveFailures,DateTimeOffset? LastSavedAt,DateTimeOffset? LastFailureAt,string? ErrorCode,bool Closed,bool CountersSaturated,long UnconfirmedCount=0);
