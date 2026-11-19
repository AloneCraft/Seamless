namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private string? DB名前変更ID;
    private bool DB名を変更できるか(CatalogDatabase DB)=>this.DB一括適用対応か&&!DB.IsSystem&&!this.操作中か&&
        this.権限があるか(DB.Name,DB.Version==0?ManagementPermission.Edit:ManagementPermission.Manage|ManagementPermission.Edit)&&
        (!this.文書一覧.TryGetValue(DB.Id,out var 文書)||文書.未確定操作 is null&&文書.DB変更 is not{Delete:true});
    private void DB名前変更開始時(CatalogDatabase DB){
        if(!this.DB名を変更できるか(DB)||this.DB名編集中か||this.DB名前変更ID is not null){return;}
        this.作成入力を閉じる();this.DB名前変更ID=DB.Id;
    }
    private void DB名前変更取消時()=>this.DB名前変更ID=null;
    private Task DB名前変更確定時Async(string 名前){
        if(this.DB一覧.FirstOrDefault(DB=>DB.Id==this.DB名前変更ID) is{} DB&&this.DB名を変更できるか(DB)){
            return this.DB名前変更を下書きに追加Async(DB,名前);
        }
        return Task.CompletedTask;
    }
}
