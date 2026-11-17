namespace Seamless.Database.Gateway.Hosting;

/// <summary>接続が所有する SQL の論理取引。内側の COMMIT では保存先を確定しない。</summary>
public sealed partial class LogicalTransaction:IDisposable{
    internal LogicalTransaction(long ownerSessionId,Guid? id=null){this.OwnerSessionId=ownerSessionId;if(id is{} chosen){this.Id=chosen;}else{this.Id=Guid.NewGuid();}}
    public Guid Id{get;}
    public long OwnerSessionId{get;}
    public int Depth{get;internal set;}=1;
    public string? Name{get;internal set;}
    internal GatewayTransactionManager? Manager{get;set;}
    internal SessionContext? Session{get;set;}
    internal global::Seamless.Transactions.TransactionScope? Scope{get;set;}
    internal IDisposable? Completion{get;set;}
    internal bool Completing,Statement;
    internal Dictionary<int,GatewayParticipant> Participants{get;}=[];
    internal List<GatewayParticipant> RetiredParticipants{get;}=[];
    internal long NextVersion{get;set;}
    internal List<GatewaySavepoint> Savepoints{get;}=[];
    internal Dictionary<string,GatewayNativeQueryVersion> NativeChanges{get;}=new(StringComparer.Ordinal);
    internal List<GatewayNativeQueryVersion> NativeVersions{get;}=[];
    internal bool Doomed{get;set;}
    internal bool Closed{get;set;}
    internal global::Seamless.Execution.ResourceReservation? Memory{get;set;}
    internal bool IsRollbackOnly=>this.Doomed||this.Participants.Values.Concat(this.RetiredParticipants).Any(static participant=>participant.Transaction?.IsRollbackOnly==true);
    public void Dispose(){if(!this.Closed&&this.Manager is not null){this.Manager.Rollback(this.Session!);}}
}

internal sealed class GatewayParticipant(InProcessDatabaseAdapter adapter,IDisposable readLease){
    internal InProcessDatabaseAdapter Adapter{get;set;}=adapter;
    internal IDisposable ReadLease{get;}=readLease;
    internal IDisposable? WriteLease{get;set;}
    internal IGatewayDatabaseTransaction? Transaction{get;set;}
    internal IDisposable? SchemaLease{get;set;}
    internal long Version{get;init;}
    internal long Epoch{get;set;}
    internal DatabaseRegistration? Registration{get;set;}
    internal GatewayGenerationCandidate? Candidate{get;set;}
    internal GatewayPreparedGeneration? Prepared{get;set;}
    internal bool KeepPrepared{get;set;}
    internal bool Closed{get;set;}
    internal IDisposable? VersionedContext{get;set;}
    internal bool Written,GenerationChanged;
}
internal sealed record GatewaySavepoint(string Name,string PhysicalName,int[] Participants,int[] Writers,IReadOnlyDictionary<int,GatewayParticipant> Versions,IReadOnlyDictionary<string,GatewayNativeQueryVersion> NativeVersions);
