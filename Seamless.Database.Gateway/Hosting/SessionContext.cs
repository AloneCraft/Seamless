namespace Seamless.Database.Gateway.Hosting;

public sealed class SessionContext:IDisposable{
    private IDisposable? currentTransaction;

    public SessionContext(long sessionId,string databaseName){
        this.SessionId=sessionId;
        this.DatabaseName=databaseName;
    }

    public long SessionId{get;}
    public string DatabaseName{get;set;}
    public bool IsInTransaction=>this.currentTransaction is not null;

    public void AttachTransaction(IDisposable transaction){
        if(this.currentTransaction is not null){
            throw new InvalidOperationException("Transaction is already active in this session.");
        }else{
        }
        this.currentTransaction=transaction;
    }

    public IDisposable? DetachTransaction(){
        var tx=this.currentTransaction;
        this.currentTransaction=null;
        return tx;
    }

    public void Dispose(){
        if(this.currentTransaction is not null){
            this.currentTransaction.Dispose();
            this.currentTransaction=null;
        }else{
        }
    }
}
