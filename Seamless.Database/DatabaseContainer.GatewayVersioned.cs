using Seamless.Transactions;
using Seamless.Transactions.Collections;
using Seamless.Execution;
using System.Threading;
namespace Seamless.Database;

public abstract partial class DatabaseContainer{
    private readonly AsyncLocal<GatewayVersionedExecution?> gatewayVersionedExecution=new();
    internal IDisposable EnterGatewayVersionedExecution(GatewayTransactionOwner owner,TransactionScope scope){
        this.EnsureAvailable();var binding=this.Options.GatewayVersionedBinding??throw new InvalidOperationException("A versioned Gateway binding is required.");binding.CheckAdmission();scope.ValidateOwner(binding.Coordinator);
        if(owner.TransactionId==Guid.Empty||owner.TransactionId!=scope.Id||this.gatewayVersionedExecution.Value is not null||this.ambientTransaction.Value is not null||this.ambientReadSession.Value is not null){
            var message="The Gateway execution must use its owning transaction and an unused database context.";
#if DEBUG
            message+=" scopeMatches="+(owner.TransactionId==scope.Id)+" previousCompleted="+this.gatewayVersionedExecution.Value?.Completed+" previousDisposed="+this.gatewayVersionedExecution.Value?.Disposed+" legacy="+(this.ambientTransaction.Value is not null)+" readDepth="+this.ambientReadSession.Value?.Depth;
#endif
            throw new InvalidOperationException(message);
        }
        return this.EnterGatewayVersionedContext(scope);
    }
    private IDisposable EnterGatewayVersionedContext(TransactionScope scope){var state=new GatewayVersionedExecution(this,scope,this.executionResources.Budget.ReserveMemory(512));try{state.SetCompletion(scope.OnCompletion(state.Complete));this.gatewayVersionedExecution.Value=state;return state;}catch{state.Complete();throw;}}
    internal TResult ReadGatewayVersionedSeed<TResult>(GatewayTransactionOwner owner,Func<TResult> read){
        ArgumentNullException.ThrowIfNull(read);this.EnsureAvailable();this.EnsureSharedStorage();var seed=this.Options.GatewayVersionedSeed??throw new InvalidOperationException("A private seed is required.");seed.CheckOwner(this,owner);
        if(!this.sharedReady||this.unpublishedSnapshot is not null||this.gatewayVersionedExecution.Value is not null){throw new InvalidOperationException("Finish the private snapshot before reading its import metadata.");}
        var scope=this.sharedCoordinator!.BeginOwnedAsync(Guid.NewGuid(),true).AsTask().GetAwaiter().GetResult();try{using var context=this.EnterGatewayVersionedContext(scope);return read();}finally{scope.DisposeAsync().AsTask().GetAwaiter().GetResult();}
    }
    private GatewayVersionedExecution CurrentGatewayVersionedExecution(bool write){
        this.ValidateOverlayWrite(write);this.CheckGatewayVersionedAdmission();var state=this.gatewayVersionedExecution.Value??throw new InvalidOperationException("Use the owning Gateway scope to access versioned database tables.");
        ObjectDisposedException.ThrowIf(state.Disposed,state);state.Scope.ValidateOwner(this.sharedCoordinator!);if(write&&state.Scope.IsReadOnly){throw new InvalidOperationException("The Gateway scope is read-only.");}return state;
    }
    internal bool TryGatewayVersionedCollection<TKey,TRecord>(StoredTable<TKey,TRecord> table,bool write,[global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)]out TransactionCollection<TKey,TRecord>? collection) where TKey:struct where TRecord:struct{
        if(this.Options.GatewayVersionedBinding is null&&this.Options.GatewayVersionedSeed is null){collection=null;return false;}var state=this.CurrentGatewayVersionedExecution(write);collection=this.GetTransactionCollection(state.Scope,table);return true;
    }
    internal bool TryGatewayVersionedCollection<TKey,TRecord>(MaterializedView<TKey,TRecord> view,bool write,[global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)]out TransactionCollection<TKey,TRecord>? collection) where TKey:struct where TRecord:struct{
        if(this.Options.GatewayVersionedBinding is null&&this.Options.GatewayVersionedSeed is null){collection=null;return false;}var state=this.CurrentGatewayVersionedExecution(write);collection=this.GetMaterializedViewImportCollection(state.Scope,view);return true;
    }
    private sealed class GatewayVersionedExecution:IDisposable{
        private readonly object gate=new();
        private readonly AsyncLocal<GatewayVersionedExecution?> context;
        private readonly AsyncLocal<DatabaseReadSessionState?> reads;
        private ResourceReservation? memory;
        private IDisposable? completion;
        internal GatewayVersionedExecution(DatabaseContainer database,TransactionScope scope,ResourceReservation memory){this.context=database.gatewayVersionedExecution;this.reads=database.ambientReadSession;this.Scope=scope;this.memory=memory;}
        internal TransactionScope Scope{get;}
        internal bool Disposed{get;private set;}
        private bool completed;
        internal bool Completed{get{lock(this.gate){return this.completed;}}}
        internal void SetCompletion(IDisposable value){lock(this.gate){if(this.completed){value.Dispose();}else{this.completion=value;}}}
        internal void Complete(){lock(this.gate){this.completed=true;this.completion=null;Interlocked.Exchange(ref this.memory,null)?.Dispose();}}
        public void Dispose(){
            lock(this.gate){
                var current=ReferenceEquals(this.context.Value,this);
                if(this.Disposed){if(current&&this.reads.Value is null){this.context.Value=null;}return;}
                // root完了後は資源を既に返している。列挙子の別の捕捉contextからもtokenを閉じられるが、活動中の別contextは変更しない。
                if(current&&this.reads.Value is not null||!current&&!this.completed){
                    var message="Finish read sessions and dispose Gateway execution in its owning context.";
#if DEBUG
                    message+=" contextMatches="+ReferenceEquals(this.context.Value,this)+" readDepth="+this.reads.Value?.Depth;
#endif
                    throw new InvalidOperationException(message);
                }
                if(current){this.context.Value=null;}this.Disposed=true;this.completion?.Dispose();this.completion=null;this.Complete();
            }
        }
    }
    internal void InitializeGatewayVersionedStorage(string? expectedSeedHash=null){
        var binding=this.Options.GatewayVersionedBinding??throw new InvalidOperationException("The database has no common versioned Gateway owner.");
        this.EnsureAvailable();try{
            this.EnsureSharedReady();
            if(!this.gatewayCheckpointVerified){
                if(this.sharedCheckpoint.DataSha256 is not null||expectedSeedHash is not null){
                    foreach(var handle in this.sharedHandles){handle.Release();}
                    var actual=ComputeGatewayDataHash(this.Options.GatewayProfile!,this.sharedParticipants.Values);
                    if(this.sharedCheckpoint.DataSha256 is{} expected&&actual!=expected||this.sharedCheckpoint.Lsn==0&&expectedSeedHash is not null&&actual!=expectedSeedHash){throw new InvalidDataException("The versioned Gateway checkpoint data hash does not match its saved rows or sealed seed.");}
                    foreach(var handle in this.sharedHandles){handle.Acquire();}
                }
                this.gatewayCheckpointVerified=true;
            }
            binding.Attach();
        }catch{this.faulted=true;throw;}
    }
    internal (long Lsn,long Version) GatewayVersionedCheckpoint{get{this.EnsureSharedReady();return(this.sharedCheckpoint.Lsn,this.sharedCheckpoint.CommitVersion);}}
    internal GatewayPreparedSnapshot SealGatewayVersionedSeed(GatewayTransactionOwner owner){
        this.EnsureAvailable();var seed=this.Options.GatewayVersionedSeed??throw new InvalidOperationException("A private versioned seed capability is required.");this.EnsureSharedStorage();seed.CheckOwner(this,owner);
        if(!this.sharedReady||this.sharedCheckpoint.CommitVersion!=1||this.sharedCheckpoint.Lsn!=0||this.unpublishedSnapshot is not null||this.ambientTransaction.Value is not null||this.ambientReadSession.Value is not null||this.sharedCoordinator!.HasCurrentTransaction){throw new InvalidOperationException("Finish the initial snapshot before sealing the private seed.");}
        this.sharedCoordinator.StopAccepting();using var drained=this.sharedCoordinator.EnterReadForShutdown();
        try{
            foreach(var handle in this.sharedHandles){handle.Release();}
            this.sharedCheckpoint.DataSha256=ComputeGatewayDataHash(this.Options.GatewayProfile!,this.sharedParticipants.Values);this.sharedStore!.Save();seed.Complete();return new(this.sharedStore.CurrentCheckpoint!.Value,this.sharedCheckpoint.DataSha256);
        }catch{this.faulted=true;throw;}
    }
    internal void SaveGatewayVersionedCheckpoint(long lsn,long version){
        var binding=this.Options.GatewayVersionedBinding??throw new InvalidOperationException("A common Gateway owner is required.");this.EnsureAvailable();binding.Coordinator.Failure.ThrowIfFailed();var state=binding.Coordinator.GetDomainDiagnostics();
        if(!state.Reclaiming||state.ActiveTransactions!=0||state.ActiveOperations!=0||lsn!=binding.Coordinator.AppliedLsn||version!=state.PublishedVersion){throw new InvalidOperationException("Save every generation only from the drained common checkpoint callback.");}
        using(GatewayCheckpointActivities.StartActivity("Hash database rows")){
            this.sharedCheckpoint.DataSha256=ComputeGatewayDataHash(this.Options.GatewayProfile!,this.sharedParticipants.Values);
        }
        this.sharedCheckpoint.Lsn=lsn;this.sharedCheckpoint.CommitVersion=version;this.sharedStore!.Save();
    }
    private static readonly global::System.Diagnostics.ActivitySource GatewayCheckpointActivities=new("Seamless.Checkpoint.Database");
    private void CheckGatewayVersionedAdmission()=>this.Options.GatewayVersionedBinding?.CheckAdmission();
    private void DisposeGatewayVersionedStorage(){
        var binding=this.Options.GatewayVersionedBinding!;binding.CheckCanDispose();List<Exception>? errors=null;
        lock(this.gate){
            if(this.disposed){return;}this.disposed=true;
            foreach(var table in this.tables.Values){try{table.Dispose();}catch(Exception error){(errors??=[]).Add(error);}}
            this.tables.Clear();this.sharedParticipants.Clear();this.sharedHandles.Clear();
        }
        try{this.sharedStore?.Dispose();}catch(Exception error){(errors??=[]).Add(error);}
        try{this.executionResources.Dispose();}catch(Exception error){(errors??=[]).Add(error);}
        try{binding.Dispose();}catch(Exception error){(errors??=[]).Add(error);}
        this.sharedStore=null;this.sharedCoordinator=null;
        if(errors is not null){throw new AggregateException("Versioned Gateway generation cleanup failed.",errors);}
    }
}
