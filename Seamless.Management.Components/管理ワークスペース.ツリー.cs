namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private bool ツリー所属選択中か;
    private readonly Dictionary<string,ツリー展開状態> DBツリー一覧=new(StringComparer.Ordinal);
    private ツリー展開状態 DBツリー状態(string ID){
        if(!this.DBツリー一覧.TryGetValue(ID,out var 状態)){
            状態=new();this.DBツリー一覧.Add(ID,状態);
        }
        return 状態;
    }
    private Task DBツリー開閉時Async(string ID,bool 展開か)=>this.処理Async(()=>this.DBツリーを開閉Async(ID,展開か));
    private async Task DBツリーを開閉Async(string ID,bool 展開か){
        var 状態=this.DBツリー状態(ID);状態.展開中か=展開か;
        if(!展開か){return;}
        try{await this.DB文書を読み込むAsync(this.DB一覧.Single(DB=>DB.Id==ID));}
        catch{状態.展開中か=false;throw;}
    }
    private void ツリー文書を選ぶ(string ID){
        if(this.選択DBID!=ID){this.文書を選ぶ(ID);}
        this.タブ順序.追加(ID);
    }
    private void ツリースキーマ選択時(string ID,string 名前){this.ツリー文書を選ぶ(ID);this.スキーマを選ぶ(名前);}
    private Task ツリー項目選択時(string ID,CatalogObject 項目){
        this.ツリー文書を選ぶ(ID);
        if(this.公開情報表示か(ID)){this.公開対象を開く(ID,項目);return Task.CompletedTask;}
        if(項目.Kind=="RegisteredFunction"){return this.処理Async(()=>this.登録関数を開くAsync(ID,項目));}
        this.オブジェクトを選ぶ時(項目);return Task.CompletedTask;
    }
    private Task ツリー追加要求時Async(string ID,対象操作要求 要求){this.ツリー文書を選ぶ(ID);return this.追加メニュー要求時Async(要求);}
    private Task ツリーメニュー要求時Async(string ID,対象操作要求 要求){this.ツリー文書を選ぶ(ID);return this.メニュー要求時Async(要求);}
    private string ツリー選択スキーマ(string ID){if(this.選択DBID==ID){return this.選択スキーマ名;}return "";}
    private CatalogObject? ツリー選択オブジェクト(string ID){
        if(this.現在対象文書 is{} 対象&&対象.DBID==ID&&対象.対象 is not null){return 対象.対象;}
        if(this.ツリー所属選択中か||this.選択DBID!=ID||!this.文書一覧.TryGetValue(ID,out var 文書)){return null;}
        var 一覧=文書.オブジェクト;
        if(this.選択表名 is{} 表){return 一覧.FirstOrDefault(項目=>項目.Kind=="Table"&&項目.Id==表);}
        if(this.計画対象 is{} 計画&&計画.DefinitionId==文書.定義.Id){return 一覧.FirstOrDefault(項目=>項目.Kind!="Table"&&項目.Id==計画.ObjectId);}
        if(this.実行問い合わせ is{} 問い合わせ){return 一覧.FirstOrDefault(項目=>項目.Kind==問い合わせ.Kind.ToString()&&項目.Id==問い合わせ.Path);}
        return null;
    }
}
