namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private readonly ReadAuditBuffer? readAuditBuffer;
    internal Action<int>? ReadAuditPersisting{get;set;}
    internal Action<string>? AuditTransferCheckpoint{get=>this.runtime.AuditTransferCheckpoint;set=>this.runtime.AuditTransferCheckpoint=value;}
    internal bool AuditExternallyScheduled{set=>this.runtime.AuditDeliveryExternallyScheduled=value;}
    internal Task FlushAuditTransfersAsync()=>this.runtime.DrainAuditOnceAsync(synchronous:true);
    private readonly Guid readAuditSession=Guid.NewGuid();
    private readonly DateTimeOffset readAuditStarted=DateTimeOffset.UtcNow;
    internal Task FlushReadAuditAsync()=>this.readAuditBuffer?.FlushAsync()??Task.CompletedTask;
    public AuditReadBufferStatus ReadAuditStatus(UserAccount user){Require(user,"*",ManagementPermission.Manage);return this.readAuditBuffer?.Status()??new(this.readAuditSession,this.readAuditStarted,false,null,5,0,0,0,0,0,0,null,null,"NotConfigured",false,false);}
    public AuditPage AuditEvents(UserAccount user,AuditQuery query){Require(user,"*",ManagementPermission.Manage);return this.store.AuditEvents(query,value=>Can(user,value.Database,ManagementPermission.Edit));}
    public AuditReadPage ReadSummaries(UserAccount user,AuditQuery query){Require(user,"*",ManagementPermission.Manage);return this.store.ReadSummaries(query,key=>Can(user,key.Database,ManagementPermission.Edit));}
}
