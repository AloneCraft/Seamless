namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private bool DB名変更対応か,DB名前変更中か;
    private string? DB名前変更ID;
    private RenameDatabaseRequest? DB名前変更要求;
    private bool DB名を変更できるか(CatalogDatabase DB)=>this.DB名変更対応か&&!DB.IsSystem&&!this.DB名前変更中か&&!this.DB作成中か&&!this.ソース下書き処理中か&&(this.DB名前変更要求 is null||this.DB名前変更要求.DatabaseId==DB.Id)&&
        this.権限があるか(DB.Name,this.DB一括適用対応か?DB.Version==0?ManagementPermission.Edit:ManagementPermission.Manage|ManagementPermission.Edit:ManagementPermission.Manage)&&
        (!this.文書一覧.TryGetValue(DB.Id,out var 文書)||文書.未確定操作 is null&&文書.DB変更 is not{Delete:true});
    private void DB名前変更開始時(CatalogDatabase DB){
        if(!this.DB名を変更できるか(DB)){return;}
        if(this.DB名前変更要求 is{} 要求&&要求.DatabaseId!=DB.Id){return;}
        this.作成入力を閉じる();this.DB名編集中か=false;this.DB名前変更ID=DB.Id;
    }
    private void DB名前変更取消時(){
        if(this.DB名前変更中か){return;}
        if(this.DB名前変更要求 is not null){this.案内=表示文言.取得("名前変更の結果が不明です。「名前変更の結果を照会」で結果を再確認してください。");this.エラーか=true;}
        this.DB名前変更ID=null;
    }
    private Task DB名前変更再照会時Async(){
        if(this.DB名前変更要求 is not{} 要求){return Task.CompletedTask;}
        this.DB名前変更ID=要求.DatabaseId;return this.DB名前変更確定時Async(要求.Name);
    }
    private async Task DB名前変更確定時Async(string 名前){
        if(this.DB名前変更ID is null||this.DB名前変更中か){return;}
        var DB=this.DB一覧.Single(項目=>項目.Id==this.DB名前変更ID);
        if(this.DB一括適用対応か){await this.DB名前変更を下書きに追加Async(DB,名前);return;}
        if(名前==DB.Name&&this.DB名前変更要求 is null){this.DB名前変更取消時();return;}
        if(this.DB名前変更要求 is{} 未確定&&未確定.Name!=名前){this.案内=表示文言.取得("前の名前変更の結果が不明です。同じ名前で確定して結果を再確認してください。");this.エラーか=true;return;}
        this.DB名前変更要求??=new(DB.Id,DB.Name,名前,Guid.NewGuid());
        var 要求=this.DB名前変更要求;this.DB名前変更中か=true;
        try{
            await this.処理Async(async()=>{
                CatalogDatabase 結果;
                try{結果=await this.管理操作.RenameDatabaseAsync(要求,this.寿命.Token);}
                catch(ManagementException){this.DB名前変更要求=null;throw;}
                this.DB名前変更要求=null;this.DB名前変更ID=null;
                this.DB一覧=this.DB一覧.Select(項目=>{if(項目.Id==結果.Id){return 結果;}return 項目;}).ToArray();
                if(this.文書一覧.TryGetValue(結果.Id,out var 文書)){文書.DB情報を更新(結果);}
                foreach(var 関数 in this.関数定義文書.Values.Where(候補=>候補.DB.Id==結果.Id)){関数.DB情報を更新(結果);}
                if(this.利用者 is{} 利用者){this.利用者=利用者 with{Grants=利用者.Grants.Select(許可=>{if(許可.Database==DB.Name){return 許可 with{Database=結果.Name};}return 許可;}).ToArray()};}
                await this.更新Async();this.案内=表示文言.取得("DB名を変更しました。");
            });
        }finally{this.DB名前変更中か=false;}
    }
}
