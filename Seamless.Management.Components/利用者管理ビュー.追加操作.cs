namespace Seamless.Management.Components;

public partial class 利用者管理ビュー{
    private コマンド登録<string>? 追加登録;
    public bool 新規開始できるか=>!this.処理中か&&!this.編集ありか;
    private コマンド登録<string> 追加コマンド=>this.追加登録??=new([
        new("user","利用者を作成",ID=>ID=="users",_=>!this.処理中か&&!this.編集ありか,_=>this.新規開始時Async(),["user","account"],アイコン:"利用者"),
        new("grant","権限を追加",ID=>ID!="users",ID=>!this.処理中か&&(ID=="editing"&&this.編集中か||ID=="user:"+this.原本?.User.Name||!this.編集ありか&&this.一覧.Any(値=>"user:"+値.User.Name==ID)),async ID=>{
            if(ID!="editing"&&ID!="user:"+this.原本?.User.Name){await this.編集開始時Async(this.一覧.Single(値=>"user:"+値.User.Name==ID));}
            await this.権限追加時Async();
        },["grant","permission","DB"],アイコン:"権限を追加")
    ]);
    public async Task 追加開始Async(){
        if(!this.新規開始できるか){return;}
        if(!this.読込済みか){await this.一覧取得Async(false);}
        await this.新規開始時Async();await this.InvokeAsync(this.StateHasChanged);
    }
}
