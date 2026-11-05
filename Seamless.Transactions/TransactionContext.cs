namespace Seamless.Transactions;

/// <summary>参加先が共有する取引の区分と寿命。書込み拒否は変更を記録する前に検査する。</summary>
public sealed partial class TransactionContext{
    private int closed;
    private int frozen;
    private int abortReason;
    internal TransactionContext(Guid id,bool readOnly){this.Id=id;this.IsReadOnly=readOnly;}
    public Guid Id{get;}
    public bool IsReadOnly{get;}
    public long ReadVersion{get;internal set;}
    internal bool IsAborted=>Volatile.Read(ref this.abortReason)!=0;
    internal bool IsClosed=>Volatile.Read(ref this.closed)!=0;
    public void ThrowIfUnavailable(){
        var reason=Volatile.Read(ref this.abortReason);
        if(reason!=0){throw new TransactionAbortedException(this.Id,(TransactionAbortReason)reason);}
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.closed)!=0,this);
        this.Domain?.ThrowIfFailed();
    }
    public void EnsureWritable(){
        this.ThrowIfUnavailable();
        if(this.IsReadOnly){throw new InvalidOperationException("The transaction is read-only.");}
        if(Volatile.Read(ref this.frozen)!=0){throw new InvalidOperationException("The transaction changes are frozen for commit.");}
    }
    internal void Freeze(){this.ThrowIfUnavailable();Interlocked.Exchange(ref this.frozen,1);}
    internal void Abort(TransactionAbortReason reason)=>Interlocked.CompareExchange(ref this.abortReason,(int)reason,0);
    internal void Close()=>Interlocked.Exchange(ref this.closed,1);
}

/// <summary>読取り専用区分と寿命を参加先の操作に引き継ぐ能力。</summary>
public interface IContextualTransactionParticipant:ITransactionParticipant{
    ITransactionChanges CreateChanges(TransactionContext context);
}
