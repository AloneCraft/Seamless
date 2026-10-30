using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed class SessionContext:IDisposable{
    private IDisposable? currentTransaction;
    private ExecutionContext? transactionContext;
    public T Run<T>(Func<T> action){T result=default!;var context=this.transactionContext?.CreateCopy()??ExecutionContext.Capture()??throw new InvalidOperationException("Execution context flow is suppressed.");ExecutionContext.Run(context,_=>result=action(),null);return result;}
    public IEnumerable<object?[]> Enumerate(IEnumerable<object?[]> rows){
        var context=this.transactionContext?.CreateCopy()??ExecutionContext.Capture()??throw new InvalidOperationException("Execution context flow is suppressed.");IEnumerator<object?[]>? iterator=null;
        T Invoke<T>(Func<T> action){T result=default!;ExecutionContext? next=null;ExecutionContext.Run(context.CreateCopy(),_=>{try{result=action();}finally{next=ExecutionContext.Capture();}},null);context=next??throw new InvalidOperationException("Execution context was lost.");return result;}
        try{iterator=Invoke(rows.GetEnumerator);while(Invoke(iterator.MoveNext)){yield return iterator.Current;}}
        finally{if(iterator is not null){Invoke(()=>{iterator.Dispose();return 0;});}}
    }

    public SessionContext(long sessionId,string databaseName){
        this.SessionId=sessionId;
        this.DatabaseName=databaseName;
    }

    public long SessionId{get;}
    public string DatabaseName{get;set;}
    public CancellationToken CancellationToken{get;set;}
    public int LockTimeoutMilliseconds{get;internal set;}=-1;
    internal bool ServerInformationCleanupAvailable{get;set;}
    internal T AcquireLock<T>(Func<CancellationToken,T> acquire){
        if(this.LockTimeoutMilliseconds<0){return acquire(this.CancellationToken);}
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(this.CancellationToken);
        timeout.CancelAfter(this.LockTimeoutMilliseconds);
        try{return acquire(timeout.Token);}
        catch(OperationCanceledException)when(timeout.IsCancellationRequested&&!this.CancellationToken.IsCancellationRequested){throw new SqlQueryException("Lock request time out period exceeded.",1222);}
    }
    public byte LastTransactionOperation{get;set;}=9;
    public void Reset(){this.Dispose();this.LastTransactionOperation=10;this.LockTimeoutMilliseconds=-1;this.ServerInformationCleanupAvailable=false;}
    public bool IsInTransaction=>this.currentTransaction is not null;

    public void AttachTransaction(IDisposable transaction){
        if(this.currentTransaction is not null){
            throw new InvalidOperationException("Transaction is already active in this session.");
        }else{
        }
        this.currentTransaction=transaction;this.transactionContext=ExecutionContext.Capture();
    }

    public IDisposable? DetachTransaction(){
        var tx=this.currentTransaction;
        this.currentTransaction=null;this.transactionContext=null;
        return tx;
    }

    public void Dispose(){
        if(this.currentTransaction is not null){
            this.Run(()=>{this.currentTransaction.Dispose();return 0;});
            this.currentTransaction=null;this.transactionContext=null;
        }else{
        }
    }
}
