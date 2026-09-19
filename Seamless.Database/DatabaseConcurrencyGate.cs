namespace Seamless.Database;

internal sealed class DatabaseConcurrencyGate{
    private readonly object gate=new();
    private int readers;
    private int waitingWriters;
    private bool writer;

    public IDisposable EnterRead(global::System.Threading.CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        using var registration=cancellationToken.Register(static state=>{
            var owner=(DatabaseConcurrencyGate)state!;
            lock(owner.gate){global::System.Threading.Monitor.PulseAll(owner.gate);}
        },this);
        lock(this.gate){
            while(this.writer||this.waitingWriters!=0){
                cancellationToken.ThrowIfCancellationRequested();
                global::System.Threading.Monitor.Wait(this.gate,50);
            }
            this.readers++;
        }
        return new Lease(this,false);
    }

    public IDisposable EnterWrite(global::System.Threading.CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        using var registration=cancellationToken.Register(static state=>{
            var owner=(DatabaseConcurrencyGate)state!;
            lock(owner.gate){global::System.Threading.Monitor.PulseAll(owner.gate);}
        },this);
        lock(this.gate){
            this.waitingWriters++;
            try{
                while(this.writer||this.readers!=0){
                    cancellationToken.ThrowIfCancellationRequested();
                    global::System.Threading.Monitor.Wait(this.gate,50);
                }
                this.writer=true;
            }finally{
                this.waitingWriters--;
                global::System.Threading.Monitor.PulseAll(this.gate);
            }
        }
        return new Lease(this,true);
    }

    private void Exit(bool write){
        lock(this.gate){
            if(write){
                if(!this.writer){throw new InvalidOperationException("A database write lease was released more than once.");}
                this.writer=false;
            }else{
                if(this.readers<=0){throw new InvalidOperationException("A database read lease was released more than once.");}
                this.readers--;
            }
            global::System.Threading.Monitor.PulseAll(this.gate);
        }
    }

    private sealed class Lease(DatabaseConcurrencyGate owner,bool write):IDisposable{
        private DatabaseConcurrencyGate? owner=owner;
        public void Dispose(){global::System.Threading.Interlocked.Exchange(ref this.owner,null)?.Exit(write);}
    }
}

internal sealed class DatabaseReadSessionState(IDisposable? lease){
    public IDisposable? Lease{get;}=lease;
    public int Depth{get;set;}
}

public sealed class DatabaseReadSession:IDisposable{
    private DatabaseContainer? database;
    private readonly DatabaseReadSessionState state;
    internal DatabaseReadSession(DatabaseContainer database,DatabaseReadSessionState state){
        this.database=database;
        this.state=state;
    }
    public void Dispose(){global::System.Threading.Interlocked.Exchange(ref this.database,null)?.EndReadSession(this.state);}
}

internal sealed class DatabaseSessionEnumerator<T>(IEnumerator<T> inner,DatabaseReadSession session):IEnumerator<T>{
    private IEnumerator<T>? inner=inner;
    private DatabaseReadSession? session=session;
    public T Current=>this.inner is null?throw new ObjectDisposedException(nameof(DatabaseSessionEnumerator<T>)):this.inner.Current;
    object? global::System.Collections.IEnumerator.Current=>this.Current;
    public bool MoveNext()=>this.inner is not null&&this.inner.MoveNext();
    public void Reset(){
        if(this.inner is null){throw new ObjectDisposedException(nameof(DatabaseSessionEnumerator<T>));}
        this.inner.Reset();
    }
    public void Dispose(){
        var iterator=global::System.Threading.Interlocked.Exchange(ref this.inner,null);
        try{iterator?.Dispose();}
        finally{global::System.Threading.Interlocked.Exchange(ref this.session,null)?.Dispose();}
    }
}

public sealed class DatabaseFaultedException(string message):InvalidOperationException(message);
