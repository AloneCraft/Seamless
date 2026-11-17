using Microsoft.JSInterop;

namespace Seamless.Management.Components;

internal static class スクリプト参照{
    internal static async ValueTask 解放Async(IJSObjectReference? 操作,IJSObjectReference? モジュール){
        try{
            if(操作 is not null){
                try{await 操作.InvokeVoidAsync("dispose");}
                catch(JSDisconnectedException){}
                catch(OperationCanceledException){}
                finally{await 破棄Async(操作);}
            }
        }finally{await 破棄Async(モジュール);}
    }
    private static async ValueTask 破棄Async(IJSObjectReference? 対象){
        if(対象 is null){return;}
        // ブラウザー終了時は切断通知の前に通信待ちが取り消されることもある。
        // ここは破棄専用であり、JS側の参照は回線とともに破棄される。
        try{await 対象.DisposeAsync();}
        catch(JSDisconnectedException){}
        catch(OperationCanceledException){}
    }
}
