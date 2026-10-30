using Seamless.Execution;
namespace Seamless.Transactions;
public sealed class WalWriter:IAsyncDisposable,IDisposable{
    private readonly object gate=new();
    private readonly Queue<Pending> queue=[];
    private readonly List<Pending> inFlight=[];
    private readonly IWalDevice device;
    private readonly WalOptions options;
    private readonly ResourceBudget budget;
    private readonly Thread pump;
    private readonly TaskCompletionSource stopped=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource space=Signal();
    private TaskCompletionSource progress=Signal();
    private long accepted;
    private long durable;
    private long queuedBytes;
    private bool stopping;
    private bool disposed;
    public TransactionFailure Failure{get;}
    public WalWriter(IWalDevice device,WalOptions? options=null,ResourceBudget? budget=null,TransactionFailure? failure=null,long startLsn=0){
        this.device=device;this.options=options??new();this.options.Validate();
        this.budget=budget??new ResourceBudget(128L<<20,1L<<30);this.Failure=failure??new();this.accepted=startLsn;this.durable=startLsn;
        this.pump=new Thread(this.Pump){IsBackground=true,Name="seamless-wal-sync"};this.pump.Start();
    }
    public long DurableLsn=>Interlocked.Read(ref this.durable);
    public int MaximumFrameBytes=>this.options.MaxFrameBytes;
    public long LastAcceptedLsn{get{lock(this.gate){return this.accepted;}}}
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask<WalReceipt> AppendAsync(WalDraft draft,CancellationToken cancellationToken=default){
        // 受付前のdraftを有界encodeし、呼出側の配列を受付後に参照しない。
        while(true){
            cancellationToken.ThrowIfCancellationRequested();Task wait;
            lock(this.gate){
                this.Failure.ThrowIfFailed();ObjectDisposedException.ThrowIf(this.stopping,this);
                if(this.queue.Count<this.options.QueueCapacity){
                    var lsn=checked(this.accepted+1);
                    byte[] frame;
                    try{frame=WalFormat.Encode(new(lsn,draft),this.options.MaxFrameBytes);}catch(Exception error){this.Fail(error);throw;}
                    if(frame.Length<=this.options.QueueBytes-this.queuedBytes){
                        var reservation=this.budget.ReserveMemory(frame.Length);
                        var pending=new Pending(lsn,frame,reservation);
                        this.queue.Enqueue(pending);this.queuedBytes+=frame.Length;this.accepted=lsn;
                        Monitor.PulseAll(this.gate);return new(lsn,pending.Completion.Task);
                    }
                }
                wait=this.space.Task;
            }
            await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    public async Task WaitForDurableAsync(long lsn){
        while(true){
            Task wait;
            lock(this.gate){
                this.Failure.ThrowIfFailed();
                if(lsn<0||lsn>this.accepted){throw new ArgumentOutOfRangeException(nameof(lsn));}
                if(this.durable>=lsn){return;}
                wait=this.progress.Task;
            }
            await wait.ConfigureAwait(false);
        }
    }
    private void Pump(){
        var batch=this.inFlight;
        try{
            while(true){
                lock(this.gate){
                    while(this.queue.Count==0&&!this.stopping&&this.Failure.Error is null){Monitor.Wait(this.gate);}
                    this.Failure.ThrowIfFailed();
                    if(this.queue.Count==0&&this.stopping){break;}
                    while(batch.Count<this.options.MaxBatch&&this.queue.TryDequeue(out var pending)){batch.Add(pending);this.queuedBytes-=pending.Frame.Length;}
                    this.space.TrySetResult();this.space=Signal();
                }
                foreach(var pending in batch){this.device.Write(pending.Frame);}
                this.device.Sync();
                lock(this.gate){
                    this.Failure.ThrowIfFailed();Interlocked.Exchange(ref this.durable,batch[^1].Lsn);
                    foreach(var pending in batch){pending.Completion.TrySetResult();pending.Reservation.Dispose();}
                    batch.Clear();this.progress.TrySetResult();this.progress=Signal();
                }
            }
        }catch(Exception error){
            this.Failure.Fail(error);
            lock(this.gate){
                foreach(var pending in batch){pending.Completion.TrySetException(error);pending.Reservation.Dispose();}
                while(this.queue.TryDequeue(out var pending)){pending.Completion.TrySetException(error);pending.Reservation.Dispose();}
                this.queuedBytes=0;this.space.TrySetResult();this.progress.TrySetResult();
            }
        }finally{this.stopped.TrySetResult();}
    }
    public void Fail(Exception error){
        this.Failure.Fail(error);
        lock(this.gate){
            foreach(var pending in this.inFlight){pending.Completion.TrySetException(error);}
            foreach(var pending in this.queue){pending.Completion.TrySetException(error);}
            this.space.TrySetResult();this.progress.TrySetResult();Monitor.PulseAll(this.gate);
        }
    }
    public async ValueTask StopAsync(){
        lock(this.gate){this.stopping=true;this.space.TrySetResult();Monitor.PulseAll(this.gate);}
        await this.stopped.Task.ConfigureAwait(false);this.pump.Join();
    }
    public async ValueTask DisposeAsync(){
        await this.StopAsync().ConfigureAwait(false);
        lock(this.gate){if(this.disposed){return;}this.disposed=true;}
        this.device.Dispose();
    }
    public void Dispose()=>this.DisposeAsync().AsTask().GetAwaiter().GetResult();
    private sealed class Pending(long lsn,byte[] frame,ResourceReservation reservation){
        public long Lsn{get;}=lsn;
        public byte[] Frame{get;}=frame;
        public ResourceReservation Reservation{get;}=reservation;
        public TaskCompletionSource Completion{get;}=Signal();
    }
}
