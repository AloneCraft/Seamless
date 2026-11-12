namespace Seamless.Transactions;

public sealed partial class TransactionCoordinator{
    /// <summary>親の活動を保持するGateway内部の予約。業務scopeに合流せず、行を変更する能力も渡さない。</summary>
    internal async ValueTask<TransactionCommitResult> CommitIndependentMetadataAsync(TransactionScope parent,Guid id,ReadOnlyMemory<byte> metadata,CancellationToken cancellationToken=default,Action<TransactionInstallationContext>? installed=null){
        ArgumentNullException.ThrowIfNull(parent);if(id==Guid.Empty||id==parent.Id||metadata.IsEmpty){throw new ArgumentException("An independent identity and nonempty reservation metadata are required.");}
        var domain=this.versioned??throw new NotSupportedException("Independent metadata requires a versioned domain.");
        if(this.preparedJournal is null&&this.journal is null){throw new NotSupportedException("An independent Gateway reservation requires its durable common WAL.");}
        var maximum=this.preparedJournal?.MaximumMetadataBytes??checked(this.journal!.Writer.MaximumFrameBytes-64);if(metadata.Length>maximum){throw new ArgumentException("Reservation metadata exceeds the common WAL frame bound.",nameof(metadata));}
        cancellationToken.ThrowIfCancellationRequested();var pin=parent.PinIndependentMetadata(this);using var retained=pin.Lease;
        if((await this.GetOutcomeAsync(id).ConfigureAwait(false)).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidOperationException("The reservation identity has already completed.");}
        var context=new TransactionContext(id,false);domain.StartPinned(context,pin.Context,cancellationToken);
        try{return await this.Commit(id,[],metadata,false,context,cancellationToken,installed:installed).ConfigureAwait(false);}
        catch(TransactionAbortedException){this.RecordAborted(id);throw;}
        catch(OperationCanceledException){this.RecordAborted(id);throw;}
        finally{context.Close();domain.End(context);}
    }
    internal sealed partial class OptimisticDomain{
        internal void StartPinned(TransactionContext context,TransactionContext parent,CancellationToken cancellationToken){
            lock(this.sync){
                ObjectDisposedException.ThrowIf(this.disposed||this.owner.disposed||this.owner.stopping,this.owner);this.owner.failure.ThrowIfFailed();cancellationToken.ThrowIfCancellationRequested();parent.EnsureWritable();
                if(!ReferenceEquals(parent.Domain,this)||!this.active.Contains(parent)||parent.Operations<=0||parent.Sealed){throw new InvalidOperationException("Independent admission requires a retained active writable parent.");}
                if(this.active.Any(active=>active.Id==context.Id)){throw new InvalidOperationException("The reservation identity is already active.");}
                context.Metadata=this.metadataBudget.ReserveMemory(1024);context.Domain=this;context.ReadVersion=this.published;
                try{this.active.Add(context);}catch{context.Metadata.Dispose();context.Metadata=null;context.Domain=null;throw;}
            }
        }
    }
}

public sealed partial class TransactionScope{
    internal (TransactionContext Context,IDisposable Lease) PinIndependentMetadata(TransactionCoordinator owner){
        if(!ReferenceEquals(this.state.Owner,owner)){throw new ArgumentException("The parent scope belongs to another transaction owner.",nameof(owner));}
        var access=this.Enter(allowQuery:true);
        try{
            this.state.Context.EnsureWritable();if(!ReferenceEquals(this.state.Current,this)||this.state.Aborted||this.state.Context.Sealed){throw new InvalidOperationException("Use the innermost active writable parent before it starts completing.");}
            if(Interlocked.CompareExchange(ref this.state.IndependentMetadata,1,0)!=0){throw new InvalidOperationException("Only one independent reservation can retain the parent at a time.");}
            try{return(this.state.Context,new IndependentMetadataPin(this.state,access));}catch{Volatile.Write(ref this.state.IndependentMetadata,0);throw;}
        }catch{access.Dispose();throw;}
    }
    private sealed class IndependentMetadataPin(State state,IDisposable access):IDisposable{
        public void Dispose(){Volatile.Write(ref state.IndependentMetadata,0);access.Dispose();}
    }
}
