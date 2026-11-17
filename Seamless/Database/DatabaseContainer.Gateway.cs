using Seamless.Collections.Tsavorite;
using Tsavorite.core;
using Seamless.Transactions;
using global::System.Linq;

namespace Seamless.Database;

public abstract partial class DatabaseContainer{
    private bool gatewayRecovered;
    private readonly global::System.Threading.AsyncLocal<int> gatewayInitializing=new();
    private Dictionary<int,ITransactionParticipant>? gatewayParticipants;
    private readonly global::System.Threading.AsyncLocal<int> gatewayReadOnly=new();
    private GatewayTransactionOwner? gatewayCandidateOwner;
    private long gatewayReplayFloor;
    private long gatewayLastAppliedLsn;
    private bool gatewayCheckpointVerified;
    // [関係代入] 表のプロパティの型は基底の Table。中身はこのDBの保存された表でなければならない。
    public void ConfigureGatewayTableChanges<TKey,TRecord>(Table<TKey,TRecord> table,Func<TRecord,TRecord>? normalize,Action<TRecord?,TRecord?> changed) where TKey:struct where TRecord:struct=>
        this.ConfigureGatewayTableChanges(table as StoredTable<TKey,TRecord>??throw new InvalidOperationException("Only a stored table of this database can be configured."),normalize,changed);
    public void ConfigureGatewayTableChanges<TKey,TRecord>(StoredTable<TKey,TRecord> table,Func<TRecord,TRecord>? normalize,Action<TRecord?,TRecord?> changed) where TKey:struct where TRecord:struct{
        if(this.Options.GatewayVersionedBinding is null){this.RequireGatewayProfile();}ArgumentNullException.ThrowIfNull(changed);lock(this.gate){if(!this.tables.TryGetValue(table.Name,out var actual)||!ReferenceEquals(actual,table)){throw new ArgumentException("The table belongs to a different database.",nameof(table));}table.GatewayNormalize=normalize;table.GatewayChanged=changed;table.GatewayKeyComparer=new CanonicalKeyComparer<TKey>(this.executionResources);}
    }
    private void ValidateOverlayWrite(bool create){
        this.EnsureAvailable();if(this.ambientTransaction.Value is{IsDisposed:true}){throw new InvalidOperationException("The captured transaction context has already ended.");}
        if(!create){return;}
        if(this.gatewayReadOnly.Value>0){throw new InvalidOperationException("The Gateway query is registered as read-only.");}
        if(this.ambientTransaction.Value is{GatewayPrepared:true}){throw new InvalidOperationException("The Gateway transaction has already been prepared.");}
    }
    public IDisposable BeginGatewayReadOnlyExecution(){if(this.Options.GatewayVersionedBinding is null){this.RequireGatewayProfile();}this.EnsureGatewayRecovered();this.gatewayReadOnly.Value++;return new GatewayReadOnlyScope(this);}
    private sealed class GatewayReadOnlyScope(DatabaseContainer owner):IDisposable{private bool disposed;public void Dispose(){if(this.disposed){return;}if(owner.gatewayReadOnly.Value<=0){throw new InvalidOperationException("The read-only execution context was lost.");}owner.gatewayReadOnly.Value--;this.disposed=true;}}
    public long GatewayCheckpointLsn{get{this.RequireGatewayProfile();this.InitializeGatewayStorage();return Math.Max(this.sharedCheckpoint.Lsn,this.gatewayReplayFloor);}}

    private GatewayDatabaseProfile RequireGatewayProfile(){
        if(this.Options.GatewayVersionedBinding is not null||this.Options.GatewayVersionedSeed is not null){throw new InvalidOperationException("Use the owning versioned Gateway domain instead of the current-value adapter.");}
        var profile=this.Options.GatewayProfile??throw new InvalidOperationException("This database is not in a Gateway profile.");
        if(profile.WalId==Guid.Empty||profile.DatabaseId<=0||this.Options.RecordSerialization!=DatabaseRecordSerialization.CanonicalV1||this.Options.StorageMode!=DatabaseStorageMode.Storage){throw new InvalidOperationException("Gateway storage requires a WAL identity, positive database ID, persistent canonical records and SharedV2 tables.");}
        return profile;
    }
    private void EnsureGatewayRecovered(){if(this.Options.GatewayVersionedBinding is not null||this.Options.GatewayVersionedSeed is not null){_=this.CurrentGatewayVersionedExecution(false);return;}if(this.Options.GatewayProfile is not null&&!this.gatewayRecovered&&this.gatewayInitializing.Value==0&&!(this.gatewayCandidateOwner is{} owner&&this.ambientTransaction.Value?.GatewayOwner==owner)){throw new InvalidOperationException("The Gateway must recover all participating databases before accepting SQL.");}}
    public void InitializeGatewayStorage(){
        this.RequireGatewayProfile();lock(this.gate){
            this.gatewayInitializing.Value++;
            try{this.EnsureSharedReady();this.gatewayParticipants??=this.sharedParticipants.Values.ToDictionary(static participant=>participant.TableId);if(!this.gatewayCheckpointVerified){if(this.sharedCheckpoint.DataSha256 is{} expected&&this.ComputeGatewayDataHash()!=expected){this.faulted=true;throw new InvalidDataException("The Gateway checkpoint data hash does not match its saved rows.");}this.gatewayCheckpointVerified=true;}}
            finally{this.gatewayInitializing.Value--;}
        }
    }
    public void CompleteGatewayRecovery()=>this.CompleteGatewayRecovery(0);
    public void CompleteGatewayRecovery(long replayFloor){this.RequireGatewayProfile();ArgumentOutOfRangeException.ThrowIfNegative(replayFloor);if(this.gatewayCandidateOwner is not null){throw new InvalidOperationException("A candidate must be sealed into a separate live dataset before admission.");}this.InitializeGatewayStorage();this.gatewayReplayFloor=Math.Max(this.gatewayReplayFloor,replayFloor);this.gatewayRecovered=true;}
    public void SetGatewayReplayFloor(long replayFloor){this.RequireGatewayProfile();ArgumentOutOfRangeException.ThrowIfNegative(replayFloor);if(this.gatewayRecovered){throw new InvalidOperationException("The replay floor must be selected before recovery.");}this.gatewayReplayFloor=Math.Max(this.gatewayReplayFloor,replayFloor);}
    public void ConfigureUnpublishedGatewayGeneration(GatewayTransactionOwner owner){
        this.RequireGatewayProfile();this.EnsureAvailable();if(owner.TransactionId==Guid.Empty){throw new ArgumentException("A candidate owner is required.",nameof(owner));}
        lock(this.gate){this.EnsureSharedStorage();if(this.gatewayRecovered||this.sharedReady||this.sharedStore!.CurrentCheckpoint is not null||this.gatewayCandidateOwner is not null){throw new InvalidOperationException("Only a fresh unpublished Gateway generation can be configured.");}this.gatewayCandidateOwner=owner;}
    }
    public void FaultGateway(){this.RequireGatewayProfile();this.faulted=true;}

    public DatabaseTransaction BeginGatewayTransaction(GatewayTransactionOwner owner,global::System.Threading.CancellationToken cancellationToken=default){
        this.RequireGatewayProfile();this.EnsureAvailable();if(this.gatewayCandidateOwner!=owner){this.EnsureGatewayRecovered();}
        if(this.gatewayCandidateOwner==owner&&!this.sharedReady){throw new InvalidOperationException("Build the unpublished snapshot before starting candidate updates.");}
        if(this.gatewayReadOnly.Value>0){throw new InvalidOperationException("A read-only Gateway operation cannot start a write transaction.");}
        if(owner.TransactionId==Guid.Empty){throw new ArgumentException("A Gateway transaction identity is required.",nameof(owner));}
        if(this.ambientReadSession.Value is not null||this.ambientTransaction.Value is not null){throw new InvalidOperationException("The Gateway database is already in a transaction or read session in this context.");}
        var lease=this.concurrencyGate.EnterWrite(cancellationToken);
        var state=new DatabaseTransactionState(this.Options.TransactionMemoryLimit,lease){GatewayOwner=owner,Depth=1};this.ambientTransaction.Value=state;return new(this,state);
    }
    private void ValidateGatewayOwner(DatabaseTransactionState state,GatewayTransactionOwner owner){
        this.ValidateTransactionContext(state);this.RequireGatewayProfile();
        if(state.GatewayOwner!=owner||state.Depth!=1||state.Aborted){throw new InvalidOperationException("The Gateway transaction owner or state does not match.");}
    }
    internal IEnumerable<WalEntry> PrepareGatewayCommit(DatabaseTransactionState state,GatewayTransactionOwner owner){
        this.ValidateGatewayOwner(state,owner);if(state.GatewayPrepared){throw new InvalidOperationException("The Gateway transaction is already prepared.");}
        state.GatewayPrepared=true;
        return Encode();
        IEnumerable<WalEntry> Encode(){
            using var changes=new SharedLegacyChanges(this,state.Overlays.Values.ToArray());
            foreach(var entry in changes.EnumerateEncoded()){yield return entry with{Partition=this.Options.GatewayProfile!.DatabaseId};}
        }
    }
    internal void ApplyGatewayCommit(DatabaseTransactionState state,GatewayTransactionOwner owner,long lsn){
        this.ValidateGatewayOwner(state,owner);if(!state.GatewayPrepared||state.GatewayApplied||lsn<=0){throw new InvalidOperationException("Only a prepared transaction with a durable common LSN can be applied.");}
        try{foreach(var overlay in state.Overlays.Values){overlay.Apply();}state.GatewayApplied=true;this.gatewayLastAppliedLsn=Math.Max(this.gatewayLastAppliedLsn,lsn);}
        catch{this.faulted=true;throw;}
    }
    internal GatewayPreparedSnapshot SealGatewaySnapshot(DatabaseTransactionState state,GatewayTransactionOwner owner){
        this.ValidateGatewayOwner(state,owner);if(this.gatewayRecovered||this.gatewayCandidateOwner!=owner||state.GatewayPrepared){throw new InvalidOperationException("Only the owning unpublished generation may be sealed without a common commit.");}
        state.GatewayPrepared=true;
        try{
            foreach(var overlay in state.Overlays.Values){overlay.Apply();}
            this.sharedCheckpoint.DataSha256=this.ComputeGatewayDataHash();
            foreach(var handle in this.sharedHandles){handle.Release();}this.sharedStore!.Save();foreach(var handle in this.sharedHandles){handle.Acquire();}
            var snapshot=new GatewayPreparedSnapshot(this.sharedStore.CurrentCheckpoint!.Value,this.GetGatewayDataHash());state.GatewayApplied=true;return snapshot;
        }catch{this.faulted=true;throw;}
    }
    public string GetGatewayDataHash(){
        var profile=this.RequireGatewayProfile();this.EnsureAvailable();this.InitializeGatewayStorage();using var lease=this.ambientTransaction.Value is null&&this.ambientReadSession.Value is null?this.concurrencyGate.EnterRead(global::System.Threading.CancellationToken.None):null;
        return this.ComputeGatewayDataHash();
    }
    private string ComputeGatewayDataHash(){
        var profile=this.RequireGatewayProfile();
        this.gatewayParticipants??=this.sharedParticipants.Values.ToDictionary(static participant=>participant.TableId);
        return ComputeGatewayDataHash(profile,this.gatewayParticipants.Values);
    }
    private static string ComputeGatewayDataHash(GatewayDatabaseProfile profile,IEnumerable<ITransactionParticipant> participants){
        using var hash=global::System.Security.Cryptography.IncrementalHash.CreateHash(global::System.Security.Cryptography.HashAlgorithmName.SHA256);hash.AppendData(profile.WalId.ToByteArray());hash.AppendData(profile.GenerationId.ToByteArray());Span<byte> id=stackalloc byte[4];global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(id,profile.DatabaseId);hash.AppendData(id);
        foreach(var participant in participants.OrderBy(static item=>item.TableId).ThenBy(static item=>item.CodecIdentity,StringComparer.Ordinal)){var snapshot=participant.GetCommittedSnapshot();global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(id,participant.TableId);hash.AppendData(id);hash.AppendData(Convert.FromHexString(snapshot.Sha256));}
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public void ReplayGatewayEntry(WalEntry entry,long lsn){
        var profile=this.RequireGatewayProfile();this.InitializeGatewayStorage();
        if(this.gatewayRecovered){throw new InvalidOperationException("Replay must finish before the Gateway starts accepting work.");}
        if(entry.Partition!=profile.DatabaseId||lsn<=0){throw new InvalidDataException("Gateway WAL database identity mismatch.");}
        if(lsn<=Math.Max(this.sharedCheckpoint.Lsn,this.gatewayReplayFloor)){return;}
        if(!this.gatewayParticipants!.TryGetValue(entry.TableId,out var participant)||participant.CodecIdentity!=entry.Codec){throw new InvalidDataException("Gateway WAL table/codec identity mismatch.");}
        try{participant.Replay(entry with{Partition=participant.Partition});this.gatewayLastAppliedLsn=Math.Max(this.gatewayLastAppliedLsn,lsn);}catch{this.faulted=true;throw;}
    }
    public void PreserveGatewayCommittedGeneration(){
        this.RequireGatewayProfile();this.EnsureAvailable();this.EnsureGatewayRecovered();var state=this.ambientTransaction.Value;
        if(this.gatewayCandidateOwner is not null||state?.GatewayOwner is null||state.Depth!=1||state.GatewayPrepared){throw new InvalidOperationException("Preserving a published generation requires its active Gateway writer.");}
        // overlay の未確定更新を含めず、直前の公開データを残す。共通 WAL は回収しない。
        try{this.sharedCheckpoint.Lsn=Math.Max(this.sharedCheckpoint.Lsn,Math.Max(this.gatewayReplayFloor,this.gatewayLastAppliedLsn));this.sharedCheckpoint.DataSha256=this.ComputeGatewayDataHash();foreach(var handle in this.sharedHandles){handle.Release();}this.sharedStore!.Save();foreach(var handle in this.sharedHandles){handle.Acquire();}}
        catch{this.faulted=true;throw;}
    }
    public void CheckpointGateway(long lsn){
        this.RequireGatewayProfile();this.EnsureAvailable();this.EnsureGatewayRecovered();
        if(lsn<this.sharedCheckpoint.Lsn){throw new ArgumentOutOfRangeException(nameof(lsn));}
        using var lease=this.concurrencyGate.EnterWrite(global::System.Threading.CancellationToken.None);
        if(this.sharedStore!.Kind!=global::Seamless.Execution.StoreKind.Persistent){return;}
        try{
            this.sharedCheckpoint.Lsn=lsn;this.sharedCheckpoint.DataSha256=this.ComputeGatewayDataHash();foreach(var handle in this.sharedHandles){handle.Release();}this.sharedStore.Save();foreach(var handle in this.sharedHandles){handle.Acquire();}
        }catch{this.faulted=true;throw;}
    }
}
