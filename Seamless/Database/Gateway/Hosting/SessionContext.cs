using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed class SessionContext:IDisposable{
    internal (int DatabaseId,int TableId)? IdentityInsertTarget{get;set;}
    internal decimal? LastIdentity{get;set;}
    internal sealed class IdentityScope{internal decimal? Value{get;set;}}
    internal IdentityScope CurrentIdentityScope{get;private set;}=new();
    internal IDisposable EnterIdentityScope(IdentityScope scope){var previous=this.CurrentIdentityScope;this.CurrentIdentityScope=scope;return new IdentityScopeLease(this,previous);}
    private sealed class IdentityScopeLease(SessionContext session,IdentityScope previous):IDisposable{
        private bool disposed;
        public void Dispose(){if(!this.disposed){session.CurrentIdentityScope=previous;this.disposed=true;}}
    }
    internal int ConstraintStatementDepth{get;set;}
    private IDisposable? retainedResults;
    private IDisposable? currentTransaction;
    private ExecutionContext? transactionContext;
    private readonly object executionGate=new();
    public T Run<T>(Func<T> action){
        lock(this.executionGate){
            T result=default!;var context=this.transactionContext?.CreateCopy()??ExecutionContext.Capture()??throw new InvalidOperationException("Execution context flow is suppressed.");
            ExecutionContext.Run(context,_=>{try{result=action();}finally{if(this.Transaction?.Manager is not null){this.RefreshTransactionContext();}}},null);return result;
        }
    }
    public IEnumerable<object?[]> Enumerate(IEnumerable<object?[]> rows)=>this.Enumerate<object?[]>(rows);
    public IEnumerable<T> Enumerate<T>(IEnumerable<T> rows){
        var context=this.transactionContext?.CreateCopy()??ExecutionContext.Capture()??throw new InvalidOperationException("Execution context flow is suppressed.");IEnumerator<T>? iterator=null;
        TResult Invoke<TResult>(Func<TResult> action){
            lock(this.executionGate){
                TResult result=default!;ExecutionContext? next=null;
                try{ExecutionContext.Run(context.CreateCopy(),_=>{try{result=action();}finally{next=ExecutionContext.Capture();}},null);}
                finally{context=next??throw new InvalidOperationException("Execution context was lost.");if(this.Transaction?.Manager is not null){this.transactionContext=context.CreateCopy();}}
                return result;
            }
        }
        try{
            iterator=Invoke(rows.GetEnumerator);
            while(true){var step=Invoke(()=>{if(iterator.MoveNext()){return(HasValue:true,Value:iterator.Current);}return(HasValue:false,Value:default(T)!);});if(!step.HasValue){break;}yield return step.Value;}
        }
        finally{if(iterator is not null){Invoke(()=>{iterator.Dispose();return 0;});}}
    }

    public SessionContext(long sessionId,string databaseName){
        this.SessionId=sessionId;
        this.DatabaseName=databaseName;
    }

    public long SessionId{get;}
    internal Guid ReadLockOwner{get;}=Guid.NewGuid();
    public string DatabaseName{get;set;}
    public CancellationToken CancellationToken{get;set;}
    internal DateTime? StatementTimestamp{get;private set;}
    internal IDisposable BeginStatement(){var previous=this.StatementTimestamp;this.StatementTimestamp??=DateTime.Now;return new StatementClock(this,previous);}
    private sealed class StatementClock(SessionContext session,DateTime? previous):IDisposable{public void Dispose(){session.StatementTimestamp=previous;}}
    public int LockTimeoutMilliseconds{get;internal set;}=-1;
    public bool XactAbort{get;internal set;}
    internal int TryCatchDepth{get;set;}
    internal bool ServerInformationCleanupAvailable{get;set;}
    internal bool DatabaseOptionRetentionAvailable{get;set;}
    internal bool DatabaseAvailabilityAvailable{get;set;}
    internal int DatabaseAvailabilityCleanupOrdinal{get;set;}
    internal T AcquireLock<T>(Func<CancellationToken,T> acquire){
        if(this.LockTimeoutMilliseconds<0){return acquire(this.CancellationToken);}
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(this.CancellationToken);
        timeout.CancelAfter(this.LockTimeoutMilliseconds);
        try{return acquire(timeout.Token);}
        catch(OperationCanceledException)when(timeout.IsCancellationRequested&&!this.CancellationToken.IsCancellationRequested){throw new SqlQueryException("Lock request time out period exceeded.",1222);}
    }
    public byte LastTransactionOperation{get;set;}=9;
    // 一時表(#名前)。接続ごとに持ち、接続の再利用(sp_reset_connection)で消える。
    internal Dictionary<string,TemporaryTable> TemporaryTables{get;}=new(StringComparer.OrdinalIgnoreCase);
    // 文ごとに実行するバッチの中では、旧来の文字列一致の模擬より共通エンジンを先に使う。
    internal bool PreferQueryEngine{get;set;}
    public void Reset(){this.TemporaryTables.Clear();this.Dispose();this.LastIdentity=null;this.CurrentIdentityScope=new();this.IdentityInsertTarget=null;this.LastTransactionOperation=10;this.LockTimeoutMilliseconds=-1;this.XactAbort=false;this.TryCatchDepth=0;this.ServerInformationCleanupAvailable=false;this.DatabaseOptionRetentionAvailable=false;this.DatabaseAvailabilityAvailable=false;this.DatabaseAvailabilityCleanupOrdinal=0;}
    public bool IsInTransaction=>this.currentTransaction is not null;
    public LogicalTransaction? Transaction{get;private set;}
    internal LogicalTransaction? StatementTransaction;
    internal LogicalTransaction? SnapshotTransaction=>this.Transaction??this.StatementTransaction;
    internal void EndStatementTransaction(LogicalTransaction transaction){if(ReferenceEquals(Interlocked.CompareExchange(ref this.StatementTransaction,null,transaction),transaction)&&this.currentTransaction is null){this.transactionContext=null;}}
    public int TransactionCount=>this.Transaction?.Depth??0;
    public int TransactionState{
        get{
            if(this.currentTransaction is null){return 0;}
            if(this.currentTransaction is LogicalTransaction managed&&managed.IsRollbackOnly){return -1;}
            if(this.currentTransaction is RuntimeSqlCatalog.RuntimeSqlTransaction runtime&&runtime.IsRollbackOnly||this.currentTransaction is DatabaseTransaction typed&&typed.IsRollbackOnly){return -1;}
            return 1;
        }
    }
    internal void BeginNestedTransaction(){
        var transaction=this.Transaction??throw new InvalidOperationException("There is no active transaction.");
        transaction.Depth=checked(transaction.Depth+1);
    }
    internal bool CommitNestedTransaction(){
        if(this.Transaction is not{Depth:>1} transaction){return false;}
        transaction.Depth--;return true;
    }
    internal void CreateSavepoint(string name){
        if(this.TransactionState<0){throw new SqlQueryException("The transaction is rollback-only.",3930);}
        if(this.Transaction?.Manager is{} manager){manager.CreateSavepoint(this,name);}
        else if(this.currentTransaction is RuntimeSqlCatalog.RuntimeSqlTransaction runtime){runtime.CreateSavepoint(name);}
        else if(this.currentTransaction is DatabaseTransaction typed){typed.CreateSavepoint(name);}
        else{throw new SqlQueryException("SAVE TRANSACTION requires an active transaction.",628);}
    }
    internal void ReleaseSavepoint(string name){
        if(this.Transaction?.Manager is{} manager){manager.ReleaseSavepoint(this,name);}
        else if(this.currentTransaction is RuntimeSqlCatalog.RuntimeSqlTransaction runtime){runtime.ReleaseSavepoint(name);}
        else if(this.currentTransaction is DatabaseTransaction typed){typed.ReleaseSavepoint(name);}
    }
    internal void MarkRollbackOnly(){
        if(this.currentTransaction is LogicalTransaction managed){managed.Doomed=true;}
        if(this.currentTransaction is DatabaseTransaction typed){typed.Rollback();}
    }
    internal void RollbackToSavepoint(string name){
        if(this.TransactionState<0){throw new SqlQueryException("The transaction cannot be rolled back to a savepoint; roll back the entire transaction.",3931);}
        try{
            if(this.Transaction?.Manager is{} manager){manager.RollbackToSavepoint(this,name);}
            else if(this.currentTransaction is RuntimeSqlCatalog.RuntimeSqlTransaction runtime){runtime.RollbackToSavepoint(name);}
            else if(this.currentTransaction is DatabaseTransaction typed){typed.RollbackToSavepoint(name);}
            else{throw new SqlQueryException("There is no active transaction.",3903);}
        }catch(KeyNotFoundException){throw new SqlQueryException("Cannot roll back to '"+name+"'. No transaction or savepoint of that name was found.",6401);}
    }

    public void AttachTransaction(IDisposable transaction){
        if(this.currentTransaction is not null){
            throw new InvalidOperationException("Transaction is already active in this session.");
        }else{
        }
        this.currentTransaction=transaction;this.Transaction=new(this.SessionId);this.transactionContext=ExecutionContext.Capture();
    }
    internal void AttachGatewayTransaction(LogicalTransaction transaction){
        if(this.currentTransaction is not null){throw new InvalidOperationException("A transaction is already attached.");}
        this.currentTransaction=transaction;this.Transaction=transaction;this.RefreshTransactionContext();
    }
    internal void RefreshTransactionContext()=>this.transactionContext=ExecutionContext.Capture()??throw new InvalidOperationException("Execution context flow is suppressed.");

    public IDisposable? DetachTransaction(){
        var tx=this.currentTransaction;
        this.currentTransaction=null;this.Transaction=null;this.transactionContext=null;
        return tx;
    }

    internal void RetainResults(IDisposable results){
        if(this.retainedResults is not null){throw new InvalidOperationException("The current request already retains results.");}
        this.retainedResults=results;
    }
    internal void FinishRequest(){
        try{Interlocked.Exchange(ref this.retainedResults,null)?.Dispose();}
        finally{this.FinishReadStatement();}
    }
    private void FinishReadStatement(){
        if(this.StatementTransaction is{} statement){this.Run(()=>{statement.Manager!.EndReadStatement(this,statement);return 0;});}
    }
    public void Dispose(){
        try{this.FinishRequest();}
        finally{if(this.currentTransaction is not null){
            this.Run(()=>{this.currentTransaction.Dispose();return 0;});
            this.currentTransaction=null;this.Transaction=null;this.transactionContext=null;
        }}
    }
}
