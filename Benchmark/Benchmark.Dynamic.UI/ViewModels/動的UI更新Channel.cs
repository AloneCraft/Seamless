using System.Diagnostics;
using System.Threading.Channels;

namespace Benchmark.Dynamic.UI;

public sealed class 動的UI更新Channel:IAsyncDisposable{
    private readonly Channel<更新項目> channel=Channel.CreateUnbounded<更新項目>(new UnboundedChannelOptions{
        SingleReader=true,
        SingleWriter=false,
        AllowSynchronousContinuations=false,
    });
    private readonly Action<動的結果行> 結果受信時;
    private readonly Action<動的実行進捗> 進捗受信時;
    private readonly Func<Action,Task> dispatcher;
    private readonly TimeSpan UI更新間隔;
    private readonly TimeSpan 進捗更新間隔;
    private readonly Task pump;
    private int 完了済み;

    public 動的UI更新Channel(
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時,
        TimeSpan? ui更新間隔=null,
        TimeSpan? 進捗更新間隔=null,
        Func<Action,Task>? dispatcher=null){
        ArgumentNullException.ThrowIfNull(結果受信時);
        ArgumentNullException.ThrowIfNull(進捗受信時);
        this.結果受信時=結果受信時;
        this.進捗受信時=進捗受信時;
        this.UI更新間隔=正の間隔(ui更新間隔??TimeSpan.FromMilliseconds(100),nameof(ui更新間隔));
        this.進捗更新間隔=正の間隔(進捗更新間隔??TimeSpan.FromMilliseconds(250),nameof(進捗更新間隔));
        this.dispatcher=dispatcher??(action=>MainThread.InvokeOnMainThreadAsync(action));
        this.pump=this.PumpAsync();
    }

    public void 結果を追加(動的結果行 row){
        ArgumentNullException.ThrowIfNull(row);
        _=this.channel.Writer.TryWrite(new(row,null));
    }

    public void 進捗を更新(動的実行進捗 progress){
        ArgumentNullException.ThrowIfNull(progress);
        _=this.channel.Writer.TryWrite(new(null,progress));
    }

    public async Task 完了Async(){
        if(Interlocked.Exchange(ref this.完了済み,1)==0){
            this.channel.Writer.TryComplete();
        }
        await this.pump.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync(){
        await this.完了Async().ConfigureAwait(false);
    }

    private async Task PumpAsync(){
        using var timer=new PeriodicTimer(this.UI更新間隔);
        var pendingRows=new List<動的結果行>();
        動的実行進捗? pendingProgress=null;
        long? lastProgressTimestamp=null;
        while(await timer.WaitForNextTickAsync().ConfigureAwait(false)){
            while(this.channel.Reader.TryRead(out var update)){
                if(update.Row is not null){
                    pendingRows.Add(update.Row);
                }
                if(update.Progress is not null){
                    pendingProgress=update.Progress;
                }
            }
            var completed=this.channel.Reader.Completion.IsCompleted;
            var now=Stopwatch.GetTimestamp();
            var progressDue=pendingProgress is not null
                &&(completed||lastProgressTimestamp is null
                    ||Stopwatch.GetElapsedTime(lastProgressTimestamp.Value,now)>=this.進捗更新間隔);
            if(pendingRows.Count!=0||progressDue){
                var rowBatch=pendingRows.ToArray();
                pendingRows.Clear();
                var progress=progressDue?pendingProgress:null;
                if(progressDue){
                    pendingProgress=null;
                    lastProgressTimestamp=now;
                }
                await this.dispatcher(()=>{
                    foreach(var row in rowBatch){
                        this.結果受信時(row);
                    }
                    if(progress is not null){
                        this.進捗受信時(progress);
                    }
                }).ConfigureAwait(false);
            }
            if(completed){
                return;
            }
        }
    }

    private static TimeSpan 正の間隔(TimeSpan value,string parameterName){
        if(value<=TimeSpan.Zero){
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return value;
    }

    private sealed record 更新項目(動的結果行? Row,動的実行進捗? Progress);
}
