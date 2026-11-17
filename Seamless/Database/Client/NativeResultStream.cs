using System.Runtime.CompilerServices;
using Seamless.Execution.Codecs;

namespace Seamless.Database.Client;

/// <summary>一回だけ列挙する業務結果。途中解放では要求の取消と後始末の完了を待つ。</summary>
public sealed class NativeResultStream<T>:IAsyncEnumerable<T>,IAsyncEnumerator<T>{
    private readonly object gate=new();
    private readonly SemaphoreSlim movement=new(1,1);
    private readonly CancellationTokenSource lifetime;
    private Func<Func<ReadOnlySpan<byte>,bool>,CancellationToken,IAsyncEnumerable<bool>>? receive;
    private Func<ReadOnlySpan<byte>,CancellationToken,T>? decode;
    private CancellationTokenSource? enumerationCancellation;
    private CancellationToken enumerationToken;
    private IAsyncEnumerator<bool>? iterator;
    private Task? disposal;
    private bool claimed;
    private bool disposed;
    private bool ended;
    private bool hasCurrent;
    private int moving;
    private T current=default!;
    internal RecordGraphValidationLimits RecordLimits{get;}
    internal CancellationToken CancellationToken=>this.lifetime.Token;

    internal NativeResultStream(Func<Func<ReadOnlySpan<byte>,bool>,CancellationToken,IAsyncEnumerable<bool>> receive,
        Func<ReadOnlySpan<byte>,CancellationToken,T> decode,RecordGraphValidationLimits limits,CancellationToken cancellationToken){
        this.receive=receive;this.decode=decode;this.RecordLimits=limits;this.lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }
    public T Current{get{if(!this.hasCurrent){throw new InvalidOperationException("結果の現在行はありません。");}return this.current;}}
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken=default){
        this.Start(payload=>{this.current=this.decode!(payload,this.enumerationToken);this.hasCurrent=true;return true;},cancellationToken);return this;
    }
    public ConfiguredCancelableAsyncEnumerable<T> ConfigureAwait(bool continueOnCapturedContext)=>
        ((IAsyncEnumerable<T>)this).ConfigureAwait(continueOnCapturedContext);
    private void Start(Func<ReadOnlySpan<byte>,bool> project,CancellationToken cancellationToken){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.claimed){throw new InvalidOperationException("結果ストリームは一度だけ列挙できます。");}
            this.claimed=true;
            try{
                this.enumerationCancellation=CancellationTokenSource.CreateLinkedTokenSource(this.lifetime.Token,cancellationToken);
                this.enumerationToken=this.enumerationCancellation.Token;
                this.iterator=this.receive!(project,this.enumerationToken).GetAsyncEnumerator(this.enumerationToken);
            }catch{
                this.disposed=true;this.disposal=Task.CompletedTask;this.receive=null;this.decode=null;
                this.enumerationCancellation?.Dispose();this.lifetime.Dispose();throw;
            }
        }
    }
    public ValueTask<bool> MoveNextAsync()=>this.AdvanceAsync();
    private async ValueTask<bool> AdvanceAsync(){
        if(Interlocked.CompareExchange(ref this.moving,1,0)!=0){throw new InvalidOperationException("同じ結果の読取を同時には実行できません。");}
        try{
            bool more;
            try{
                await this.movement.WaitAsync().ConfigureAwait(false);
                try{
                    if(this.ended){return false;}
                    ObjectDisposedException.ThrowIf(this.disposed,this);
                    if(this.iterator is null){throw new InvalidOperationException("先に結果の列挙を開始してください。");}
                    this.hasCurrent=false;this.current=default!;
                    more=await this.iterator.MoveNextAsync().ConfigureAwait(false);
                    if(!more){this.ended=true;}
                }finally{this.movement.Release();}
            }catch{await this.DisposeAsync().ConfigureAwait(false);throw;}
            if(!more){await this.DisposeAsync().ConfigureAwait(false);}
            return more;
        }finally{Volatile.Write(ref this.moving,0);}
    }
    internal async IAsyncEnumerable<bool> Project(Func<ReadOnlySpan<byte>,bool> project,[EnumeratorCancellation]CancellationToken cancellationToken){
        this.Start(project,cancellationToken);
        try{while(await this.AdvanceAsync().ConfigureAwait(false)){yield return true;}}
        finally{await this.DisposeAsync().ConfigureAwait(false);}
    }
    public ValueTask DisposeAsync(){
        lock(this.gate){
            if(this.disposal is null){this.disposed=true;this.disposal=this.DisposeCoreAsync();}
            return new(this.disposal);
        }
    }
    private async Task DisposeCoreAsync(){
        try{if(!this.ended){this.lifetime.Cancel();}}
        finally{
            await this.movement.WaitAsync().ConfigureAwait(false);
            try{if(this.iterator is not null){await this.iterator.DisposeAsync().ConfigureAwait(false);}}
            finally{
                this.iterator=null;this.receive=null;this.decode=null;this.current=default!;this.hasCurrent=false;
                this.enumerationCancellation?.Dispose();this.lifetime.Dispose();this.movement.Release();
            }
        }
    }
}
