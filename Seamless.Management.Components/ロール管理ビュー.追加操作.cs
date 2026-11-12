namespace Seamless.Management.Components;

public partial class ロール管理ビュー{
    private コマンド登録<string>? 追加登録;
    public bool 新規開始できるか=>!this.処理中か&&!this.編集ありか&&this.未確定操作 is null;
    private bool 割当展開か;
    private long 割当展開世代;
    private コマンド登録<string> 追加コマンド=>this.追加登録??=new([
        new("role","新しいロール",ID=>ID=="roles",_=>!this.処理中か&&this.DBID is not null&&!this.編集ありか&&this.未確定操作 is null,_=>this.新規時Async(),["role","作成"],アイコン:"ロール"),
        new("rule","対象資源の規則を追加",ID=>ID!="roles",this.追加対象を選べるか,async ID=>{await this.追加対象を選ぶAsync(ID);await this.規則追加時Async();},["rule","permission","許可","拒否"],アイコン:"権限を追加"),
        new("binding","割り当てを追加",ID=>ID!="roles",this.追加対象を選べるか,async ID=>{await this.追加対象を選ぶAsync(ID);this.割当展開か=true;this.割当展開世代++;},["user","group","binding"],アイコン:"利用者")
    ]);
    private bool 追加対象を選べるか(string ID)=>!this.処理中か&&this.未確定操作 is null&&(this.ロールID?.ToString()==ID||!this.編集ありか&&this.全ロール.Any(値=>値.Id.ToString()==ID));
    private async Task 追加対象を選ぶAsync(string ID){
        if(this.ロールID?.ToString()==ID){return;}
        var 対象=this.全ロール.Single(値=>値.Id.ToString()==ID);await this.選択時Async(対象);
    }
    private Task ロール行選択Async(AuthorizationRoleHeader ロール){if(this.処理中か){return Task.CompletedTask;}return this.選択時Async(ロール);}
    public async Task 追加開始Async(){
        if(!this.新規開始できるか){return;}
        if(!this.取得済みか){await this.一覧取得Async();}
        await this.新規時Async();await this.InvokeAsync(this.StateHasChanged);
    }
}
