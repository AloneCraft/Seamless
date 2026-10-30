using System.Text.Json;

namespace Benchmark.Infrastructure.Tests;

public sealed class アプリ操作ホスト{
    [Fact]
    public async Task 一度だけ実行し元の処理と保存完了を待つAsync(){
        var 通信=new 偽通信();
        var 完了=new TaskCompletionSource<アプリ操作結果>(TaskCreationOptions.RunContinuationsAsynchronously);
        var 実行数=0;
        var 排他=new アプリ操作排他();
        var サービス=new 偽サービス(()=>{
            var 予約=排他.予約する();
            if(予約 is null){return null;}
            return new(予約,_=>{Interlocked.Increment(ref 実行数);return 完了.Task;});
        });
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        var 操作=操作を作る();
        await ホスト.受信を適用Async([操作]);
        await ホスト.受信を適用Async([操作]);
        Assert.Equal(1,実行数);
        Assert.Single(通信.要求一覧);
        Assert.Empty(通信.終端一覧);
        Assert.True(排他.実行中か);
        完了.SetResult(new("Completed","Failed","run:failed-validation"));
        await ホスト.実行完了を待つAsync();
        Assert.False(排他.実行中か);
        var 終端=Assert.Single(通信.終端一覧);
        Assert.Equal("Completed",終端.State);
        Assert.Equal("Failed",終端.ValidationOutcome);
        Assert.True(終端.WorkersStopped&&終端.PersistenceCompleted);
    }

    [Fact]
    public async Task claim失敗時に開始しないAsync(){
        var 通信=new 偽通信{成功か=false};
        var 実行数=0;
        var 排他=new アプリ操作排他();
        var サービス=new 偽サービス(()=>new(排他.予約する()!,_=>{実行数++;return Task.FromResult(new アプリ操作結果("Completed","Passed","run:1"));}));
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        await ホスト.受信を適用Async([操作を作る()]);
        await ホスト.受信を適用Async([操作を作る()]);
        Assert.Equal(0,実行数);
        Assert.False(排他.実行中か);
        Assert.Equal("Rejected",Assert.Single(通信.終端一覧).State);
        Assert.True(ホスト.終了可能か);
    }

    [Fact]
    public async Task 取消受付後もcleanup終了前に終端を出さないAsync(){
        var 通信=new 偽通信();
        var 取消確認=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var サービス=new 偽サービス(()=>new(new アプリ操作排他().予約する()!,async 取消=>{
            using var 登録=取消.Register(()=>取消確認.SetResult());
            await cleanup.Task;
            return new("Cancelled","Unavailable","");
        }));
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        var 操作=操作を作る();
        await ホスト.受信を適用Async([操作]);
        操作.Status="Quarantined";
        操作.External!.CancellationRequested=true;
        await ホスト.受信を適用Async([操作]);
        await 取消確認.Task;
        Assert.Empty(通信.終端一覧);
        cleanup.SetResult();
        await ホスト.実行完了を待つAsync();
        Assert.Equal("Cancelled",Assert.Single(通信.終端一覧).State);
    }

    [Fact]
    public async Task cleanup証拠がない例外時は終端を捏造しないAsync(){
        var 通信=new 偽通信();
        var 排他=new アプリ操作排他();
        var サービス=new 偽サービス(()=>new(排他.予約する()!,_=>throw new IOException("Worker cleanup failed")));
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        await ホスト.受信を適用Async([操作を作る()]);
        await ホスト.実行完了を待つAsync();
        Assert.Empty(通信.終端一覧);
        Assert.NotNull(ホスト.最終障害);
        Assert.True(排他.実行中か);
        Assert.False(ホスト.終了可能か);
    }

    [Fact]
    public async Task claim応答の別nonceとtimeoutを実行許可にしないAsync(){
        foreach(var 応答異常 in new[]{"nonce","timeout","cancel"}){
            var 通信=new 偽通信{応答異常=応答異常};
            var 実行数=0;
            var 排他=new アプリ操作排他();
            var サービス=new 偽サービス(()=>new(排他.予約する()!,_=>{実行数++;return Task.FromResult(new アプリ操作結果("Completed","Passed","run:1"));}));
            var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
            await ホスト.受信を適用Async([操作を作る()]);
            await ホスト.受信を適用Async([操作を作る()]);
            Assert.Equal(0,実行数);
            Assert.Single(通信.要求一覧);
            Assert.Equal("Rejected",Assert.Single(通信.終端一覧).State);
            Assert.False(排他.実行中か);
        }
    }

    [Fact]
    public async Task 手動実行中と未知操作はclaimしないAsync(){
        var 通信=new 偽通信();
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,new 偽サービス(()=>null));
        await ホスト.受信を適用Async([操作を作る()]);
        Assert.Empty(通信.要求一覧);
        Assert.Equal("Rejected",Assert.Single(通信.終端一覧).State);
        var 未対応=操作を作る();未対応.Id=Guid.NewGuid().ToString("N");未対応.External!.OperationId=未対応.Id;未対応.Operation="eval";
        await ホスト.受信を適用Async([未対応]);
        Assert.Empty(通信.要求一覧);
        Assert.Equal(2,通信.終端一覧.Count);
    }

    [Fact]
    public async Task 成功と取消が競合しても受理可能なreceiptを保存するAsync(){
        var 通信=new 偽通信();
        var 完了=new TaskCompletionSource<アプリ操作結果>(TaskCreationOptions.RunContinuationsAsynchronously);
        var サービス=new 偽サービス(()=>new(new アプリ操作排他().予約する()!,_=>完了.Task));
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        var 操作=操作を作る();
        await ホスト.受信を適用Async([操作]);
        操作.Status="Cancelling";操作.External!.CancellationRequested=true;
        await ホスト.受信を適用Async([操作]);
        完了.SetResult(new("Completed","Passed","run:finished"));
        await ホスト.実行完了を待つAsync();
        var 終端=Assert.Single(通信.終端一覧);
        Assert.Equal("Cancelled",終端.State);
        Assert.Equal("Unavailable",終端.ValidationOutcome);
        Assert.True(ホスト.終了可能か);
    }

    [Fact]
    public async Task 未開始の取消を拒否しIO復旧後も同じreceiptだけ再公開するAsync(){
        var 通信=new 偽通信{保存失敗か=true,応答異常="timeout"};
        var 実行数=0;
        var 排他=new アプリ操作排他();
        var サービス=new 偽サービス(()=>new(排他.予約する()!,_=>{実行数++;return Task.FromResult(new アプリ操作結果("Completed","Passed","run:1"));}));
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        var 操作=操作を作る();
        await ホスト.受信を適用Async([操作]);
        Assert.Equal(0,実行数);
        Assert.Empty(通信.終端一覧);
        Assert.True(排他.実行中か);
        Assert.False(ホスト.終了可能か);
        var 最初のreceipt=通信.保存試行一覧[0];
        通信.保存失敗か=false;
        操作.Status="Quarantined";操作.External!.Claimed=true;操作.External.CancellationRequested=true;
        await ホスト.受信を適用Async([操作]);
        Assert.Equal(0,実行数);
        Assert.Single(通信.要求一覧);
        var 終端=Assert.Single(通信.終端一覧);
        Assert.Same(最初のreceipt,終端);
        Assert.Equal("Rejected",終端.State);
        Assert.False(排他.実行中か);
        Assert.True(ホスト.終了可能か);
    }

    [Fact]
    public async Task 完成済み結果はIO復旧後も実行し直さないAsync(){
        var 通信=new 偽通信{保存失敗か=true};
        var 実行数=0;
        var サービス=new 偽サービス(()=>new(new アプリ操作排他().予約する()!,_=>{実行数++;return Task.FromResult(new アプリ操作結果("Completed","Failed","run:1"));}));
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        await ホスト.受信を適用Async([操作を作る()]);
        await ホスト.実行完了を待つAsync();
        var 最初のreceipt=通信.保存試行一覧[0];
        Assert.False(ホスト.終了可能か);
        通信.保存失敗か=false;
        await ホスト.終端を再公開Async();
        Assert.Same(最初のreceipt,Assert.Single(通信.終端一覧));
        Assert.Equal(1,実行数);
        Assert.True(ホスト.終了可能か);
    }

    [Fact]
    public async Task 受付前に取消済みならclaimせず拒否receiptを保存するAsync(){
        var 通信=new 偽通信();
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,new 偽サービス(()=>throw new InvalidOperationException("準備を開始しません")));
        var 操作=操作を作る();操作.Status="Cancelling";操作.External!.CancellationRequested=true;
        await ホスト.受信を適用Async([操作]);
        Assert.Empty(通信.要求一覧);
        Assert.Equal("Rejected",Assert.Single(通信.終端一覧).State);
    }

    [Fact]
    public async Task ホスト終了も元Taskのcleanupとreceiptを待つAsync(){
        var 通信=new 偽通信();
        var 完了=new TaskCompletionSource<アプリ操作結果>(TaskCreationOptions.RunContinuationsAsynchronously);
        var 取消確認=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var サービス=new 偽サービス(()=>new(new アプリ操作排他().予約する()!,async 取消=>{
            using var 登録=取消.Register(()=>取消確認.TrySetResult());
            return await 完了.Task;
        }));
        var ホスト=new global::Benchmark.Infrastructure.アプリ操作ホスト("Benchmark.Dynamic.UI",通信,サービス);
        await ホスト.受信を適用Async([操作を作る()]);
        using var 終了=new CancellationTokenSource();終了.Cancel();
        var 停止=ホスト.実行Async(終了.Token);
        await 取消確認.Task;
        Assert.False(停止.IsCompleted);
        Assert.False(ホスト.終了可能か);
        Assert.Empty(通信.終端一覧);
        完了.SetResult(new("Completed","Passed","run:1"));
        await 停止;
        Assert.True(ホスト.終了可能か);
        var 終端=Assert.Single(通信.終端一覧);
        Assert.Equal("Cancelled",終端.State);
        Assert.Equal("Unavailable",終端.ValidationOutcome);
    }

    private static ExternalTaskRecord 操作を作る()=>new(){Id=new string('2',32),Application="Benchmark.Dynamic.UI",Operation="run",Status="Reserved",ArtifactDirectory=Path.GetFullPath("artifacts"),External=new(){OperationId=new string('2',32),Target=偽通信.対象,GrantToken=new string('a',64),Parameters=JsonSerializer.SerializeToElement(new{})}};
    private sealed class 偽サービス(Func<アプリ操作準備?> 準備):Iアプリ操作サービス{
        public IReadOnlyList<string> 操作一覧=>["run"];
        public Task<アプリ操作準備?> 準備Async(string 操作,JsonElement 設定)=>Task.FromResult(準備());
    }
    private sealed class 偽通信:Iアプリ操作通信{
        internal static readonly ExternalTarget 対象=new(new string('1',32),123,639285123450000000);
        public ExternalTarget 接続先=>対象;
        internal bool 成功か=true;
        internal string 応答異常="";
        internal bool 保存失敗か;
        internal readonly List<ExternalRequest> 要求一覧=[];
        internal readonly List<ExternalTerminalReceipt> 終端一覧=[];
        internal readonly List<ExternalTerminalReceipt> 保存試行一覧=[];
        public Task<ExternalReply> 送信Async(ExternalRequest 要求,CancellationToken 取消){
            this.要求一覧.Add(要求);
            if(this.応答異常=="timeout"){throw new TimeoutException();}
            var 操作=操作を作る();操作.Status="Running";操作.External!.Claimed=true;
            if(this.応答異常=="nonce"){操作.External.GrantToken=new string('b',64);}
            if(this.応答異常=="cancel"){操作.Status="Cancelling";操作.External.CancellationRequested=true;}
            return Task.FromResult(new ExternalReply{Ok=this.成功か,Task=操作});
        }
        public Task 終端を保存Async(ExternalTaskRecord 操作,ExternalTerminalReceipt 終端){
            this.保存試行一覧.Add(終端);
            if(this.保存失敗か){throw new IOException("一時的な保存失敗");}
            this.終端一覧.Add(終端);return Task.CompletedTask;
        }
    }
}
