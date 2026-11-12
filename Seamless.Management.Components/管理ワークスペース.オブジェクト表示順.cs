using System.Text.Json;
using Microsoft.JSInterop;

namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private IJSObjectReference? オブジェクト順モジュール,オブジェクト順操作;
    [JSInvokable]public Task オブジェクト表示順失敗Async(){this.案内=表示文言.取得("操作に失敗しました。編集内容は保持しています。");this.エラーか=true;return this.InvokeAsync(this.StateHasChanged);}
    private bool オブジェクト表示順を使えるか(string ID)=>this.操作受付中か&&this.認証有効か&&this.DB一覧.FirstOrDefault(DB=>DB.Id==ID) is{IsSystem:false} DB&&this.権限があるか(DB.Name,ManagementPermission.View);
    private bool オブジェクト表示順を変更できるか(管理対象 対象,int 差)=>対象.表 is{} ID&&this.オブジェクト表示順を使えるか(対象.ID)&&this.DBツリー画面.TryGetValue(対象.ID,out var 木)&&木.表示順を変更できるか(ID,差);
    private Task オブジェクト表示順を変更Async(管理対象 対象,int 差){
        if(!this.オブジェクト表示順を変更できるか(対象,差)){return Task.CompletedTask;}return this.DBツリー画面[対象.ID].表示順を変更Async(対象.表!,差);
    }
    private Task オブジェクト表示順を戻すAsync(管理対象 対象){
        if(対象.表 is null||!this.オブジェクト表示順を使えるか(対象.ID)||!this.DBツリー画面.TryGetValue(対象.ID,out var 木)){return Task.CompletedTask;}return 木.表示順を戻すAsync(対象.表);
    }
    [JSInvokable]public Task オブジェクト表示順ドロップAsync(string? グループ,string? ID,string? 対象ID,bool 前か){
        if(グループ is null||ID is null||対象ID is null||グループ.Length>16384||ID.Length>4096||対象ID.Length>4096){return Task.CompletedTask;}
        string[]? 範囲;try{範囲=JsonSerializer.Deserialize<string[]>(グループ);}catch(JsonException){return Task.CompletedTask;}
        if(範囲 is not{Length:4}||範囲.Any(値=>値 is null)||!this.オブジェクト表示順を使えるか(範囲[0])||!this.DBツリー画面.TryGetValue(範囲[0],out var 木)){return Task.CompletedTask;}
        var 先頭="query:";if(範囲[2]=="Table"){先頭="table:";}
        if(!ID.StartsWith(先頭,StringComparison.Ordinal)||!対象ID.StartsWith(先頭,StringComparison.Ordinal)){return Task.CompletedTask;}
        return 木.表示順をドロップAsync(範囲[1],範囲[2],範囲[3],ID[先頭.Length..],対象ID[先頭.Length..],前か);
    }
}
