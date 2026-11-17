using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Execution;

public readonly record struct CpuSchedulerUsage(int Capacity,int Active,int Waiting,int Outstanding,int PeakActive,long CompletedChunks);

/// <summary>ホスト内の実行可能なCPU作業へ共通枠を配る。query/domainごとに上限を重複確保しない。</summary>
public sealed class CpuScheduler{
    [ThreadStatic]private static Lease? current;
    private readonly object gate=new();
    private SemaphoreSlim slots;
    private int capacity,active,waiting,outstanding,peak;
    private long completed;
    public static CpuScheduler Shared{get;}=new(Math.Max(1,Environment.ProcessorCount));
    private CpuScheduler(int capacity){this.capacity=capacity;this.slots=new(capacity,capacity);}
    public CpuSchedulerUsage Usage{get{lock(this.gate){return new(this.capacity,this.active,this.waiting,this.outstanding,this.peak,this.completed);}}}
    public bool IsExecuting=>current is not null;
    public void ConfigureMaximum(int maximum){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        if(maximum>Environment.ProcessorCount){throw new ArgumentOutOfRangeException(nameof(maximum));}
        lock(this.gate){
            if(this.outstanding!=0||this.waiting!=0){throw new InvalidOperationException("Configure the shared CPU limit before starting work.");}
            this.slots.Dispose();this.slots=new(maximum,maximum);this.capacity=maximum;this.peak=0;
        }
    }
    /// <summary>同期のCPU chunkだけを渡す。I/O待ちはSuspendForIoで枠を返す。</summary>
    public ValueTask<TResult> RunAsync<TResult>(Func<TResult> chunk,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(chunk);cancellationToken.ThrowIfCancellationRequested();
        if(current is{Held:true}){return ValueTask.FromResult(chunk());}
        return this.RunCore(chunk,cancellationToken);
    }
    /// <summary>取引のproducerを呼出しスレッド上で実行し、scopeの同期操作の所有者を維持する。</summary>
    public TResult Run<TResult>(Func<TResult> chunk,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(chunk);cancellationToken.ThrowIfCancellationRequested();if(current is{Held:true}){return chunk();}
        SemaphoreSlim slots;lock(this.gate){this.waiting++;slots=this.slots;}
        try{slots.Wait(cancellationToken);}catch{lock(this.gate){this.waiting--;}throw;}
        lock(this.gate){this.waiting--;this.outstanding++;this.active++;this.peak=Math.Max(this.peak,this.active);}
        var lease=new Lease(this,slots);var previous=current;current=lease;
        try{cancellationToken.ThrowIfCancellationRequested();return chunk();}
        finally{current=previous;lease.Dispose();lock(this.gate){this.completed++;}}
    }
    private async ValueTask<TResult> RunCore<TResult>(Func<TResult> chunk,CancellationToken cancellationToken){
        SemaphoreSlim slots;lock(this.gate){this.waiting++;slots=this.slots;}
        try{await slots.WaitAsync(cancellationToken).ConfigureAwait(false);}
        catch{lock(this.gate){this.waiting--;}throw;}
        lock(this.gate){this.waiting--;this.outstanding++;this.active++;this.peak=Math.Max(this.peak,this.active);}
        var lease=new Lease(this,slots);var previous=current;current=lease;
        try{cancellationToken.ThrowIfCancellationRequested();return chunk();}
        finally{current=previous;lease.Dispose();lock(this.gate){this.completed++;}}
    }
    /// <summary>nativeの同期待機中も他queryへCPU枠を渡す。Disposeで処理を再開する枠を取り直す。</summary>
    public static IDisposable? SuspendForIo(){
        var lease=current;if(lease is null||!lease.Held){return null;}lease.Suspend();return new Resume(lease);
    }
    /// <summary>I/O完了処理内のreader/callbackがCPU作業を行う区間だけ枠へ復帰する。</summary>
    public static IDisposable? EnterCallback(){
        var lease=current;if(lease is null||lease.Held){return null;}lease.Resume();return new Suspend(lease);
    }
    private sealed class Lease(CpuScheduler owner,SemaphoreSlim slots):IDisposable{
        internal bool Held=true;
        private bool disposed;
        internal void Suspend(){
            lock(owner.gate){if(this.disposed||!this.Held){return;}this.Held=false;owner.active--;slots.Release();}
        }
        internal void Resume(){
            if(this.disposed||this.Held){return;}slots.Wait();
            lock(owner.gate){this.Held=true;owner.active++;owner.peak=Math.Max(owner.peak,owner.active);}
        }
        public void Dispose(){
            lock(owner.gate){if(this.disposed){return;}this.disposed=true;if(this.Held){this.Held=false;owner.active--;slots.Release();}owner.outstanding--;}
        }
    }
    private sealed class Resume(Lease lease):IDisposable{
        private Lease? lease=lease;
        public void Dispose()=>Interlocked.Exchange(ref this.lease,null)?.Resume();
    }
    private sealed class Suspend(Lease lease):IDisposable{
        private Lease? lease=lease;
        public void Dispose()=>Interlocked.Exchange(ref this.lease,null)?.Suspend();
    }
}
