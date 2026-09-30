using System.Text.Json;
namespace Seamless.Execution;
public sealed class ExecutionScope:IDisposable,IAsyncDisposable{
    private readonly object gate=new();
    private readonly HashSet<IDisposable> resources=[];
    private readonly ExecutionOptions options;
    private int active;
    private int ownerThread;
    private volatile bool disposed;
    private string? directory;
    private FileStream? ownerLock;
    public ExecutionScope(ExecutionOptions? options=null){
        this.options=options??new();
        ArgumentException.ThrowIfNullOrWhiteSpace(this.options.TemporaryDirectory);
        this.Budget=new(this.options.MemoryLimitBytes,this.options.TemporaryStorageLimitBytes);
        this.Codecs=new();
    }
    public ResourceBudget Budget{get;}
    public CodecRegistry Codecs{get;}
    public CancellationToken CancellationToken=>this.options.CancellationToken;
    public void ThrowIfUnavailable(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        this.CancellationToken.ThrowIfCancellationRequested();
    }
    public IDisposable Enter(){
        this.ThrowIfUnavailable();
        if(Interlocked.CompareExchange(ref this.active,1,0)!=0){throw new InvalidOperationException("Concurrent execution in one scope is not supported. Use a separate scope.");}
        try{this.ownerThread=Environment.CurrentManagedThreadId;this.ThrowIfUnavailable();return new Operation(this);}
        catch{Interlocked.Exchange(ref this.active,0);throw;}
    }
    public IDisposable EnterDependent(){
        this.ThrowIfUnavailable();
        if(Volatile.Read(ref this.active)==0){return this.Enter();}
        if(this.ownerThread!=Environment.CurrentManagedThreadId){throw new InvalidOperationException("Concurrent execution in one scope is not supported.");}
        Interlocked.Increment(ref this.active);
        return new Operation(this);
    }
    public OwnedBuffer Rent(int length){
        this.ThrowIfUnavailable();
        return new(this,length);
    }
    public OwnedBuffer Encode<T>(T value){
        var codec=this.Codecs.Get<T>();
        var buffer=this.Rent(codec.GetEncodedLength(value));
        try{codec.Encode(value,buffer.Memory.Span);return buffer;}
        catch{buffer.Dispose();throw;}
    }
    public TemporaryFile CreateTemporaryFile(){
        lock(this.gate){
            this.ThrowIfUnavailable();
            if(this.directory is null){
                this.directory=Path.Combine(Path.GetFullPath(this.options.TemporaryDirectory),"scope-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(this.directory);
                this.ownerLock=new FileStream(Path.Combine(this.directory,"owner.lock"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
                File.WriteAllText(Path.Combine(this.directory,"owner.json"),JsonSerializer.Serialize(new{Format="seamless-execution-v1",Id=Path.GetFileName(this.directory),ProcessId=Environment.ProcessId,CreatedAt=DateTimeOffset.UtcNow}));
            }
            var file=new TemporaryFile(this,Path.Combine(this.directory,Guid.NewGuid().ToString("N")+".tmp"));
            this.resources.Add(file);
            return file;
        }
    }
    public void Own(IDisposable resource){
        ArgumentNullException.ThrowIfNull(resource);
        lock(this.gate){this.ThrowIfUnavailable();this.resources.Add(resource);}
    }
    public void Release(IDisposable resource){lock(this.gate){this.resources.Remove(resource);}}
    public void Dispose(){
        lock(this.gate){
            if(this.disposed){return;}
            this.disposed=true;
            List<Exception>? errors=null;
            while(this.resources.Count>0){
                var resource=this.resources.First();this.resources.Remove(resource);
                try{resource.Dispose();}catch(Exception error){(errors??=[]).Add(error);}
            }
            try{this.ownerLock?.Dispose();}catch(Exception error){(errors??=[]).Add(error);}
            try{if(this.directory is not null){OwnedDirectory.Delete(this.directory);}}catch(Exception error){(errors??=[]).Add(error);}
            if(errors is not null){throw new AggregateException("Execution scope cleanup failed.",errors);}
        }
    }
    public ValueTask DisposeAsync(){this.Dispose();return ValueTask.CompletedTask;}
    private sealed class Operation(ExecutionScope scope):IDisposable{
        private ExecutionScope? scope=scope;
        public void Dispose(){
            var owner=Interlocked.Exchange(ref this.scope,null);
            if(owner is not null){Interlocked.Decrement(ref owner.active);}
        }
    }
}
