using Microsoft.JSInterop;

namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly DB表示順序 DBの表示順=new();
    private CatalogDatabase[] 表示DB一覧=>this.DBの表示順.適用(this.DB一覧);
    private bool DB表示順を保存できるか(string ID){
        var DB=this.DB一覧.FirstOrDefault(項目=>項目.Id==ID);
        return this.認証有効か&&this.利用者 is{Disabled:false}&&this.表示状態操作 is not null&&this.表示状態診断 is null&&DB is{IsSystem:false}&&DB.Id.StartsWith("db:",StringComparison.Ordinal)&&this.権限があるか(DB.Name,ManagementPermission.View)&&this.DBの表示順.保存値.Union(this.表示DB一覧.Select(項目=>項目.Id),StringComparer.Ordinal).Count()<=256;
    }
    private bool DB表示順を変更できるか(管理対象 対象,int 差){
        if(this.対象DB(対象) is not{} DB||!this.DB表示順を保存できるか(DB.Id)){return false;}
        var 一覧=this.表示DB一覧;var 位置=Array.FindIndex(一覧,項目=>項目.Id==DB.Id);var 保存件数=this.DBの表示順.保存値.Union(一覧.Select(項目=>項目.Id),StringComparer.Ordinal).Count();return 位置>=0&&位置+差>=0&&位置+差<一覧.Length&&保存件数<=256;
    }
    private Task DB表示順を変更Async(管理対象 対象,int 差){
        if(!this.DB表示順を変更できるか(対象,差)){throw new ManagementException("Unsupported",表示文言.取得("このブラウザーでは表示順を保存できません。"));}
        this.DBの表示順.移動(this.DB一覧,対象.ID,差);return Task.CompletedTask;
    }
    private Task DB表示順を戻すAsync(){this.DBの表示順.既定順に戻す();return Task.CompletedTask;}
    [JSInvokable]public Task DB表示順ドロップAsync(string ID,string 対象ID,bool 前か){
        if(!this.操作受付中か||!this.DB表示順を保存できるか(ID)||!this.DB表示順を保存できるか(対象ID)){return Task.CompletedTask;}
        this.DBの表示順.対象位置へ移動(this.DB一覧,ID,対象ID,前か);return this.InvokeAsync(this.StateHasChanged);
    }
}
