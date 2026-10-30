namespace Seamless.Transactions;
public interface ITransactionChanges:IDisposable{
    IReadOnlyList<WalEntry> Encode();
    IReadOnlyList<WalEntry> EncodeBounded(int maximumBytes)=>this.Encode();
    void Apply();
}
public interface ITransactionParticipant{
    int Partition{get;}
    int TableId{get;}
    string CodecIdentity{get;}
    ITransactionChanges CreateChanges();
    void Replay(WalEntry entry);
}
public enum TransactionCommitKind{Voted,NoChanges,Committed}
public readonly record struct TransactionCommitResult(Guid Id,TransactionCommitKind Kind,long Lsn);
public sealed class TransactionCommitException(Guid id,TransactionOutcomeKind outcome,Exception inner):IOException("Transaction completion failed; inspect Outcome before retrying.",inner){
    public Guid TransactionId{get;}=id;
    public TransactionOutcomeKind Outcome{get;}=outcome;
}
public sealed class TransactionCoordinator:IAsyncDisposable{
    private readonly object initialization=new();
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly AsyncLocal<Marker?> ambient=new();
    private readonly Dictionary<(int,int),ITransactionParticipant> participants=[];
    private readonly TransactionJournal? journal;
    private readonly TransactionFailure failure;
    private bool started;
    private bool disposed;
    private volatile bool stopping;
    private Guid lastAborted;
    public TransactionCoordinator(TransactionJournal? journal=null,TransactionFailure? failure=null){this.journal=journal;this.failure=failure??journal?.Writer.Failure??new();}
    public bool HasCurrentTransaction=>this.ambient.Value?.Active==true;
    public long AppliedLsn{get;private set;}
    public TransactionFailure Failure=>this.failure;
    public void Register(ITransactionParticipant participant){
        if(this.started){throw new InvalidOperationException("Register the complete typed schema before starting transactions.");}
        if(!this.participants.TryAdd((participant.Partition,participant.TableId),participant)){throw new InvalidOperationException("Duplicate transaction participant.");}
    }
    public void Recover(long checkpointLsn=0){
        lock(this.initialization){this.RecoverCore(checkpointLsn);}
    }
    private void RecoverCore(long checkpointLsn){
        if(this.started){return;}
        this.failure.ThrowIfFailed();
        try{
            if(this.journal is not null){
                if(checkpointLsn>this.journal.Device.RecoveredLsn||checkpointLsn<this.journal.Device.FirstAvailableLsn-1){throw new InvalidDataException("Checkpoint is outside retained WAL coverage.");}
                foreach(var record in this.journal.ReadTransactions()){
                    if(record.Lsn<=checkpointLsn){continue;}
                    foreach(var entry in record.Draft.Entries){
                        if(!this.participants.TryGetValue((entry.Partition,entry.TableId),out var participant)||participant.CodecIdentity!=entry.Codec){throw new InvalidDataException("WAL table/schema identity mismatch.");}
                    }
                    foreach(var entry in record.Draft.Entries){this.participants[(entry.Partition,entry.TableId)].Replay(entry);}
                    this.AppliedLsn=record.Lsn;
                }
            }
            this.AppliedLsn=Math.Max(this.AppliedLsn,checkpointLsn);this.started=true;
        }catch(Exception error){this.failure.Fail(error);throw;}
    }
    public ValueTask<TransactionScope> BeginAsync(CancellationToken cancellationToken=default){
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Start a joined child through the existing transaction scope.");}
        ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();
        if(!this.started){this.Recover();}
        // 非asyncの入口でholderを設定し、await先でAsyncLocalを書き戻すことに依存しない。
        var marker=new Marker();this.ambient.Value=marker;return this.Acquire(marker,cancellationToken);
    }
    private async ValueTask<TransactionScope> Acquire(Marker marker,CancellationToken cancellationToken){
        var acquired=false;
        try{
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);acquired=true;ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();
            return new TransactionScope(this,marker);
        }catch{marker.Active=false;if(acquired){this.gate.Release();}throw;}
    }
    public IDisposable EnterRead(CancellationToken cancellationToken=default){
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Use the explicit transaction collection view.");}
        ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);
        this.failure.ThrowIfFailed();this.gate.Wait(cancellationToken);
        try{ObjectDisposedException.ThrowIf(this.disposed||this.stopping,this);this.failure.ThrowIfFailed();return new ReadLease(this.gate);}catch{this.gate.Release();throw;}
    }
    internal void Release(Marker marker){marker.Active=false;this.gate.Release();}
    public void StopAccepting()=>this.stopping=true;
    public IDisposable EnterReadForShutdown(){this.gate.Wait();return new ReadLease(this.gate);}
    internal bool Owns(ITransactionParticipant participant)=>this.participants.TryGetValue((participant.Partition,participant.TableId),out var actual)&&ReferenceEquals(participant,actual);
    internal async ValueTask<TransactionCommitResult> Commit(Guid id,IReadOnlyList<ITransactionChanges> changes){
        long lsn=0;var durable=false;
        try{
            this.failure.ThrowIfFailed();var entries=new List<WalEntry>();var parts=new SortedSet<int>();long bytes=60;
            foreach(var change in changes){
                IReadOnlyList<WalEntry> encoded;
                if(this.journal is not null){encoded=change.EncodeBounded(checked((int)(this.journal.Writer.MaximumFrameBytes-bytes)));}else{encoded=change.Encode();}
                foreach(var entry in encoded){
                    bytes=checked(bytes+WalFormat.EntryPayloadBytes(entry.Codec,entry.Key.Length,entry.Row?.Length??0));if(parts.Add(entry.Partition)){bytes=checked(bytes+4);}
                    if(this.journal is not null&&bytes>this.journal.Writer.MaximumFrameBytes){throw new InvalidDataException("Transaction exceeds the WAL frame bound.");}
                    entries.Add(entry);
                }
            }
            if(entries.Count==0){return new(id,TransactionCommitKind.NoChanges,this.AppliedLsn);}
            if(this.journal is not null){
                var receipt=await this.journal.AppendAsync(new(id,parts.ToArray(),entries,[])).ConfigureAwait(false);
                lsn=receipt.Lsn;await receipt.Durable.ConfigureAwait(false);durable=true;
            }
            foreach(var change in changes){change.Apply();}
            this.AppliedLsn=Math.Max(this.AppliedLsn,lsn);this.failure.ThrowIfFailed();
            return new(id,TransactionCommitKind.Committed,lsn);
        }catch(Exception error){
            this.failure.Fail(error);this.journal?.Writer.Fail(error);
            var outcome=TransactionOutcomeKind.Unknown;if(durable){outcome=TransactionOutcomeKind.Committed;}
            throw new TransactionCommitException(id,outcome,error);
        }
    }
    internal void RecordAborted(Guid id){if(this.journal is not null){this.journal.RecordAborted(id);}else{lock(this.initialization){this.lastAborted=id;}}}
    public ValueTask<TransactionOutcome> GetOutcomeAsync(Guid id){
        if(this.journal is not null){return ValueTask.FromResult(this.journal.GetOutcome(id));}
        lock(this.initialization){var kind=TransactionOutcomeKind.Unknown;if(id!=Guid.Empty&&id==this.lastAborted){kind=TransactionOutcomeKind.Aborted;}return ValueTask.FromResult(new TransactionOutcome(id,kind,0));}
    }
    public ValueTask DisposeAsync(){
        if(this.HasCurrentTransaction||!this.gate.Wait(0)){throw new InvalidOperationException("Finish active transactions before disposing the coordinator.");}
        this.disposed=true;this.gate.Release();this.gate.Dispose();return ValueTask.CompletedTask;
    }
    internal sealed class Marker{public volatile bool Active=true;}
    private sealed class ReadLease(SemaphoreSlim gate):IDisposable{
        private SemaphoreSlim? owner=gate;
        public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.Release();
    }
}
public sealed class TransactionScope:IAsyncDisposable{
    private readonly State state;
    private readonly TransactionScope? parent;
    private bool closed;
    internal TransactionScope(TransactionCoordinator owner,TransactionCoordinator.Marker marker){this.state=new(owner,marker);this.state.Current=this;}
    private TransactionScope(State state,TransactionScope parent){this.state=state;this.parent=parent;}
    public Guid Id=>this.state.Id;
    public TransactionScope BeginNested(){
        using var access=this.Enter();var child=new TransactionScope(this.state,this);this.state.Current=child;return child;
    }
    public void Enlist(ITransactionChanges changes){using var access=this.Enter();this.state.Extra.Add(changes);}
    public T Use<T,TResult>(ITransactionParticipant participant,Func<TResult,T> action) where TResult:class,ITransactionChanges{
        using var access=this.Enter();this.state.Owner.Failure.ThrowIfFailed();
        if(this.state.Aborted){throw new InvalidOperationException("The joined transaction was aborted.");}
        if(!this.state.Owner.Owns(participant)){throw new InvalidOperationException("Collection belongs to a different transaction database.");}
        if(!this.state.Changes.TryGetValue(participant,out var changes)){changes=participant.CreateChanges();this.state.Changes.Add(participant,changes);}
        return action((TResult)changes);
    }
    public async ValueTask<TransactionCommitResult> CommitAsync(){
        using var access=this.Enter();
        TransactionCommitResult result=default;Exception? operationError=null;
        try{
            if(this.state.Aborted){throw new InvalidOperationException("The joined transaction was aborted.");}
            if(this.parent is not null){this.closed=true;this.state.Current=this.parent;return new(this.Id,TransactionCommitKind.Voted,0);}
            result=await this.state.Owner.Commit(this.Id,this.state.Changes.Values.Concat(this.state.Extra).ToArray()).ConfigureAwait(false);return result;
        }catch(Exception error){operationError=error;throw;}
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
        using var access=this.Enter();this.state.Aborted=true;
        if(this.parent is not null){this.closed=true;this.state.Current=this.parent;}else{this.CloseRoot();}
        return ValueTask.CompletedTask;
    }
    public ValueTask DisposeAsync(){if(this.closed){return ValueTask.CompletedTask;}return this.RollbackAsync();}
    private IDisposable Enter(){
        ObjectDisposedException.ThrowIf(this.closed,this);
        if(Interlocked.CompareExchange(ref this.state.Busy,1,0)!=0){throw new InvalidOperationException("Concurrent operations on a transaction are not supported.");}
        if(!ReferenceEquals(this.state.Current,this)){Volatile.Write(ref this.state.Busy,0);throw new InvalidOperationException("Finish the innermost transaction scope first.");}
        return new Access(this.state);
    }
    private void CloseRoot(){
        this.closed=true;
        var errors=new List<Exception>();
        try{foreach(var change in this.state.Changes.Values.Concat(this.state.Extra)){try{change.Dispose();}catch(Exception error){errors.Add(error);}}}
        finally{
            this.state.Changes.Clear();this.state.Extra.Clear();
            try{if(this.state.Aborted){this.state.Owner.RecordAborted(this.Id);}}finally{this.state.Owner.Release(this.state.Marker);}
        }
        if(errors.Count>0){var error=new AggregateException("Transaction cleanup failed.",errors);this.state.Owner.Failure.Fail(error);throw error;}
    }
    private sealed class State(TransactionCoordinator owner,TransactionCoordinator.Marker marker){
        public TransactionCoordinator Owner{get;}=owner;
        public TransactionCoordinator.Marker Marker{get;}=marker;
        public Guid Id{get;}=Guid.NewGuid();
        public Dictionary<ITransactionParticipant,ITransactionChanges> Changes{get;}=[];
        public List<ITransactionChanges> Extra{get;}=[];
        public TransactionScope? Current;
        public int Busy;
        public bool Aborted;
    }
    private sealed class Access(State state):IDisposable{public void Dispose()=>Volatile.Write(ref state.Busy,0);}
}
