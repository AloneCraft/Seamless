using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;
using Seamless.Transactions;
namespace Seamless.Database;

/// <summary>Gateway所有者だけが作る共通domainへの接続。公開optionsの別運用モードにはしない。</summary>
internal sealed class GatewayVersionedBinding:IDisposable{
    private readonly object gate=new();
    private readonly TransactionCoordinatorOptions options;
    private readonly Func<bool> admission;
    private readonly ResourceReservation metadata;
    private readonly List<IRoutedVersionedTransactionParticipant> participants=[];
    private DatabaseContainer? database;
    private Task? retirement;
    private bool attached,retired,shutdown,disposed;
    private long pendingBytes=512;
    internal GatewayVersionedBinding(TransactionCoordinator coordinator,VersionedTransactionParticipantGroup group,Guid walId,Guid generationId,TransactionCoordinatorOptions options,ResourceBudget metadataBudget,Func<bool> admission){
        ArgumentNullException.ThrowIfNull(coordinator);ArgumentNullException.ThrowIfNull(group);ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(metadataBudget);ArgumentNullException.ThrowIfNull(admission);
        if(walId==Guid.Empty||generationId==Guid.Empty||group.Partition<=0||options.History is null||options.ParentBudget is null){throw new ArgumentException("A versioned Gateway binding requires stable identities and finite shared budgets.");}
        _=coordinator.GetDomainDiagnostics();this.Coordinator=coordinator;this.Group=group;this.WalId=walId;this.GenerationId=generationId;this.options=options;this.admission=admission;this.metadata=metadataBudget.ReserveMemory(this.pendingBytes);
    }
    internal TransactionCoordinator Coordinator{get;}
    internal VersionedTransactionParticipantGroup Group{get;}
    internal Guid WalId{get;}
    internal Guid GenerationId{get;}
    internal bool NeedsCheckpoint{get{lock(this.gate){return this.attached&&!this.retired&&!this.disposed;}}}
    internal bool IsDisposed{get{lock(this.gate){return this.disposed;}}}
    internal void Validate(DatabaseContainerOptions value){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(value.TransactionHistory!=this.options.History||value.TransactionConcurrency!=this.options.Concurrency||value.RecordSerialization!=DatabaseRecordSerialization.CanonicalV1||value.StorageMode!=DatabaseStorageMode.Storage||!ReferenceEquals(value.ParentBudget,this.options.ParentBudget)||value.GatewayProfile is not{} profile||profile.WalId!=this.WalId||profile.DatabaseId!=this.Group.Partition||profile.GenerationId!=this.GenerationId){throw new ArgumentException("The database must use its owning Gateway versioned domain and generation profile.",nameof(value));}
        }
    }
    internal void Bind(DatabaseContainer owner){lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);if(this.database is not null&&!ReferenceEquals(this.database,owner)){throw new InvalidOperationException("A binding belongs to exactly one physical generation.");}this.database=owner;}}
    internal VersionedParticipantRoute Route(string table){
        var length=Encoding.UTF8.GetByteCount(table);if(length is<1 or>4080){throw new ArgumentOutOfRangeException(nameof(table));}var prefix=new byte[16+length];this.GenerationId.TryWriteBytes(prefix);Encoding.UTF8.GetBytes(table,prefix.AsSpan(16));return new(this.Group,prefix);
    }
    internal void AddPending(IRoutedVersionedTransactionParticipant participant){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(this.attached){throw new InvalidOperationException("Finish the generation schema before attachment.");}
            var before=this.pendingBytes;var bytes=checked(before+128L+participant.Routing!.Prefix.Length*6L+participant.CodecIdentity.Length*2L);this.metadata.Resize(bytes);
            try{this.participants.Add(participant);this.pendingBytes=bytes;}catch{this.metadata.Resize(before);throw;}
        }
    }
    internal void Attach(){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(this.attached){return;}if(this.database is null){throw new InvalidOperationException("Initialize the physical database before attachment.");}
            this.Group.RegisterBatch(this.participants);this.attached=true;this.metadata.Resize(checked(512L+this.participants.Count*128L));
        }
    }
    internal void CheckAdmission(){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed||this.shutdown,this);this.Coordinator.Failure.ThrowIfFailed();
            if(!this.attached||this.retired||!this.admission()){throw new InvalidOperationException("The Gateway generation is not admitted to its common transaction domain.");}
        }
    }
    internal Task RetireAsync(CancellationToken cancellationToken=default){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);cancellationToken.ThrowIfCancellationRequested();if(this.retired){return Task.CompletedTask;}if(!this.attached){this.retired=true;return Task.CompletedTask;}if(this.retirement is not null){return this.retirement;}
            var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);this.retirement=completion.Task;_=this.RetireCore(completion,cancellationToken);return completion.Task;
        }
    }
    internal static async Task RetireBatchAsync(IReadOnlyList<GatewayVersionedBinding> bindings){
        if(bindings.Count==0){return;}var coordinator=bindings[0].Coordinator;var participants=new List<IRoutedVersionedTransactionParticipant>();
        foreach(var binding in bindings){lock(binding.gate){ObjectDisposedException.ThrowIf(binding.disposed,binding);if(!ReferenceEquals(coordinator,binding.Coordinator)||binding.retirement is not null){throw new InvalidOperationException("Retire a generation batch through its single common owner.");}if(!binding.retired&&binding.attached){participants.AddRange(binding.participants);}}}
        // 一括受付し、最初の世代だけがcheckpointを先に終える隙間を作らない。
        if(participants.Count==0){await coordinator.CheckpointVersionedAsync().ConfigureAwait(false);}else{await coordinator.RetireVersionedParticipantsAsync(participants).ConfigureAwait(false);}
        foreach(var binding in bindings){lock(binding.gate){binding.retired=true;}}
    }
    private async Task RetireCore(TaskCompletionSource completion,CancellationToken cancellationToken){
        try{
            if(this.participants.Count==0){cancellationToken.ThrowIfCancellationRequested();await this.Coordinator.CheckpointVersionedAsync().ConfigureAwait(false);}
            else{await this.Coordinator.RetireVersionedParticipantsAsync(this.participants,cancellationToken).ConfigureAwait(false);}
            lock(this.gate){this.retired=true;}
            completion.TrySetResult();
        }catch(OperationCanceledException error){completion.TrySetCanceled(error.CancellationToken);}
        catch(Exception error){completion.TrySetException(error);}
        finally{lock(this.gate){this.retirement=null;}}
    }
    internal void AllowDisposeForShutdown(){
        lock(this.gate){if(this.disposed||this.retired||!this.attached){this.shutdown=true;return;}}
        if(this.Coordinator.HasCurrentTransaction){throw new InvalidOperationException("Finish the current scope before stopping the common Gateway domain.");}
        this.Coordinator.StopAccepting();using var lease=this.Coordinator.EnterReadForShutdown();lock(this.gate){this.shutdown=true;}
    }
    internal void CheckCanDispose(){lock(this.gate){if(this.attached&&!this.retired&&!this.shutdown){throw new InvalidOperationException("Retire this generation or stop its common domain before disposing its store.");}}}
    public void Dispose(){lock(this.gate){if(this.disposed){return;}this.CheckCanDispose();this.disposed=true;this.participants.Clear();this.database=null;this.metadata.Dispose();}}
}
