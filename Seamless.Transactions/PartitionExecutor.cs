using System.Threading.Channels;
namespace Seamless.Transactions;
public sealed record PartitionExecution<T>(T Result,WalDraft? Draft,IDisposable? Reservation=null);
public readonly record struct PartitionResult<T>(T Result,long FenceLsn);
/// <summary>最小区画へのmailboxと昇順所有。同期の本体と非同期のdurable応答を分離する。</summary>
public sealed class PartitionExecutor:IAsyncDisposable{
    private readonly SemaphoreSlim[] gates;
    private readonly Channel<Action>[] mailboxes;
    private readonly Thread[] owners;
    private readonly long[] applied;
    private readonly WalWriter writer;
    private readonly Func<WalDraft,ValueTask<WalReceipt>> append;
    private readonly object pendingGate=new();
    private readonly HashSet<Task> pending=[];
    private readonly CancellationTokenSource failed=new();
    private int stopping;
    public PartitionExecutor(WalWriter writer,IReadOnlyList<long> appliedLsns,Func<WalDraft,ValueTask<WalReceipt>>? append=null){
        if(appliedLsns.Count==0){throw new ArgumentException("At least one partition is required.",nameof(appliedLsns));}
        this.writer=writer;this.applied=appliedLsns.ToArray();
        this.append=append??(draft=>writer.AppendAsync(draft));
        this.gates=Enumerable.Range(0,this.applied.Length).Select(static _=>new SemaphoreSlim(1,1)).ToArray();
        this.mailboxes=new Channel<Action>[this.applied.Length];this.owners=new Thread[this.applied.Length];
        for(var index=0;index<this.applied.Length;index++){
            var mailbox=Channel.CreateBounded<Action>(new BoundedChannelOptions(256){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});this.mailboxes[index]=mailbox;
            this.owners[index]=new Thread(()=>{
                while(mailbox.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult()){while(mailbox.Reader.TryRead(out var action)){action();}}
            }){IsBackground=true,Name="seamless-partition-"+index};this.owners[index].Start();
        }
    }
    public long AppliedLsn(int partition)=>Interlocked.Read(ref this.applied[partition]);
    public async Task<PartitionResult<T>> ExecuteAsync<T>(int[] partitions,Func<PartitionExecution<T>> execute,CancellationToken cancellationToken=default){
        var parts=partitions.Distinct().Order().ToArray();
        if(parts.Length==0||parts[0]<0||parts[^1]>=this.gates.Length){throw new ArgumentOutOfRangeException(nameof(partitions));}
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.stopping)!=0,this);this.writer.Failure.ThrowIfFailed();
        var completion=new TaskCompletionSource<PartitionResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(this.pendingGate){this.pending.Add(completion.Task);}
        try{
            await this.mailboxes[parts[0]].Writer.WriteAsync(()=>this.Run(parts,execute,completion,cancellationToken),cancellationToken).ConfigureAwait(false);
            return await completion.Task.ConfigureAwait(false);
        }finally{lock(this.pendingGate){this.pending.Remove(completion.Task);}}
    }
    private void Run<T>(int[] parts,Func<PartitionExecution<T>> execute,TaskCompletionSource<PartitionResult<T>> completion,CancellationToken cancellationToken){
        var acquired=0;var began=false;
        try{
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.failed.Token);
            foreach(var part in parts){this.gates[part].Wait(cancellation.Token);acquired++;}
            cancellation.Token.ThrowIfCancellationRequested();this.writer.Failure.ThrowIfFailed();began=true;
            var execution=execute();using var reservation=execution.Reservation;this.writer.Failure.ThrowIfFailed();long fence=0;
            if(execution.Draft is not null){
                if(!execution.Draft.Partitions.SequenceEqual(parts)){throw new InvalidDataException("Draft partition set differs from owned partitions.");}
                var receipt=this.append(execution.Draft).AsTask().GetAwaiter().GetResult();fence=receipt.Lsn;
                foreach(var part in parts){Interlocked.Exchange(ref this.applied[part],fence);}
            }else{foreach(var part in parts){fence=Math.Max(fence,this.AppliedLsn(part));}}
            for(var index=acquired-1;index>=0;index--){this.gates[parts[index]].Release();}acquired=0;
            _=this.Complete(execution.Result,fence,completion);
        }catch(OperationCanceledException) when(!began&&cancellationToken.IsCancellationRequested){completion.TrySetCanceled(cancellationToken);}
        catch(Exception error){this.writer.Fail(error);this.failed.Cancel();completion.TrySetException(error);}
        finally{for(var index=acquired-1;index>=0;index--){this.gates[parts[index]].Release();}}
    }
    private async Task Complete<T>(T result,long fence,TaskCompletionSource<PartitionResult<T>> completion){
        try{await this.writer.WaitForDurableAsync(fence).ConfigureAwait(false);this.writer.Failure.Permit(()=>completion.TrySetResult(new(result,fence)));}
        catch(Exception error){this.failed.Cancel();completion.TrySetException(error);}
    }
    public async Task CheckpointAsync(int partition,Action<long> save,CancellationToken cancellationToken=default){
        if(partition<0||partition>=this.gates.Length){throw new ArgumentOutOfRangeException(nameof(partition));}
        var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var acquired=false;
        lock(this.pendingGate){ObjectDisposedException.ThrowIf(Volatile.Read(ref this.stopping)!=0,this);this.pending.Add(finished.Task);}
        try{
            await this.gates[partition].WaitAsync(cancellationToken).ConfigureAwait(false);acquired=true;
            this.writer.Failure.ThrowIfFailed();var target=this.AppliedLsn(partition);
            await this.writer.WaitForDurableAsync(target).ConfigureAwait(false);this.writer.Failure.ThrowIfFailed();save(target);this.writer.Failure.ThrowIfFailed();
        }catch(OperationCanceledException) when(!acquired&&cancellationToken.IsCancellationRequested){throw;}
        catch(Exception error){this.writer.Fail(error);this.failed.Cancel();throw;}
        finally{
            if(acquired){this.gates[partition].Release();}
            lock(this.pendingGate){this.pending.Remove(finished.Task);finished.TrySetResult();}
        }
    }
    public async ValueTask DisposeAsync(){
        if(Interlocked.Exchange(ref this.stopping,1)!=0){return;}
        if(this.owners.Contains(Thread.CurrentThread)){throw new InvalidOperationException("An owner cannot join itself.");}
        foreach(var mailbox in this.mailboxes){mailbox.Writer.TryComplete();}
        foreach(var owner in this.owners){owner.Join();}
        Task[] remaining;lock(this.pendingGate){remaining=this.pending.ToArray();}
        try{await Task.WhenAll(remaining).ConfigureAwait(false);}catch{this.writer.Failure.ThrowIfFailed();throw;}
        finally{foreach(var gate in this.gates){gate.Dispose();}this.failed.Dispose();}
    }
}
