namespace Seamless.Transactions;
public interface ITransactionChanges:IDisposable{
    ITransactionChanges CreateSnapshot()=>throw new NotSupportedException("This transaction participant does not support savepoints.");
    IReadOnlyList<WalEntry> Encode();
    IEnumerable<WalEntry> EnumerateEncoded()=>this.Encode();
    IReadOnlyList<WalEntry> EncodeBounded(int maximumBytes)=>this.Encode();
    void Apply();
}
public delegate void TransactionOperation<TChanges,TState>(TChanges changes,ref TState state) where TChanges:class,ITransactionChanges;
/// <summary>共通WALの耐久化済みLSNを使って全参加先を反映・公開する外部adapter。</summary>
public interface ICommittedTransactionChanges:ITransactionChanges{
    void Apply(long durableLsn);
}
public interface ITransactionParticipant{
    int Partition{get;}
    int TableId{get;}
    string CodecIdentity{get;}
    ITransactionChanges CreateChanges();
    void Replay(WalEntry entry);
    TransactionParticipantSnapshot GetCommittedSnapshot()=>throw new NotSupportedException("This participant cannot verify a persisted Gateway snapshot.");
}
public readonly record struct TransactionParticipantSnapshot(long RowCount,string Sha256);
public enum TransactionCommitKind{Voted,NoChanges,Committed}
public readonly record struct TransactionCommitResult(Guid Id,TransactionCommitKind Kind,long Lsn);
public sealed class TransactionCommitException(Guid id,TransactionOutcomeKind outcome,Exception inner):IOException("Transaction completion failed; inspect Outcome before retrying.",inner){
    public Guid TransactionId{get;}=id;
    public TransactionOutcomeKind Outcome{get;}=outcome;
}
public sealed partial class TransactionCoordinator:IAsyncDisposable{
    private readonly object initialization=new();
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly AsyncLocal<Marker?> ambient=new();
    private readonly Dictionary<(int,int),ITransactionParticipant> participants=[];
    private readonly TransactionJournal? journal;
    private readonly PreparedTransactionJournal? preparedJournal;
    private readonly TransactionFailure failure;
    private bool started;
    private bool disposed;
    private volatile bool stopping;
    private Guid lastAborted;
    private readonly OptimisticDomain? versioned;
    public TransactionCoordinator(TransactionJournal? journal=null,TransactionFailure? failure=null,PreparedTransactionJournal? preparedJournal=null):this(journal,failure,preparedJournal,new()){}
    public TransactionCoordinator(TransactionJournal? journal,TransactionFailure? failure,PreparedTransactionJournal? preparedJournal,TransactionCoordinatorOptions options){
        ArgumentNullException.ThrowIfNull(options);options.Validate();
        if(journal is not null&&preparedJournal is not null){throw new ArgumentException("Choose one WAL profile.");}
        this.journal=journal;this.preparedJournal=preparedJournal;this.failure=failure??journal?.Writer.Failure??preparedJournal?.Failure??new();
        this.Concurrency=options.Concurrency;
        if(options.History is not null){this.versioned=new(this,options);}
    }
    public TransactionConcurrencyMode Concurrency{get;}
    public bool HasCurrentTransaction=>this.ambient.Value?.Active==true;
    private readonly List<Action<TransactionScope>> commitValidators=[];
    public void RegisterCommitValidator(Action<TransactionScope> validator){lock(this.initialization){if(this.started){throw new InvalidOperationException("Register validators before starting transactions.");}this.commitValidators.Add(validator);}}
    internal void ValidateCommit(TransactionScope scope){foreach(var validator in this.commitValidators){validator(scope);}}
    public long AppliedLsn{get;private set;}
    public TransactionFailure Failure=>this.failure;
    public void Register(ITransactionParticipant participant){
        ArgumentNullException.ThrowIfNull(participant);
        if(participant is IRoutedVersionedTransactionParticipant{Routing:not null}){throw new InvalidOperationException("Register the routed table's logical group, not the child table.");}
        if(this.started){throw new InvalidOperationException("Register the complete typed schema before starting transactions.");}
        if(this.versioned is not null&&participant is not IVersionedTransactionParticipant){throw new NotSupportedException("A versioned domain requires versioned participants.");}
        if(this.versioned is null&&participant is IVersionedTransactionParticipant){throw new NotSupportedException("A versioned participant requires finite domain budgets.");}
        if(!this.participants.TryAdd((participant.Partition,participant.TableId),participant)){throw new InvalidOperationException("Duplicate transaction participant.");}
        try{this.versioned?.Register((IVersionedTransactionParticipant)participant);}catch{this.participants.Remove((participant.Partition,participant.TableId));throw;}
    }
    public void Recover(long checkpointLsn=0){
        lock(this.initialization){this.RecoverCore(checkpointLsn);}
    }
    private void RecoverCore(long checkpointLsn){
        if(this.started){return;}
        this.failure.ThrowIfFailed();
        try{
            this.versioned?.InitializeVersion();
            if(this.preparedJournal is not null){
                if(checkpointLsn<0||checkpointLsn>this.preparedJournal.DurableLsn){throw new InvalidDataException("Checkpoint is outside retained WAL coverage.");}
                this.preparedJournal.Replay((id,lsn,metadata,entries)=>{
                    var version=this.versioned?.NextRecoveryVersion()??0;
                    foreach(var entry in entries){
                        this.Replay(this.ResolveReplayParticipant(entry),entry,id,version,lsn);
                    }
                    this.AppliedLsn=Math.Max(this.AppliedLsn,lsn);
                },checkpointLsn);
            }
            if(this.journal is not null){
                if(checkpointLsn>this.journal.Device.RecoveredLsn||checkpointLsn<this.journal.Device.FirstAvailableLsn-1){throw new InvalidDataException("Checkpoint is outside retained WAL coverage.");}
                foreach(var record in this.journal.ReadTransactions()){
                    if(record.Draft.IsPreparation){throw new InvalidDataException("Prepared WAL requires the prepared transaction replay path.");}
                    if(record.Lsn<=checkpointLsn){continue;}
                    foreach(var entry in record.Draft.Entries){
                        this.ResolveReplayParticipant(entry);
                    }
                    var version=this.versioned?.NextRecoveryVersion()??0;
                    foreach(var entry in record.Draft.Entries){this.Replay(this.ResolveReplayParticipant(entry),entry,record.Draft.Id,version,record.Lsn);}
                    this.AppliedLsn=record.Lsn;
                }
            }
            this.AppliedLsn=Math.Max(this.AppliedLsn,checkpointLsn);this.started=true;
        }catch(Exception error){this.failure.Fail(error);throw;}
    }
    public ValueTask<TransactionScope> BeginAsync(CancellationToken cancellationToken=default)=>this.BeginCore(false,cancellationToken);
    public ValueTask<TransactionScope> BeginReadOnlyAsync(CancellationToken cancellationToken=default)=>this.BeginCore(true,cancellationToken);
    private ValueTask<TransactionScope> BeginCore(bool readOnly,CancellationToken cancellationToken){
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Start a joined child through the existing transaction scope.");}
        ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();
        if(!this.started){this.Recover();}
        // 非asyncの入口でholderを設定し、await先でAsyncLocalを書き戻すことに依存しない。
        var marker=new Marker();this.ambient.Value=marker;return this.Acquire(marker,readOnly,cancellationToken);
    }
    private async ValueTask<TransactionScope> Acquire(Marker marker,bool readOnly,CancellationToken cancellationToken,Guid? requestedId=null){
        var acquired=false;TransactionContext? context=null;
        try{
            if(this.Concurrency==TransactionConcurrencyMode.Exclusive){await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);acquired=true;marker.HoldsExclusiveGate=true;}
            cancellationToken.ThrowIfCancellationRequested();ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();
            var id=Guid.NewGuid();if(requestedId is{} requested){id=requested;}context=new(id,readOnly);
            if(this.versioned is not null){await this.versioned.Start(context,cancellationToken).ConfigureAwait(false);}
            marker.Context=context;return new TransactionScope(this,marker,context);
        }catch{marker.Active=false;if(context is not null){this.versioned?.End(context);}if(acquired){this.gate.Release();}throw;}
    }
    public IDisposable EnterRead(CancellationToken cancellationToken=default){
        if(this.versioned is not null){throw new NotSupportedException("Use a versioned read-only transaction; raw reads cannot select a snapshot.");}
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Use the explicit transaction collection view.");}
        ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);
        this.failure.ThrowIfFailed();this.gate.Wait(cancellationToken);
        try{ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();return new ReadLease(this.gate);}catch{this.gate.Release();throw;}
    }
    internal void Release(Marker marker){marker.Active=false;if(marker.Context is not null){this.versioned?.End(marker.Context);}if(marker.HoldsExclusiveGate){this.gate.Release();}}
    public void StopAccepting()=>this.stopping=true;
    /// <summary>外部所有者が分離性と変更集合の凍結を確立した確定要求。版付きscopeの代用にはしない。</summary>
    public async ValueTask<TransactionCommitResult> CommitExternalAsync(Guid id,ICommittedTransactionChanges changes,ReadOnlyMemory<byte> metadata=default,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(changes);if(id==Guid.Empty){throw new ArgumentException("A transaction identity is required.",nameof(id));}
        if(this.versioned is not null||this.preparedJournal is null){throw new NotSupportedException("External commit requires the non-versioned prepared-journal adapter.");}
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Finish the ambient scope before submitting an external commit.");}
        ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();if(this.preparedJournal.GetOutcome(id).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidOperationException("The external transaction has already completed.");}return await this.Commit(id,[changes],metadata,cancellationToken:cancellationToken,forceCommitRecord:true).ConfigureAwait(false);}
        finally{this.gate.Release();}
    }
    public IDisposable EnterReadForShutdown(){if(this.versioned is not null){this.versioned.WaitForDrain();}this.gate.Wait();return new ReadLease(this.gate);}
    internal bool Owns(ITransactionParticipant participant){
        if(participant is IRoutedVersionedTransactionParticipant{Routing:{} route} child){
            var group=route.Group;return this.participants.TryGetValue((group.Partition,group.TableId),out var owner)&&ReferenceEquals(owner,group)&&group.Owns(child);
        }
        return this.participants.TryGetValue((participant.Partition,participant.TableId),out var actual)&&ReferenceEquals(participant,actual);
    }
    private ITransactionParticipant ResolveReplayParticipant(WalEntry entry){
        if(this.participants.TryGetValue((entry.Partition,entry.TableId),out var actual)&&actual.CodecIdentity==entry.Codec){return actual;}
        IVersionedTransactionParticipant? match=null;
        foreach(var group in this.participants.Values.OfType<IVersionedTransactionParticipantGroup>()){
            if(group.Partition!=entry.Partition||!group.TryResolve(entry,out var child)){continue;}
            if(!group.Owns(child)||child.Partition!=entry.Partition||child.TableId!=entry.TableId||child.CodecIdentity!=entry.Codec||match is not null){throw new InvalidDataException("Ambiguous or invalid WAL participant route.");}match=child;
        }
        return match??throw new InvalidDataException("WAL table/schema identity mismatch.");
    }
    internal async ValueTask<TransactionCommitResult> Commit(Guid id,IReadOnlyList<ITransactionChanges> changes,ReadOnlyMemory<byte> metadata=default,bool readOnly=false,TransactionContext? context=null,CancellationToken cancellationToken=default,bool forceCommitRecord=false,Action<TransactionInstallationContext>? installed=null){
        if(this.versioned is not null){return await this.versioned.Commit(context!,changes,metadata,cancellationToken,installed).ConfigureAwait(false);}
        if(installed is not null){throw new NotSupportedException("An installation observer requires a versioned domain.");}
        cancellationToken.ThrowIfCancellationRequested();
        if(readOnly){this.failure.ThrowIfFailed();return new(id,TransactionCommitKind.NoChanges,0);}
        long lsn=0;var durable=false;var submissionAttempted=false;var installationStarted=false;
        try{
            this.failure.ThrowIfFailed();
            if(this.preparedJournal is not null){
                if(forceCommitRecord){
                    var externalCommit=await this.preparedJournal.CommitAsync(id,changes.SelectMany(static change=>change.EnumerateEncoded()),metadata,cancellationToken).ConfigureAwait(false);lsn=externalCommit.Lsn;durable=true;
                    foreach(var change in changes){ApplyCommitted(change,lsn);}this.AppliedLsn=Math.Max(this.AppliedLsn,lsn);this.failure.ThrowIfFailed();return externalCommit;
                }
                using var iterator=changes.SelectMany(static change=>change.EnumerateEncoded()).GetEnumerator();var hasRows=iterator.MoveNext();
                if(!hasRows&&metadata.IsEmpty){return new(id,TransactionCommitKind.NoChanges,this.AppliedLsn);}
                IEnumerable<WalEntry> Rows(){if(hasRows){do{yield return iterator.Current;}while(iterator.MoveNext());}}
                var committed=await this.preparedJournal.CommitAsync(id,Rows(),metadata).ConfigureAwait(false);lsn=committed.Lsn;durable=true;
                foreach(var change in changes){ApplyCommitted(change,lsn);}this.AppliedLsn=Math.Max(this.AppliedLsn,lsn);this.failure.ThrowIfFailed();return committed;
            }
            this.failure.ThrowIfFailed();var entries=new List<WalEntry>();var parts=new SortedSet<int>();long bytes=checked(60L+metadata.Length);
            foreach(var change in changes){
                IReadOnlyList<WalEntry> encoded;
                if(this.journal is not null){encoded=change.EncodeBounded(checked((int)(this.journal.Writer.MaximumFrameBytes-bytes)));}else{encoded=change.Encode();}
                foreach(var entry in encoded){
                    bytes=checked(bytes+WalFormat.EntryPayloadBytes(entry.Codec,entry.Key.Length,entry.Row?.Length??0));if(parts.Add(entry.Partition)){bytes=checked(bytes+4);}
                    if(this.journal is not null&&bytes>this.journal.Writer.MaximumFrameBytes){throw new InvalidDataException("Transaction exceeds the WAL frame bound.");}
                    entries.Add(entry);
                }
            }
            if(entries.Count==0&&metadata.IsEmpty){return new(id,TransactionCommitKind.NoChanges,this.AppliedLsn);}
            if(parts.Count==0){parts.Add(0);}
            if(this.journal is not null){
                submissionAttempted=true;
                var receipt=await this.journal.AppendAsync(new(id,parts.ToArray(),entries,metadata.ToArray())).ConfigureAwait(false);
                lsn=receipt.Lsn;await receipt.Durable.ConfigureAwait(false);durable=true;
            }
            installationStarted=true;
            foreach(var change in changes){ApplyCommitted(change,lsn);}
            this.AppliedLsn=Math.Max(this.AppliedLsn,lsn);this.failure.ThrowIfFailed();
            return new(id,TransactionCommitKind.Committed,lsn);
        }catch(Exception error){
            var outcome=error is TransactionCommitException commitError?commitError.Outcome:TransactionOutcomeKind.Unknown;if(durable){outcome=TransactionOutcomeKind.Committed;}
            // WAL要求と適用のどちらも始まっていない拒否は、健全な他の取引を停止しない。
            if(this.preparedJournal is null&&!submissionAttempted&&!installationStarted&&this.failure.Error is null&&(this.journal is null||this.journal.Writer.Failure.Error is null)){
                this.RecordAborted(id);throw new TransactionCommitException(id,TransactionOutcomeKind.Aborted,error);
            }
            // 外部adapterの準備失敗は、最終commit未受付とjournal健全性を確認できれば個別中止。
            // durable後の反映失敗、受付済みで結果不明、WAL自体の故障だけをdomain停止へ進める。
            if(forceCommitRecord&&!durable&&this.preparedJournal!.Failure.Error is null){
                if(error is not TransactionCommitException){outcome=TransactionOutcomeKind.Aborted;}
                if(outcome==TransactionOutcomeKind.Aborted){this.RecordAborted(id);throw new TransactionCommitException(id,outcome,error);}
            }
            this.failure.Fail(error);this.journal?.Writer.Fail(error);
            throw new TransactionCommitException(id,outcome,error);
        }
    }
    private static void ApplyCommitted(ITransactionChanges change,long lsn){if(change is ICommittedTransactionChanges committed){committed.Apply(lsn);}else{change.Apply();}}
    internal void RecordAborted(Guid id){if(this.preparedJournal is not null){this.preparedJournal.RecordAborted(id);}else if(this.journal is not null){this.journal.RecordAborted(id);}else{lock(this.initialization){this.lastAborted=id;}}}
    public ValueTask<TransactionOutcome> GetOutcomeAsync(Guid id){
        if(this.preparedJournal is not null){return ValueTask.FromResult(this.preparedJournal.GetOutcome(id));}
        if(this.journal is not null){return ValueTask.FromResult(this.journal.GetOutcome(id));}
        lock(this.initialization){var kind=TransactionOutcomeKind.Unknown;if(id!=Guid.Empty&&id==this.lastAborted){kind=TransactionOutcomeKind.Aborted;}return ValueTask.FromResult(new TransactionOutcome(id,kind,0));}
    }
    public ValueTask DisposeAsync(){
        if(this.HasCurrentTransaction||this.versioned?.HasActive==true||!this.gate.Wait(0)){throw new InvalidOperationException("Finish active transactions before disposing the coordinator.");}
        try{this.versioned?.Dispose();this.disposed=true;}catch{this.gate.Release();throw;}
        this.gate.Release();this.gate.Dispose();return ValueTask.CompletedTask;
    }
    internal sealed class Marker{public volatile bool Active=true;public bool HoldsExclusiveGate;public TransactionContext? Context;}
    private sealed class ReadLease(SemaphoreSlim gate):IDisposable{
        private SemaphoreSlim? owner=gate;
        public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.Release();
    }
}
public sealed partial class TransactionScope:IAsyncDisposable{
    private readonly State state;
    private readonly TransactionScope? parent;
    private readonly global::Seamless.Execution.ResourceReservation? metadata;
    private bool closed;
    internal TransactionScope(TransactionCoordinator owner,TransactionCoordinator.Marker marker,TransactionContext context){this.state=new(owner,marker,context);this.state.Current=this;this.state.Context.Retire=this.RetireReadOnly;}
    private TransactionScope(State state,TransactionScope parent){this.state=state;this.parent=parent;this.metadata=state.Context.Domain?.ReserveMetadata(state.Context,128);}
    public Guid Id=>this.state.Id;
    public bool IsReadOnly=>this.state.Context.IsReadOnly;
    internal void ValidateOwner(TransactionCoordinator owner){if(!ReferenceEquals(this.state.Owner,owner)){throw new ArgumentException("The scope belongs to another transaction owner.",nameof(owner));}using var access=this.Enter(allowQuery:true);}
    public TransactionScope BeginNested(){
        using var access=this.Enter();var child=new TransactionScope(this.state,this);this.state.Current=child;return child;
    }
    public void CreateSavepoint(string name){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var access=this.Enter();
        if(this.state.Aborted){throw new InvalidOperationException("The transaction is rollback-only.");}
        var point=new Savepoint(name,this.SavepointMetadata(name));
        try{
            foreach(var pair in this.state.Changes){point.Changes.Add(pair.Key,pair.Value.CreateSnapshot());}
            foreach(var change in this.state.Extra){point.Extra.Add(change.CreateSnapshot());}
            this.state.Savepoints.Add(point);
        }catch{point.Dispose();throw;}
    }
    public void RollbackToSavepoint(string name){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var access=this.Enter();
        if(this.state.Aborted){throw new InvalidOperationException("The transaction is rollback-only.");}
        var index=this.state.Savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));
        if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}
        var restored=new Savepoint(name,this.SavepointMetadata(name));
        try{
            foreach(var pair in this.state.Savepoints[index].Changes){restored.Changes.Add(pair.Key,pair.Value.CreateSnapshot());}
            foreach(var change in this.state.Savepoints[index].Extra){restored.Extra.Add(change.CreateSnapshot());}
        }catch{restored.Dispose();throw;}
        foreach(var change in this.state.Changes.Values.Concat(this.state.Extra)){change.Dispose();}
        this.state.Changes.Clear();this.state.Extra.Clear();
        foreach(var pair in restored.Changes){this.state.Changes.Add(pair.Key,pair.Value);}
        this.state.Extra.AddRange(restored.Extra);restored.Changes.Clear();restored.Extra.Clear();
        restored.Dispose();
        for(var item=this.state.Savepoints.Count-1;item>index;item--){this.state.Savepoints[item].Dispose();this.state.Savepoints.RemoveAt(item);}
    }
    public void ReleaseSavepoint(string name){
        using var access=this.Enter();var index=this.state.Savepoints.FindLastIndex(point=>string.Equals(point.Name,name,StringComparison.Ordinal));
        if(index<0){throw new KeyNotFoundException("Savepoint not found: "+name);}
        this.state.Savepoints[index].Dispose();this.state.Savepoints.RemoveAt(index);
    }
    public void Enlist(ITransactionChanges changes){using var access=this.Enter();this.state.Context.EnsureWritable();if(this.state.Context.Domain is not null){throw new NotSupportedException("Register versioned participants before starting the domain.");}this.state.Extra.Add(changes);}
    public T Use<T,TResult>(ITransactionParticipant participant,Func<TResult,T> action) where TResult:class,ITransactionChanges{
        using var access=this.Enter(allowQuery:true);var changes=this.GetChanges(participant);
        var result=action((TResult)changes);this.state.Context.ThrowIfUnavailable();return result;
    }
    /// <summary>引数をキャプチャせず、単一の取引操作へ渡す。</summary>
    public T Use<T,TChanges,TState>(ITransactionParticipant participant,TState value,Func<TChanges,TState,T> action) where TChanges:class,ITransactionChanges{
        // 単一の操作として扱い、クエリ用の再入権限は付与しない。
        using var access=this.Enter(allowQuery:true);var changes=this.GetChanges(participant);
        var result=action((TChanges)changes,value);this.state.Context.ThrowIfUnavailable();return result;
    }
    public void Use<TChanges,TState>(ITransactionParticipant participant,ref TState value,TransactionOperation<TChanges,TState> operation) where TChanges:class,ITransactionChanges{
        using var access=this.Enter(allowQuery:true);var changes=this.GetChanges(participant);var previous=this.state.QueryThread;this.state.QueryThread=Environment.CurrentManagedThreadId;
        try{operation((TChanges)changes,ref value);this.state.Context.ThrowIfUnavailable();}finally{this.state.QueryThread=previous;}
    }
    private ITransactionChanges GetChanges(ITransactionParticipant participant){
        this.state.Owner.Failure.ThrowIfFailed();
        if(this.state.Aborted){throw new InvalidOperationException("The joined transaction was aborted.");}
        if(!this.state.Owner.Owns(participant)){throw new InvalidOperationException("Collection belongs to a different transaction database.");}
        if(!this.state.Changes.TryGetValue(participant,out var changes)){
            var reserved=this.state.Context.Domain?.UseParticipant(this.state.Context,participant)??false;
            try{
            if(participant is IVersionedTransactionParticipant versioned){changes=versioned.CreateChanges(this.state.Context);}
            else if(participant is IContextualTransactionParticipant contextual){changes=contextual.CreateChanges(this.state.Context);}
            else{
                if(this.IsReadOnly){throw new NotSupportedException("This participant cannot enforce a read-only transaction.");}
                changes=participant.CreateChanges();
            }
            this.state.Changes.Add(participant,changes);
            }catch{if(reserved){this.state.Context.Domain!.UndoParticipant(this.state.Context,participant);}throw;}
        }
        return changes;
    }
    public ValueTask<TransactionCommitResult> CommitAsync()=>this.CommitAsync(ReadOnlyMemory<byte>.Empty);
    public ValueTask<TransactionCommitResult> CommitAsync(ReadOnlyMemory<byte> metadata)=>this.CommitAsync(metadata,default);
    public ValueTask<TransactionCommitResult> CommitAsync(ReadOnlyMemory<byte> metadata,CancellationToken cancellationToken)=>this.CommitCore(metadata,cancellationToken,null);
    private async ValueTask<TransactionCommitResult> CommitCore(ReadOnlyMemory<byte> metadata,CancellationToken cancellationToken,Action<TransactionInstallationContext>? installed){
        using var access=this.Enter();
        if(this.parent is not null&&!metadata.IsEmpty){throw new InvalidOperationException("Only the outer transaction can publish commit metadata.");}
        if(!metadata.IsEmpty){this.state.Context.EnsureWritable();}
        TransactionCommitResult result=default;Exception? operationError=null;
        try{
            if(this.state.Aborted){if(this.state.Context.Domain is not null){throw new TransactionAbortedException(this.Id,TransactionAbortReason.JoinedRollback);}throw new InvalidOperationException("The joined transaction was aborted.");}
            if(this.parent is not null){cancellationToken.ThrowIfCancellationRequested();this.closed=true;this.state.Current=this.parent;this.metadata?.Dispose();return new(this.Id,TransactionCommitKind.Voted,0);}
            if(!this.IsReadOnly){var previous=this.state.QueryThread;this.state.QueryThread=Environment.CurrentManagedThreadId;try{this.state.Owner.ValidateCommit(this);}finally{this.state.QueryThread=previous;}}
            result=await this.state.Owner.Commit(this.Id,this.state.Changes.Values.Concat(this.state.Extra).ToArray(),metadata,this.IsReadOnly,this.state.Context,cancellationToken,installed:installed).ConfigureAwait(false);return result;
        }catch(Exception error){if(error is TransactionAbortedException||error is OperationCanceledException){this.state.Aborted=true;}operationError=error;throw;}
        finally{
            if(this.parent is null){
                try{this.CloseRoot();}catch(Exception cleanup){
                    if(operationError is TransactionCommitException prior){throw new TransactionCommitException(this.Id,prior.Outcome,new AggregateException(prior,cleanup));}
                    if(operationError is not null){throw new AggregateException(operationError,cleanup);}
                    var outcome=TransactionOutcomeKind.Unknown;if(result.Kind==TransactionCommitKind.Committed){outcome=TransactionOutcomeKind.Committed;}
                    throw new TransactionCommitException(this.Id,outcome,cleanup);
                }
            }
        }
    }
    public ValueTask RollbackAsync(){
        using var access=this.Enter(false);this.state.Aborted=true;
        if(this.parent is not null){this.closed=true;this.state.Current=this.parent;this.metadata?.Dispose();}else{this.CloseRoot();}
        return ValueTask.CompletedTask;
    }
    public ValueTask DisposeAsync(){if(this.closed||Volatile.Read(ref this.state.Retired)!=0){this.closed=true;return ValueTask.CompletedTask;}return this.RollbackAsync();}
    private IDisposable Enter(bool requireAvailable=true,bool allowQuery=false){
        if(Volatile.Read(ref this.state.IndependentMetadata)!=0){throw new InvalidOperationException("Wait for the independent metadata commit before using its parent transaction.");}
        if(requireAvailable&&this.state.Context.IsAborted){this.state.Context.ThrowIfUnavailable();}
        ObjectDisposedException.ThrowIf(this.closed,this);
        if(Interlocked.CompareExchange(ref this.state.Busy,1,0)!=0){
            if(allowQuery&&this.state.QueryThread==Environment.CurrentManagedThreadId){this.state.Context.Domain?.BeginOperation(this.state.Context);return new Access(this.state,true,false);}
            throw new InvalidOperationException("Concurrent operations on a transaction are not supported.");
        }
        if(!ReferenceEquals(this.state.Current,this)){Volatile.Write(ref this.state.Busy,0);throw new InvalidOperationException("Finish the innermost transaction scope first.");}
        try{
            if(requireAvailable){this.state.Context.ThrowIfUnavailable();this.state.Context.Domain?.BeginOperation(this.state.Context);}
            return new Access(this.state,requireAvailable);
        }catch{Volatile.Write(ref this.state.Busy,0);throw;}
    }
    private void RetireReadOnly(){this.state.Aborted=true;this.CloseRoot();}
    private global::Seamless.Execution.ResourceReservation? SavepointMetadata(string name)=>this.state.Context.Domain?.ReserveMetadata(this.state.Context,checked(128L+name.Length*2L+this.state.Changes.Count*512L));
    private void CloseRoot(){
        if(Interlocked.Exchange(ref this.state.Retired,1)!=0){return;}
        this.closed=true;
        var errors=new List<Exception>();
        try{
            foreach(var change in this.state.Changes.Values.Concat(this.state.Extra)){try{change.Dispose();}catch(Exception error){errors.Add(error);}}
            foreach(var point in this.state.Savepoints){try{point.Dispose();}catch(Exception error){errors.Add(error);}}
            this.state.Savepoints.Clear();
            CompletionRegistration[] completions;lock(this.state.Completions){completions=this.state.Completions.ToArray();this.state.Completions.Clear();}
            foreach(var completion in completions){try{completion.Run();}catch(Exception error){errors.Add(error);}}
            var current=this.state.Current;this.state.Current=null;
            while(current is not null){current.closed=true;current.metadata?.Dispose();current=current.parent;}
        }
        finally{
            this.state.Context.Close();
            this.state.Changes.Clear();this.state.Extra.Clear();
            try{if(this.state.Aborted){this.state.Owner.RecordAborted(this.Id);}}finally{this.state.Owner.Release(this.state.Marker);}
        }
        if(errors.Count>0){var error=new AggregateException("Transaction cleanup failed.",errors);this.state.Owner.Failure.Fail(error);throw error;}
    }
    private sealed class State{
        internal State(TransactionCoordinator owner,TransactionCoordinator.Marker marker,TransactionContext context){this.Owner=owner;this.Marker=marker;this.Context=context;}
        public TransactionCoordinator Owner{get;}
        public TransactionCoordinator.Marker Marker{get;}
        public Guid Id=>this.Context.Id;
        public TransactionContext Context{get;}
        public Dictionary<ITransactionParticipant,ITransactionChanges> Changes{get;}=[];
        public List<ITransactionChanges> Extra{get;}=[];
        public List<Savepoint> Savepoints{get;}=[];
        public List<CompletionRegistration> Completions{get;}=[];
        public TransactionScope? Current;
        public int Busy;
        public int QueryThread;
        public int IndependentMetadata;
        public bool Aborted;
        public int Retired;
    }
    private sealed class Savepoint(string name,global::Seamless.Execution.ResourceReservation? memory=null):IDisposable{
        internal string Name{get;}=name;
        internal Dictionary<ITransactionParticipant,ITransactionChanges> Changes{get;}=[];
        internal List<ITransactionChanges> Extra{get;}=[];
        public void Dispose(){
            List<Exception>? errors=null;
            try{foreach(var change in this.Changes.Values.Concat(this.Extra)){try{change.Dispose();}catch(Exception error){errors??=[];errors.Add(error);}}}
            finally{this.Changes.Clear();this.Extra.Clear();memory?.Dispose();}
            if(errors is not null){throw new AggregateException("Savepoint cleanup failed.",errors);}
        }
    }
    private sealed class Access(State state,bool operation,bool ownsBusy=true):IDisposable{
        public void Dispose(){try{if(operation){state.Context.Domain?.EndOperation(state.Context);}}finally{if(ownsBusy){Volatile.Write(ref state.Busy,0);}}}
    }
}
