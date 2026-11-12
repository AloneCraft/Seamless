using System.Reflection;
using Microsoft.JSInterop;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class 型入力ライフサイクル{
    [Fact]public async Task JS応答待ちに再描画されてもポップを一度だけ破棄する(){
        var ポップ=new 試験参照{破棄を待たせるか=true};
        var モジュール=new 試験参照{子=ポップ};
        await using var 入力=new 試験入力();
        typeof(型入力).GetProperty("スクリプト",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(入力,new 試験ランタイム(モジュール));
        await 入力.描画後Async();
        var 状態=typeof(型入力).GetField("表示中か",BindingFlags.Instance|BindingFlags.NonPublic)!;
        状態.SetValue(入力,true);
        await 入力.描画後Async();
        状態.SetValue(入力,false);
        var 最初=入力.描画後Async();
        await ポップ.破棄受付.Task;
        var 重複=入力.描画後Async();
        try{
            Assert.Equal(1,ポップ.破棄呼び出し数);
        }finally{
            ポップ.応答.TrySetResult();
            await Task.WhenAll(最初,重複);
        }
        Assert.Equal(1,ポップ.参照解放数);
    }
    private sealed class 試験入力:型入力{
        public Task 描画後Async()=>base.OnAfterRenderAsync(false);
    }
    private sealed class 試験ランタイム(IJSObjectReference モジュール):IJSRuntime{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>ValueTask.FromResult((TValue)モジュール);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
    }
    private sealed class 試験参照:IJSObjectReference{
        public 試験参照? 子{get;init;}
        public bool 破棄を待たせるか{get;init;}
        public int 破棄呼び出し数,参照解放数;
        public TaskCompletionSource 破棄受付{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource 応答{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>this.InvokeAsync<TValue>(identifier,default,args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args){
            if(identifier=="dispose"){
                this.破棄呼び出し数++;
                this.破棄受付.TrySetResult();
                if(this.破棄を待たせるか){await this.応答.Task.WaitAsync(cancellationToken);}
            }
            if(identifier=="型入力を初期化"){return (TValue)(object)new 試験参照();}
            if(typeof(TValue)==typeof(IJSObjectReference)){return (TValue)(object)this.子!;}
            return default!;
        }
        public ValueTask DisposeAsync(){this.参照解放数++;return ValueTask.CompletedTask;}
    }
}
