namespace Seamless.Transactions;

public enum DatabaseLockMode{Read,Write,Schema}
public sealed class TransactionDeadlockException(Guid transactionId):Exception("The transaction was chosen as a deadlock victim."){
    public Guid TransactionId{get;}=transactionId;
}

/// <summary>取引 ID が所有する DB lease。別スレッドでの実行と動的な DB 追加を扱う。</summary>
public sealed class DatabaseLockManager:IDisposable{
    private readonly object gate=new();
    private readonly Dictionary<long,DatabaseState> databases=[];
    private readonly Dictionary<Guid,Request> waiting=[];
    private readonly HashSet<Guid> victims=[];
    private readonly int maximumWaiters;
    private bool disposed;
    public DatabaseLockManager(int maximumWaiters=256){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWaiters);this.maximumWaiters=maximumWaiters;}
    public int WaitingCount{get{lock(this.gate){return this.waiting.Count;}}}

    public Task<IDisposable> AcquireAsync(Guid transactionId,long databaseId,DatabaseLockMode mode,CancellationToken cancellationToken=default){
        if(transactionId==Guid.Empty){throw new ArgumentException("A transaction ID is required.",nameof(transactionId));}
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(databaseId);if(!Enum.IsDefined(mode)){throw new ArgumentOutOfRangeException(nameof(mode));}
        cancellationToken.ThrowIfCancellationRequested();Request request;
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.victims.Contains(transactionId)){return Task.FromException<IDisposable>(new TransactionDeadlockException(transactionId));}
            if(this.waiting.ContainsKey(transactionId)){throw new InvalidOperationException("Concurrent lock requests on one transaction are not supported.");}
            if(!this.databases.TryGetValue(databaseId,out var database)){database=new();this.databases.Add(databaseId,database);}
            if((database.Queue.Count==0||database.Owners.TryGetValue(transactionId,out var own)&&own.Allows(mode))&&Compatible(database,transactionId,mode)){
                return Task.FromResult(this.Grant(database,transactionId,databaseId,mode));
            }
            if(this.waiting.Count>=this.maximumWaiters){throw new InvalidOperationException("The database lock wait queue is full.");}
            request=new(transactionId,databaseId,mode,cancellationToken);request.Node=database.Queue.AddLast(request);this.waiting.Add(transactionId,request);
            if(this.HasCycle(transactionId,transactionId,new HashSet<Guid>())){
                this.victims.Add(transactionId);this.RemoveRequest(request);request.Completion.TrySetException(new TransactionDeadlockException(transactionId));this.Pump(database);
            }
        }
        if(!request.Completion.Task.IsCompleted){
            request.Registration=cancellationToken.UnsafeRegister(static state=>{var item=((DatabaseLockManager Manager,Request Request))state!;item.Manager.Cancel(item.Request);},(this,request));
            if(request.Completion.Task.IsCompleted){request.Registration.Unregister();}
        }
        return request.Completion.Task;
    }

    private IDisposable Grant(DatabaseState database,Guid owner,long databaseId,DatabaseLockMode mode){
        if(!database.Owners.TryGetValue(owner,out var holding)){holding=new();database.Owners.Add(owner,holding);}
        holding.Counts[(int)mode]++;return new Lease(this,owner,databaseId,mode);
    }
    private static bool Compatible(DatabaseState database,Guid owner,DatabaseLockMode requested){
        foreach(var pair in database.Owners){if(pair.Key!=owner&&(requested!=DatabaseLockMode.Read||pair.Value.Counts[1]!=0||pair.Value.Counts[2]!=0)){return false;}}
        return true;
    }
    private bool HasCycle(Guid current,Guid target,HashSet<Guid> visited){
        if(!visited.Add(current)||!this.waiting.TryGetValue(current,out var request)){return false;}
        var database=this.databases[request.DatabaseId];
        foreach(var pair in database.Owners){
            if(pair.Key==current||request.Mode==DatabaseLockMode.Read&&pair.Value.Counts[1]==0&&pair.Value.Counts[2]==0){continue;}
            if(pair.Key==target||this.HasCycle(pair.Key,target,visited)){return true;}
        }
        for(var node=database.Queue.First;node is not null&&node!=request.Node;node=node.Next){if(node.Value.Owner==target||this.HasCycle(node.Value.Owner,target,visited)){return true;}}
        return false;
    }
    private void RemoveRequest(Request request){
        this.waiting.Remove(request.Owner);
        if(request.Node is not null){this.databases[request.DatabaseId].Queue.Remove(request.Node);request.Node=null;}
        request.Registration.Unregister();
    }
    private void Pump(DatabaseState database){
        while(database.Queue.First is{}node){
            var request=node.Value;
            if(request.Token.IsCancellationRequested){this.RemoveRequest(request);request.Completion.TrySetCanceled(request.Token);continue;}
            if(!Compatible(database,request.Owner,request.Mode)){return;}
            this.RemoveRequest(request);request.Completion.TrySetResult(this.Grant(database,request.Owner,request.DatabaseId,request.Mode));
        }
    }
    private void Cancel(Request request){
        lock(this.gate){
            if(request.Node is null){return;}
            var database=this.databases[request.DatabaseId];this.RemoveRequest(request);request.Completion.TrySetCanceled(request.Token);this.Pump(database);
        }
    }
    private void Release(Guid owner,long databaseId,DatabaseLockMode mode){
        lock(this.gate){
            var database=this.databases[databaseId];var holding=database.Owners[owner];holding.Counts[(int)mode]--;
            if(holding.Counts.All(static count=>count==0)){database.Owners.Remove(owner);}
            this.Pump(database);
            if(!this.waiting.ContainsKey(owner)&&!this.databases.Values.Any(state=>state.Owners.ContainsKey(owner))){this.victims.Remove(owner);}
        }
    }
    public void Dispose(){
        lock(this.gate){
            if(this.disposed){return;}this.disposed=true;
            foreach(var request in this.waiting.Values.ToArray()){this.RemoveRequest(request);request.Completion.TrySetException(new ObjectDisposedException(nameof(DatabaseLockManager)));}
        }
    }
    private sealed class DatabaseState{
        internal Dictionary<Guid,Holding> Owners{get;}=[];
        internal LinkedList<Request> Queue{get;}=[];
    }
    private sealed class Holding{
        internal int[] Counts{get;}=new int[3];
        internal bool Allows(DatabaseLockMode mode)=>this.Counts[2]>0||this.Counts[1]>0||this.Counts[(int)mode]>0;
    }
    private sealed class Request(Guid owner,long databaseId,DatabaseLockMode mode,CancellationToken token){
        internal Guid Owner{get;}=owner;
        internal long DatabaseId{get;}=databaseId;
        internal DatabaseLockMode Mode{get;}=mode;
        internal CancellationToken Token{get;}=token;
        internal TaskCompletionSource<IDisposable> Completion{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<Request>? Node;
        internal CancellationTokenRegistration Registration;
    }
    private sealed class Lease(DatabaseLockManager manager,Guid owner,long databaseId,DatabaseLockMode mode):IDisposable{
        private int released;
        public void Dispose(){if(Interlocked.Exchange(ref this.released,1)==0){manager.Release(owner,databaseId,mode);}}
    }
}
