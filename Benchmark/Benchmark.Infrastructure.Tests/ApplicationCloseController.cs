namespace Benchmark.Infrastructure.Tests;

public sealed class アプリ終了調停{
    [Fact]
    public async Task 閉じる要求は元TaskとWorkerと保存とreceipt終了後に一度だけ通すAsync(){
        var 元の処理=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var Worker停止=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 保存=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var receipt=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 呼出数=0;
        var 調停=new global::Benchmark.Infrastructure.アプリ終了調停(async()=>{
            Interlocked.Increment(ref 呼出数);
            await 元の処理.Task;await Worker停止.Task;await 保存.Task;await receipt.Task;
        });
        var 閉じる=調停.終了を要求Async();
        Assert.False(調停.終了可能か);
        Assert.False(await 調停.終了を要求Async());
        元の処理.SetResult();Worker停止.SetResult();保存.SetResult();
        Assert.False(閉じる.IsCompleted);
        Assert.False(調停.終了可能か);
        receipt.SetResult();
        Assert.True(await 閉じる);
        Assert.True(調停.終了可能か);
        Assert.False(await 調停.終了を要求Async());
        Assert.Equal(1,呼出数);
    }

    [Fact]
    public async Task 停止不明時は閉じず新規操作も受け付けないAsync(){
        var 排他=new アプリ操作排他();
        var 予約=排他.予約する();
        var 調停=new global::Benchmark.Infrastructure.アプリ終了調停(()=>{
            排他.終了を受け付ける();
            throw new IOException("Workerの停止を確認できません。");
        });
        Assert.False(await 調停.終了を要求Async());
        Assert.False(調停.終了可能か);
        Assert.NotNull(調停.最終障害);
        予約!.Dispose();
        Assert.Null(排他.予約する());
    }
}
