using Benchmark.Infrastructure;
using Benchmark.Scalable.Core;
using Xunit;

namespace Benchmark.Scalable.Tests;

public sealed class 大規模実行サービス{
    [Fact]
    public async Task 実行Async(){
        using var 取消=new CancellationTokenSource();
        var 開始=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cleanup許可=new ManualResetEventSlim();
        var cleanup完了か=false;
        var サービス=new UI.大規模実行サービス((要求,進捗,Token)=>{
            try{開始.SetResult();cleanup許可.Wait();Token.ThrowIfCancellationRequested();throw new InvalidOperationException("取消されていません。");}
            finally{cleanup完了か=true;}
        });
        var 元の処理=サービス.実行Async(new ScalableRequest(),null,取消.Token);
        await 開始.Task;
        try{
            取消.Cancel();
            Assert.False(元の処理.IsCompleted);
            Assert.False(cleanup完了か);
        }finally{cleanup許可.Set();}
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>元の処理);
        Assert.True(cleanup完了か);
    }

    [Fact]
    public void 手動と外部操作で予約を共有する(){
        var サービス=new UI.大規模実行サービス((_,_,_)=>throw new InvalidOperationException());
        using(var 手動=サービス.実行排他.予約する()){
            Assert.NotNull(手動);
            Assert.Null(サービス.実行排他.予約する());
        }
        using var 外部=サービス.実行排他.予約する();
        Assert.NotNull(外部);
    }
}
