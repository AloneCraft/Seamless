using System.Text.Json;
using System.Security.Cryptography;
using Seamless.Database.Query;
using Seamless.Execution;
using Seamless.Transactions;

namespace Seamless.Database.Gateway.Hosting;

public enum GatewayCommitPhase{BeforeWal,WalDurable,DatabaseApplied,AllDatabasesApplied,DatabaseCheckpointed,PreparationDurable,BeforeCandidateBuild,CandidateBuilt,CandidateCopied,GenerationSealed,BeforePublication,AfterPublication,TransactionStarted,BeforeCommonCheckpointPublication,AfterCommonCheckpointPublication}

/// <summary>接続所有の論理取引。各 DB の準備と反映を、一つの共通 WAL commit で束ねる。</summary>
public sealed partial class GatewayTransactionManager:IDisposable{
    private readonly Dictionary<int,Registration> databases=[];
    private readonly HashSet<Guid> active=[];
    private readonly DatabaseLockManager locks=new();
    private readonly PreparedTransactionJournal journal;
    private readonly TransactionCoordinator commitCoordinator;
    private readonly ResourceBudget budget;
    private readonly string directory;
    private readonly Action<GatewayCommitPhase,int>? failureInjection;
    private readonly object gate=new();
    private bool recovered;
    private bool disposed;
    private CheckpointData? checkpoint;
    public GatewayTransactionManager(string directory,WalOptions? walOptions=null,ResourceBudget? budget=null,Action<GatewayCommitPhase,int>? failureInjection=null):this(directory,walOptions,budget,failureInjection,null){}
    internal GatewayTransactionManager(string directory,WalOptions? walOptions,ResourceBudget? budget,Action<GatewayCommitPhase,int>? failureInjection,DatabaseContainerOptions? databaseOptions){
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);if(!Path.IsPathFullyQualified(directory)){throw new ArgumentException("Gateway storage must use an absolute path.",nameof(directory));}
        this.directory=directory;this.budget=budget??new(256L<<20,16L<<30);this.failureInjection=failureInjection;var versioned=databaseOptions?.TransactionHistory is not null;if(versioned){walOptions=(walOptions??new()) with{CatalogIdentity=(walOptions??new()).CatalogIdentity+"|GatewayDeferredMvccV1"};}this.journal=new(Path.Combine(directory,"common-wal"),walOptions,this.budget,phase=>{if(phase==PreparedTransactionPhase.PreparationDurable){this.failureInjection?.Invoke(GatewayCommitPhase.PreparationDurable,0);}});
        try{
            if(versioned){this.versioned=new(this,databaseOptions!);this.commitCoordinator=this.versioned.Coordinator;}else{this.commitCoordinator=new(null,new TransactionFailure(),this.journal);}
            var format="seamless-gateway-checkpoint-v1";var checkpointVersion=1;if(versioned){format="seamless-gateway-checkpoint-v2";checkpointVersion=2;}
            var path=Path.Combine(directory,"common-checkpoint.json");if(File.Exists(path)){var envelope=JsonSerializer.Deserialize<CheckpointEnvelope>(File.ReadAllBytes(path))??throw new InvalidDataException("Missing common checkpoint.");if(envelope.Format!=format||envelope.Payload is null||!string.Equals(envelope.Sha256,Convert.ToHexString(SHA256.HashData(envelope.Payload)),StringComparison.Ordinal)){throw new InvalidDataException("Common checkpoint hash or storage profile mismatch.");}this.checkpoint=JsonSerializer.Deserialize<CheckpointData>(envelope.Payload)??throw new InvalidDataException("Missing common checkpoint payload.");if(this.checkpoint.Version!=checkpointVersion||this.checkpoint.WalId!=this.journal.WalId||this.checkpoint.Lsn<0||this.checkpoint.Databases.Length>1024||versioned&&this.checkpoint.CommitVersion<1){throw new InvalidDataException("Invalid common checkpoint identity.");}}
        }catch{this.versioned?.Dispose();this.commitCoordinator?.DisposeAsync().AsTask().GetAwaiter().GetResult();this.journal.DisposeAsync().AsTask().GetAwaiter().GetResult();throw;}
    }
    public Guid WalId=>this.journal.WalId;
    public int PendingLockRequests=>this.locks.WaitingCount;
    public GatewayDatabaseProfile Profile(int databaseId){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(databaseId);return new(this.WalId,databaseId);}
    public TransactionOutcome GetOutcome(Guid transactionId)=>this.journal.GetOutcome(transactionId);
    internal void Register(string name,InProcessDatabaseAdapter adapter){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(this.recovered){throw new InvalidOperationException("Register Gateway databases before recovery.");}
            var profile=adapter.Database.Options.GatewayProfile??throw new InvalidOperationException("Gateway registration requires the new storage profile.");
            if(profile.WalId!=this.WalId){throw new InvalidDataException("The database belongs to another Gateway WAL.");}
            if(this.databases.TryGetValue(profile.DatabaseId,out var existing)){if(!ReferenceEquals(existing.Adapter,adapter)){throw new InvalidOperationException("A Gateway database ID is already registered.");}return;}
            if(this.databases.Count>=1024){throw new InvalidOperationException("The Gateway database registration limit was reached.");}
            if(this.versioned is null){adapter.Database.InitializeGatewayStorage();}this.databases.Add(profile.DatabaseId,new(profile.DatabaseId,name,adapter));adapter.GatewayTransactions=this;
        }
    }
    public void Recover(){
        if(this.versioned is not null){this.RecoverVersionedGateway();return;}
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(this.recovered){return;}
            this.RecoverGenerationRegistrations();
            if(this.checkpoint is not null){foreach(var expected in this.checkpoint.Databases){if(!this.databases.TryGetValue(expected.Id,out var registration)||registration.Name!=expected.Name||registration.Adapter.Database.GatewayCheckpointLsn<this.checkpoint.Lsn){throw new InvalidDataException("A common-checkpoint database is missing or has lost its checkpoint.");}}}
            foreach(var registration in this.databases.Values){if(registration.Adapter.Database.GatewayCheckpointLsn>this.journal.DurableLsn){throw new InvalidDataException("A database checkpoint is ahead of the common WAL.");}}
            try{
                this.journal.Replay((id,lsn,metadata,entries)=>{
                    foreach(var entry in entries){if(!this.databases.TryGetValue(entry.Partition,out var registration)){throw new InvalidDataException("A committed Gateway database is not registered.");}registration.Adapter.Database.ReplayGatewayEntry(entry,lsn);}
                },this.checkpoint?.Lsn??0);
                foreach(var registration in this.databases.Values){registration.Adapter.Database.CompleteGatewayRecovery();}this.recovered=true;
            }catch{foreach(var registration in this.databases.Values){registration.Adapter.Database.FaultGateway();}throw;}
        }
    }
    internal void Begin(SessionContext session,Guid? requestedId=null){
        if(this.versioned is not null){this.BeginVersioned(session,requestedId);return;}
        lock(this.gate){
            this.EnsureReady();
            if(session.IsInTransaction){var current=this.Own(session);if(requestedId is{} nested&&nested!=current.Id){throw new InvalidOperationException("The nested request has a different transaction ID.");}session.BeginNestedTransaction();return;}
            if(session.StatementTransaction is not null){throw new InvalidOperationException("Finish the current statement result before starting another transaction.");}
            session.AttachGatewayTransaction(this.NewLegacyTransaction(session,requestedId,false));
        }
    }
    private LogicalTransaction NewLegacyTransaction(SessionContext session,Guid? requestedId,bool statement){
        var transaction=new LogicalTransaction(session.SessionId,requestedId){Manager=this,Session=session,Statement=statement};
        if(transaction.Id==Guid.Empty||this.active.Contains(transaction.Id)||this.journal.GetOutcome(transaction.Id).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidOperationException("The Gateway transaction ID has already been used.");}
        transaction.Memory=this.budget.ReserveMemory(256);
        this.active.Add(transaction.Id);
        return transaction;
    }
    private void EnsureReady(){ObjectDisposedException.ThrowIf(this.disposed,this);if(!this.recovered){throw new InvalidOperationException("Recover all Gateway databases before accepting requests.");}if(this.versioned is not null){this.commitCoordinator.Failure.ThrowIfFailed();}}
    private LogicalTransaction Own(SessionContext session){
        this.EnsureReady();var transaction=session.SnapshotTransaction;
        if(transaction is null||transaction.Manager!=this||transaction.OwnerSessionId!=session.SessionId||!ReferenceEquals(transaction.Session,session)||transaction.Closed){throw new SqlQueryException("The connection does not own this Gateway transaction.",40515);}
        return transaction;
    }
    private IDisposable Acquire(SessionContext session,Guid owner,int databaseId,DatabaseLockMode mode){
        try{return session.AcquireLock(token=>this.locks.AcquireAsync(owner,databaseId,mode,token).GetAwaiter().GetResult());}
        catch(TransactionDeadlockException){if(session.Transaction?.Manager==this){this.Rollback(session);}throw new SqlQueryException("The transaction was chosen as a deadlock victim.",1205);}
    }
    private GatewayParticipant Enlist(SessionContext session,InProcessDatabaseAdapter adapter){
        if(this.versioned is not null){return this.EnlistVersioned(session,adapter);}
        var transaction=this.Own(session);var id=adapter.Database.Options.GatewayProfile!.DatabaseId;
        if(transaction.Participants.TryGetValue(id,out var participant)){if(!ReferenceEquals(participant.Adapter,adapter)){throw new SqlQueryException("The SQL binding belongs to an obsolete transaction generation.",3961);}return participant;}
        var lease=this.Acquire(session,transaction.Id,id,DatabaseLockMode.Read);
        try{this.ValidatePublishedAdapter(adapter);transaction.Memory!.Resize(checked(transaction.Memory.Bytes+512));participant=new(adapter,lease){Epoch=this.PublishedEpoch(id)};transaction.Participants.Add(id,participant);return participant;}
        catch{lease.Dispose();throw;}
    }
    internal void EnsureWrite(SessionContext session,InProcessDatabaseAdapter adapter){
        if(this.versioned is not null){this.EnsureVersionedWrite(session,adapter);return;}
        var transaction=this.Own(session);if(transaction.Statement){throw new SqlQueryException("The transaction is not writable.",3930);}
        if(transaction.IsRollbackOnly){throw new SqlQueryException("The transaction is rollback-only.",3930);}
        var participant=this.Enlist(session,adapter);if(participant.Transaction is not null){return;}
        var id=adapter.Database.Options.GatewayProfile!.DatabaseId;var lease=this.Acquire(session,transaction.Id,id,DatabaseLockMode.Write);
        try{participant.Transaction=adapter.BeginGatewayDatabaseTransaction(new(transaction.Id,session.SessionId),session.CancellationToken);participant.WriteLease=lease;session.RefreshTransactionContext();}
        catch{lease.Dispose();throw;}
    }
    internal IDisposable AcquireRead(SessionContext? session,InProcessDatabaseAdapter adapter){
        if(this.versioned is not null){return this.AcquireVersionedRead(session,adapter);}
        this.EnsureReady();var id=adapter.Database.Options.GatewayProfile!.DatabaseId;
        if(session is not null&&(session.IsInTransaction||session.StatementTransaction is not null)){_ =this.Enlist(session,adapter);return adapter.Database.BeginReadSession(session.CancellationToken);}
        session??=new SessionContext(0,adapter.DatabaseName);var lease=this.Acquire(session,session.ReadLockOwner,id,DatabaseLockMode.Read);
        try{this.ValidatePublishedAdapter(adapter);return new ReadLease(adapter.Database.BeginReadSession(session.CancellationToken),lease);}catch{lease.Dispose();throw;}
    }
    internal void CreateSavepoint(SessionContext session,string name){
        if(this.versioned is not null){this.CreateVersionedSavepoint(session,name);return;}
        var transaction=this.Own(session);if(transaction.IsRollbackOnly){throw new SqlQueryException("The transaction is rollback-only.",3930);}
        if(transaction.Savepoints.Count>=1024){throw new InvalidOperationException("The Gateway savepoint limit was reached.");}
        var physical=Guid.NewGuid().ToString("N");var writers=transaction.Participants.Where(static pair=>pair.Value.Transaction is not null).Select(static pair=>pair.Key).ToArray();
        try{
            transaction.Memory!.Resize(checked(transaction.Memory.Bytes+128+transaction.Participants.Count*8L));
            foreach(var id in writers){transaction.Participants[id].Transaction!.CreateSavepoint(physical);}
            transaction.Savepoints.Add(new(name,physical,transaction.Participants.Keys.ToArray(),writers,new Dictionary<int,GatewayParticipant>(transaction.Participants),new Dictionary<string,GatewayNativeQueryVersion>(transaction.NativeChanges,StringComparer.Ordinal)));
        }catch{transaction.Doomed=true;throw;}
    }
    internal void RollbackToSavepoint(SessionContext session,string name){
        if(this.versioned is not null){this.RollbackVersionedSavepoint(session,name);return;}
        var transaction=this.Own(session);if(transaction.IsRollbackOnly){throw new SqlQueryException("The transaction cannot be rolled back to a savepoint.",3931);}
        var index=transaction.Savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}
        var point=transaction.Savepoints[index];
        try{
            foreach(var item in transaction.Participants.ToArray()){
                if(!point.Participants.Contains(item.Key)){this.DiscardVersions(transaction,item.Key,-1);transaction.Participants.Remove(item.Key);continue;}
                var restored=point.Versions[item.Key];if(!ReferenceEquals(item.Value,restored)){this.DiscardVersions(transaction,item.Key,restored.Version);transaction.RetiredParticipants.Remove(restored);transaction.Participants[item.Key]=restored;}
                if(point.Writers.Contains(item.Key)){restored.Transaction!.RollbackToSavepoint(point.PhysicalName);}
                else if(restored.Transaction is not null){restored.Transaction.Dispose();restored.Transaction=null;restored.SchemaLease?.Dispose();restored.SchemaLease=null;restored.WriteLease?.Dispose();restored.WriteLease=null;}
            }
            transaction.Savepoints.RemoveRange(index+1,transaction.Savepoints.Count-index-1);this.RestoreNativeSavepoint(transaction,point);this.Resize(transaction);session.RefreshTransactionContext();this.generationRegistry?.ReleaseObsoletePlans();
        }catch{transaction.Doomed=true;throw;}
    }
    internal void ReleaseSavepoint(SessionContext session,string name){
        if(this.versioned is not null){this.ReleaseVersionedSavepoint(session,name);return;}
        var transaction=this.Own(session);var index=transaction.Savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}
        var point=transaction.Savepoints[index];try{foreach(var id in point.Writers){point.Versions[id].Transaction!.ReleaseSavepoint(point.PhysicalName);}transaction.Savepoints.RemoveAt(index);this.PruneNativeVersions(transaction);this.Resize(transaction);}catch{transaction.Doomed=true;throw;}
    }
    private void Resize(LogicalTransaction transaction)=>transaction.Memory!.Resize(checked(256+(transaction.Participants.Count+transaction.RetiredParticipants.Count)*512L+transaction.NativeVersions.Count*512L+transaction.Savepoints.Sum(static point=>128+(point.Participants.Length+point.Writers.Length)*12L+point.NativeVersions.Count*64L)));
    internal void Commit(SessionContext session){
        if(this.versioned is not null){this.CommitVersioned(session);return;}
        var transaction=this.Own(session);if(transaction.IsRollbackOnly){throw new SqlQueryException("The transaction is rollback-only.",3930);}if(session.CommitNestedTransaction()){return;}
        var owner=new GatewayTransactionOwner(transaction.Id,session.SessionId);var durable=false;var retired=new List<GatewayPreparedGeneration>();
        try{
            if(this.generationStore is not null){foreach(var participant in transaction.Participants.Values.Where(static item=>item.Transaction is not null).ToArray()){var id=participant.Adapter.Database.Options.GatewayProfile!.DatabaseId;this.ValidateConstraints(session,this.databases[id].Name);}}
            var metadata=this.SealGenerations(session,transaction,owner);
            var prepared=transaction.Participants.Values.Where(static participant=>participant.Candidate is null&&participant.Transaction is not null).OrderBy(static participant=>participant.Adapter.Database.Options.GatewayProfile!.DatabaseId).Select(participant=>(Participant:participant,Rows:participant.Transaction!.PrepareGatewayCommit(owner))).ToArray();
            IEnumerable<WalEntry> Entries(){foreach(var item in prepared){foreach(var entry in item.Rows){yield return entry;}}}
            this.failureInjection?.Invoke(GatewayCommitPhase.BeforeWal,0);
            using var changes=new CommonCommitChanges(Entries,lsn=>{
                durable=true;this.failureInjection?.Invoke(GatewayCommitPhase.WalDurable,0);
                foreach(var item in prepared){item.Participant.Transaction!.ApplyGatewayCommit(owner,lsn);this.failureInjection?.Invoke(GatewayCommitPhase.DatabaseApplied,item.Participant.Adapter.Database.Options.GatewayProfile!.DatabaseId);}
                lock(this.gate){this.PublishGenerations(transaction,lsn,retired);this.PublishNativeVersions(transaction);}this.failureInjection?.Invoke(GatewayCommitPhase.AllDatabasesApplied,0);
            });
            this.commitCoordinator.CommitExternalAsync(transaction.Id,changes,metadata,session.CancellationToken).AsTask().GetAwaiter().GetResult();this.Close(session,transaction,true);
        }catch(Exception error){
            var outcome=TransactionOutcomeKind.Aborted;if(durable){outcome=TransactionOutcomeKind.Committed;}else if(error is TransactionCommitException completion){outcome=completion.Outcome;}
            if(outcome==TransactionOutcomeKind.Aborted){this.journal.RecordAborted(transaction.Id);}
            if(outcome is TransactionOutcomeKind.Committed or TransactionOutcomeKind.Unknown){var changed=transaction.Participants.Where(item=>item.Value.Transaction is not null||item.Value.Candidate is not null||transaction.NativeChanges.Count!=0&&item.Value.SchemaLease is not null).Select(static item=>item.Key).ToHashSet();foreach(var participant in transaction.Participants.Values.Concat(transaction.RetiredParticipants).Where(item=>changed.Contains(item.Adapter.Database.Options.GatewayProfile!.DatabaseId))){participant.Adapter.Database.FaultGateway();}}
            try{this.Close(session,transaction,false);}catch(Exception cleanup){throw new TransactionCommitException(transaction.Id,outcome,new AggregateException(error,cleanup));}
            throw new TransactionCommitException(transaction.Id,outcome,error);
        }finally{foreach(var generation in retired){generation.Dispose();}}
    }
    internal void Rollback(SessionContext session){if(this.versioned is not null){var transaction=this.Own(session);transaction.Completing=true;try{transaction.Scope!.RollbackAsync().AsTask().GetAwaiter().GetResult();}finally{this.CloseVersioned(session,transaction);}return;}var legacy=this.Own(session);this.journal.RecordAborted(legacy.Id);this.Close(session,legacy,false);}
    private void Close(SessionContext session,LogicalTransaction transaction,bool commit){
        if(transaction.Closed){return;}var errors=new List<Exception>();
        foreach(var participant in transaction.Participants.Values){try{CloseParticipant(participant,commit);}catch(Exception error){participant.Adapter.Database.FaultGateway();errors.Add(error);}}
        foreach(var participant in transaction.RetiredParticipants){try{CloseParticipant(participant,false);}catch(Exception error){participant.Adapter.Database.FaultGateway();errors.Add(error);}}
        this.CloseNativeVersions(transaction,errors);
        transaction.Closed=true;
        if(transaction.Statement){session.EndStatementTransaction(transaction);}
        else{session.DetachTransaction();}
        transaction.Participants.Clear();transaction.RetiredParticipants.Clear();transaction.Savepoints.Clear();transaction.Memory?.Dispose();transaction.Memory=null;lock(this.gate){this.active.Remove(transaction.Id);}
        if(errors.Count>0){throw new AggregateException("Gateway transaction cleanup failed.",errors);}
    }
    private static void CloseParticipant(GatewayParticipant participant,bool commit){
        if(participant.Closed){return;}var errors=new List<Exception>();
        void Release(Action action){try{action();}catch(Exception error){participant.Adapter.Database.FaultGateway();errors.Add(error);}}
        Release(()=>{if(participant.Transaction is not null){try{if(commit){participant.Transaction.Commit();}}finally{participant.Transaction.Dispose();participant.Transaction=null;}}});
        Release(()=>{participant.SchemaLease?.Dispose();participant.SchemaLease=null;});Release(()=>{participant.WriteLease?.Dispose();participant.WriteLease=null;});Release(participant.ReadLease.Dispose);
        Release(()=>participant.Candidate?.Dispose());if(!participant.KeepPrepared){Release(()=>participant.Prepared?.Dispose());}participant.Closed=true;if(errors.Count>0){throw new AggregateException("Gateway participant cleanup failed.",errors);}
    }
    public void Checkpoint(){
        if(this.versioned is not null){this.CheckpointVersioned();return;}
        this.EnsureReady();var owner=Guid.NewGuid();var leases=new List<IDisposable>();GatewayGenerationStore.CollectionPlan? collection=null,nativeCollection=null;
        this.commitCoordinator.Failure.ThrowIfFailed();
        try{
            foreach(var id in this.databases.Keys.Order()){leases.Add(this.locks.AcquireAsync(owner,id,DatabaseLockMode.Schema).GetAwaiter().GetResult());}
            this.journal.CheckpointAsync((walId,lsn)=>{
                foreach(var registration in this.databases.Values){registration.Adapter.Database.CheckpointGateway(lsn);this.failureInjection?.Invoke(GatewayCommitPhase.DatabaseCheckpointed,registration.Id);}
                ValueReservation[] values;lock(this.valueGate){values=this.values.Values.ToArray();}var checkpoint=new CheckpointData(1,walId,lsn,this.databases.Values.Select(static registration=>new DatabaseIdentity(registration.Id,registration.Name)).OrderBy(static item=>item.Id).ToArray(),this.published.Values.Select(static item=>item.State).ToArray(),values,this.nativePublished.Values.Select(static item=>item.State).ToArray());
                var payload=JsonSerializer.SerializeToUtf8Bytes(checkpoint);var envelope=new CheckpointEnvelope("seamless-gateway-checkpoint-v1",Convert.ToHexString(SHA256.HashData(payload)),payload);
                var path=Path.Combine(this.directory,"common-checkpoint.json");using(var output=new FileStream(path+".new",FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,envelope);output.Flush(true);}File.Move(path+".new",path,true);this.checkpoint=checkpoint;return ValueTask.CompletedTask;
            }).AsTask().GetAwaiter().GetResult();
            collection=this.generationStore?.PlanCollection(this.published.Values.SelectMany(static generation=>generation.State.Previous is null?new[]{generation.State.Current.Reference}:new[]{generation.State.Current.Reference,generation.State.Previous.Reference}));
            nativeCollection=this.nativeStore?.PlanCollection(this.nativePublished.Values.SelectMany(static native=>native.State.Previous is null?new[]{native.State.Current}:new[]{native.State.Current,native.State.Previous}));
        }finally{for(var index=leases.Count-1;index>=0;index--){leases[index].Dispose();}}
        if(collection is not null){this.LastGenerationCollection=collection.Collect();}
        if(nativeCollection is not null){var native=nativeCollection.Collect();var database=this.LastGenerationCollection;this.LastGenerationCollection=new((database?.Removed??[]).Concat(native.Removed).ToArray(),(database?.Deferred??[]).Concat(native.Deferred).ToArray());}
    }
    public GatewayGenerationCollectionResult? LastGenerationCollection{get;private set;}
    public void Dispose(){lock(this.gate){if(this.disposed){return;}if(this.active.Count!=0){throw new InvalidOperationException("Finish active Gateway transactions before disposing storage.");}this.disposed=true;}try{if(this.versioned is not null){this.WaitForVersionedCollection();this.versioned.Dispose();}else{foreach(var item in this.nativePublished.Values){item.Version.Dispose();}foreach(var item in this.published.Values){item.Prepared.Dispose();}}this.nativePublished.Clear();this.published.Clear();this.databases.Clear();}finally{foreach(var reservation in this.valueMemory){reservation.Dispose();}this.valueMemory.Clear();this.values.Clear();try{this.commitCoordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();}finally{this.journal.DisposeAsync().AsTask().GetAwaiter().GetResult();this.locks.Dispose();}}}
    private sealed record Registration(int Id,string Name,InProcessDatabaseAdapter Adapter);
    private sealed record DatabaseIdentity(int Id,string Name);
    private sealed record CheckpointData(int Version,Guid WalId,long Lsn,DatabaseIdentity[] Databases,PublishedGenerationState[]? Generations=null,ValueReservation[]? Values=null,NativePublishedState[]? NativeQueries=null,long CommitVersion=0,GatewayGenerationReference[]? RetainedGenerations=null);
    private sealed record CheckpointEnvelope(string Format,string Sha256,byte[] Payload);
    private sealed class ReadLease(IDisposable read,IDisposable lease):IDisposable{public void Dispose(){try{read.Dispose();}finally{lease.Dispose();}}}
}
