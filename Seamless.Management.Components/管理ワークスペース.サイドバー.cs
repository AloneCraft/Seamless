using Microsoft.JSInterop;

namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly string サイドバーID="manager-sidebar-"+Guid.NewGuid().ToString("N");
    private IJSObjectReference? サイドバーモジュール,サイドバー操作;
    private async Task サイドバーを初期化Async(){
        try{
            this.サイドバーモジュール=await this.スクリプト.InvokeAsync<IJSObjectReference>("import","./_content/Seamless.Management.Components/sidebar-layout.js");
            this.サイドバー操作=await this.サイドバーモジュール.InvokeAsync<IJSObjectReference>("initialize",this.根);
        }catch(JSDisconnectedException){}
        catch(JSException){this.表示状態診断="サイドバーの操作を初期化できません。";await this.InvokeAsync(this.StateHasChanged);}
    }
}
