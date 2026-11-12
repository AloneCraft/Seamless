using System.Text.Json;
using Seamless.Execution;
using Seamless.Transactions;

namespace Seamless.Management.Server;

internal sealed record AuditOrigin(int Format,Guid WalId,string Database,string DefinitionId,string ArtifactHash,string StoragePath,WalOptions Options,Guid? RecoveredSession=null,bool Drained=false,long? DurableLsn=null,long? AcknowledgedLsn=null,DateTimeOffset? StateUpdatedUtc=null,string? ErrorCode=null);

internal sealed partial class NativeRuntime{
    private readonly Guid auditSession=Guid.NewGuid();
    private readonly CancellationTokenSource auditStopping=new();
    private readonly SemaphoreSlim auditSignal=new(0,1);
    private readonly SemaphoreSlim auditPass=new(1,1);
    private readonly object auditWakeGate=new();
    private readonly Queue<Guid> auditWake=[];
    private readonly HashSet<Guid> auditPending=[];
    private Task auditWorker=Task.CompletedTask;
    private long auditCursor;
    private volatile bool auditPaused;
    private string? auditError;
    internal bool AuditDeliveryPaused{get=>this.auditPaused;set{this.auditPaused=value;this.SignalAudit();}}
    internal string? AuditDeliveryError=>Volatile.Read(ref this.auditError);
    internal bool AuditDeliveryExternallyScheduled{get;set;}
    internal Action<string>? AuditTransferCheckpoint{get;set;}
    internal bool AuditDeliveryRunning=>this.store.HasAudit&&(!this.auditWorker.IsCompleted||this.AuditDeliveryExternallyScheduled);
    private void StartAuditDelivery(){if(this.store.HasAudit){this.auditWorker=Task.Run(this.RunAuditDeliveryAsync);}}
    private void StopAuditDelivery(){this.AuditDeliveryExternallyScheduled=false;this.auditStopping.Cancel();this.auditWorker.GetAwaiter().GetResult();}
    private void SignalAudit(){lock(this.auditWakeGate){if(!this.auditStopping.IsCancellationRequested&&this.auditSignal.CurrentCount==0){this.auditSignal.Release();}}}
    private void NotifyAudit(Guid walId){lock(this.auditWakeGate){if(this.auditStopping.IsCancellationRequested){return;}if(this.auditPending.Count<256&&this.auditPending.Add(walId)){this.auditWake.Enqueue(walId);}if(this.auditSignal.CurrentCount==0){this.auditSignal.Release();}}}
    private async Task RunAuditDeliveryAsync(){
        var cancellation=this.auditStopping.Token;
        try{while(!cancellation.IsCancellationRequested){
            await this.auditSignal.WaitAsync(TimeSpan.FromMilliseconds(250),cancellation).ConfigureAwait(false);if(this.auditPaused){continue;}
            try{await this.DrainAuditOnceAsync(cancellation).ConfigureAwait(false);Volatile.Write(ref this.auditError,null);}
            catch(OperationCanceledException)when(cancellation.IsCancellationRequested){return;}
            catch(Exception error){Volatile.Write(ref this.auditError,AuditErrorCode(error));}
        }}catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}
    }
    internal async Task DrainAuditOnceAsync(CancellationToken cancellation=default,bool synchronous=false){
        if(!this.store.HasAudit){return;}if(synchronous){this.auditPass.Wait(cancellation);}else{await this.auditPass.WaitAsync(cancellation).ConfigureAwait(false);}
        try{
            for(var index=0;index<8;index++){Guid id;lock(this.auditWakeGate){if(!this.auditWake.TryDequeue(out id)){break;}this.auditPending.Remove(id);}var origin=this.store.Read<AuditOrigin>("auditOrigin",id.ToString("N"));if(origin is not null){await this.DeliverOriginAsync(origin,cancellation,synchronous).ConfigureAwait(false);}}
            var page=this.store.SelectPage<AuditOrigin,AuditOrigin>("auditOrigin",this.auditCursor,4,null,static value=>value,64);this.auditCursor=page.NextCursor;if(!page.HasMore){this.auditCursor=0;}
            foreach(var origin in page.Items){await this.DeliverOriginAsync(origin,cancellation,synchronous).ConfigureAwait(false);}
        }finally{this.auditPass.Release();}
    }
    private AuditOrigin? CreateAuditOrigin(PreparedDefinition prepared,DatabaseInstance instance){
        if(instance.AuditDestination is null){return null;}if(instance.AuditDestination!=this.store.InstanceId){throw new InvalidDataException("Audit destination mismatch.");}
        var journal=instance.Database.MetadataJournal??throw new InvalidDataException("The metadata journal is missing.");
        return new(1,journal.Device.WalId,prepared.Definition.Database,prepared.Definition.Id,prepared.ArtifactHash,prepared.StoragePath,journal.Options);
    }
    private void AuditOriginOpened(DefinitionVersion version,DatabaseInstance instance){
        if(instance.AuditDestination is null){return;}var journal=instance.Database.MetadataJournal!;var id=journal.Device.WalId.ToString("N");var origin=this.store.Read<AuditOrigin>("auditOrigin",id)??throw new ManagementException("RecoveryRequired","公開世代の監査転記元がありません。");
        if(origin.Format!=1||origin.Drained||origin.StoragePath!=version.StoragePath||origin.ArtifactHash!=version.ArtifactHash||origin.Database!=version.Definition.Database||origin.Options!=journal.Options){throw new ManagementException("RecoveryRequired","監査転記元と公開世代が一致しません。");}
        if(origin.RecoveredSession is not null){this.store.Write(()=>this.store.Put("auditOrigin",id,origin with{RecoveredSession=null}));}
    }
    private AuditEvent CreateUpdateAudit(Definition definition,CompiledSource query,UserAccount user,Guid transaction,DateTimeOffset started){
        var metadata=this.store.Read<QueryAuditMetadata>("queryAudit",query.Result.ArtifactHash);if(metadata is not null){QueryAuditMetadataBuilder.Validate(metadata,definition.Source.Hash(),query.Result.ArtifactHash);}
        return new(Guid.NewGuid(),null,started,DateTimeOffset.UtcNow,this.store.InstanceId,ManagementStore.Normalize(user.Name),this.store.CurrentDatabase(definition),definition.Id,AuditAction.DataChange,AuditOutcome.Succeeded,null,query.Result.ArtifactHash,transaction,"Committed",null,metadata?.Namespace,metadata?.TypeName,metadata?.MethodName,metadata?.MethodSignature,metadata?.QueryTemplate,metadata?.ModuleId,metadata?.MetadataToken);
    }
    private async Task DeliverOriginAsync(AuditOrigin origin,CancellationToken cancellation,bool synchronous=false){
        if(origin.Drained){return;}var gate=this.Gate("db:"+origin.DefinitionId);if(synchronous){gate.Wait(cancellation);}else{await gate.WaitAsync(cancellation).ConfigureAwait(false);}
        try{
            var stored=this.store.Read<AuditOrigin>("auditOrigin",origin.WalId.ToString("N"));if(stored is null||stored.Drained){return;}origin=stored;
            try{
                if(origin.Format!=1||origin.WalId==Guid.Empty||origin.Options.MetadataConsumer!="audit-v1"){throw new InvalidDataException("Invalid audit origin.");}
                var current=this.store.Read<DefinitionVersion>("active",origin.DefinitionId);var archived=current is null||current.StoragePath!=origin.StoragePath;
                var live=this.databases.TryGetValue(origin.DefinitionId,out var instance)&&instance.Path==this.Full(System.IO.Path.Combine(origin.StoragePath,"data"));
                if(live&&instance!.Compiled.Result.ArtifactHash!=origin.ArtifactHash){throw new InvalidDataException("Active audit artifact mismatch.");}
                if(!live&&!archived&&origin.RecoveredSession==this.auditSession){return;}
                TransactionJournal? detached=null;AuditDeliveryReceipt receipt;long acknowledged;
                try{
                    TransactionJournal journal;if(live){journal=instance!.Database.MetadataJournal??throw new InvalidDataException("The active metadata journal is missing.");}
                    else{detached=new(this.AuditOriginPath(origin),origin.Options);journal=detached;}
                    if(journal.Device.WalId!=origin.WalId||journal.Options!=origin.Options){throw new InvalidDataException("Audit WAL identity/profile mismatch.");}
                    var delivery=new AuditDelivery{Checkpoint=this.AuditTransferCheckpoint};
                    if(synchronous){receipt=delivery.DeliverOnce(journal,this.store,cancellation);}else{receipt=await delivery.DeliverOnceAsync(journal,this.store,cancellation).ConfigureAwait(false);}acknowledged=journal.MetadataAcknowledgedLsn;
                }finally{if(detached is not null){if(synchronous){detached.DisposeAsync().AsTask().GetAwaiter().GetResult();}else{await detached.DisposeAsync().ConfigureAwait(false);}}}
                var session=origin.RecoveredSession;if(!live&&!receipt.HasMore){session=this.auditSession;}
                var next=origin with{RecoveredSession=session,Drained=archived&&!receipt.HasMore,DurableLsn=receipt.DurableCeiling,AcknowledgedLsn=acknowledged,ErrorCode=null};
                if(next!=origin){next=next with{StateUpdatedUtc=DateTimeOffset.UtcNow};this.store.Write(()=>this.store.Put("auditOrigin",origin.WalId.ToString("N"),next));}
                if(receipt.HasMore){this.NotifyAudit(origin.WalId);}
            }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
            catch(Exception error){var code=AuditErrorCode(error);if(origin.ErrorCode!=code){var failed=origin with{ErrorCode=code,StateUpdatedUtc=DateTimeOffset.UtcNow};this.store.Write(()=>this.store.Put("auditOrigin",origin.WalId.ToString("N"),failed));}}
        }finally{gate.Release();}
    }
    private string AuditOriginPath(AuditOrigin origin){
        var parts=origin.StoragePath.Split(['\\','/'],StringSplitOptions.RemoveEmptyEntries);if(parts.Length!=2||parts[0]!="generations"||!Guid.TryParseExact(parts[1],"N",out _)){throw new InvalidDataException("Invalid audit generation path.");}
        var root=this.Full(origin.StoragePath);var wal=System.IO.Path.Combine(root,"data","wal-v2");
        var path=this.store.Root;foreach(var component in new[]{parts[0],parts[1],"data","wal-v2"}){path=System.IO.Path.Combine(path,component);if(!Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0){throw new InvalidDataException("Audit generation is missing or contains a link.");}}
        if(!Directory.EnumerateFiles(wal,"segment-*.wal").Any()){throw new InvalidDataException("Audit WAL is missing.");}
        var manifestPath=System.IO.Path.Combine(root,"manifest.json");if((File.GetAttributes(manifestPath)&FileAttributes.ReparsePoint)!=0){throw new InvalidDataException("Audit manifest contains a link.");}
        using var file=File.OpenRead(manifestPath);if(file.Length>64L<<20){throw new InvalidDataException("Audit manifest exceeds its bound.");}var manifest=JsonSerializer.Deserialize<ArtifactManifest>(file,ManagementJson.Options)??throw new InvalidDataException("Audit manifest is missing.");
        if(manifest.AuditDestination!=this.store.InstanceId||manifest.MetadataConsumer!="audit-v1"||manifest.Sha256!=origin.ArtifactHash||manifest.Definition.Id!=origin.DefinitionId||manifest.Definition.Database!=origin.Database){throw new InvalidDataException("Audit generation manifest mismatch.");}return wal;
    }
    private static string AuditErrorCode(Exception error){if(error is ResourceLimitException){return "Capacity";}if(error is InvalidDataException){return "Integrity";}if(error is ManagementException management){return management.Code;}return "ProjectionFailed";}
    internal AuditDeliveryPage AuditDeliveryStatus(long cursor){
        var page=this.store.SelectPage<AuditOrigin,AuditDeliveryState>("auditOrigin",cursor,100,null,origin=>{var active=this.store.Read<DefinitionVersion>("active",origin.DefinitionId);return new(origin.WalId,origin.Database,origin.DefinitionId,origin.ArtifactHash,active is null||active.StoragePath!=origin.StoragePath,origin.Drained,origin.DurableLsn,origin.AcknowledgedLsn,origin.StateUpdatedUtc,origin.ErrorCode);},4096);
        return new(page.Items,page.NextCursor,page.HasMore,this.AuditDeliveryRunning,this.AuditDeliveryError);
    }
}

public sealed partial class ManagementService{
    internal bool AuditDeliveryPaused{get=>this.runtime.AuditDeliveryPaused;set=>this.runtime.AuditDeliveryPaused=value;}
    internal Task DrainAuditOnceAsync()=>this.runtime.DrainAuditOnceAsync();
    public AuditDeliveryPage AuditDeliveryStatus(UserAccount user,long cursor=0){Require(user,"*",ManagementPermission.Manage);if(!this.store.HasAudit){throw new ManagementException("MigrationRequired","監査テーブルを明示導入してください。");}return this.runtime.AuditDeliveryStatus(cursor);}
}
