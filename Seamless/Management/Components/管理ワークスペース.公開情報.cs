namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly Dictionary<string,公開DB表示状態> 公開DB一覧=new(StringComparer.Ordinal);
    private bool 公開情報表示か(string ID)=>this.公開DB一覧.ContainsKey(ID)&&!this.権限があるか(this.公開DB一覧[ID].DB.Name,ManagementPermission.Edit);
    private async Task 公開DBを読むAsync(CatalogDatabase DB,bool 強制か=false){
        if(!this.権限があるか(DB.Name,ManagementPermission.View)){throw new ManagementException("Permission",表示文言.取得("権限がありません。"));}
        if(!強制か&&this.公開DB一覧.TryGetValue(DB.Id,out var 既存)&&既存.DB==DB){return;}
        var スキーマ=new List<string>();var 一覧=new List<CatalogObject>();var 版=this.カタログ版;long 次=0;
        do{var 頁=await this.管理操作.SchemasAsync(new(DB.Id,版,次),this.寿命.Token);スキーマ.AddRange(頁.Items.Where(項目=>項目.Name.Length>0).Select(項目=>項目.Name));版=頁.VersionToken;次=頁.NextCursor;if(!頁.HasMore){break;}if(スキーマ.Count>8192){throw new ManagementException("Limit","公開カタログの表示件数が上限を超えています。");}}while(true);
        次=0;
        do{var 頁=await this.管理操作.ObjectsAsync(new(DB.Id,版,次),this.寿命.Token);一覧.AddRange(頁.Items);次=頁.NextCursor;if(!頁.HasMore){break;}if(一覧.Count>8192){throw new ManagementException("Limit","公開カタログの表示件数が上限を超えています。");}}while(true);
        if(this.破棄済みか||!this.権限があるか(DB.Name,ManagementPermission.View)){throw new ManagementException("Permission",表示文言.取得("権限がありません。"));}
        this.公開DB一覧[DB.Id]=new(DB,版,スキーマ.ToArray(),一覧.ToArray());await this.登録関数を読むAsync(DB);
    }
    private CatalogObject[] 公開ツリー項目(公開DB表示状態 文書)=>[..文書.オブジェクト,..this.登録関数一覧.GetValueOrDefault(文書.DB.Id,[]).Select(関数=>new CatalogObject("registered:"+関数.Target.DefinitionId,関数.Name,"","RegisteredFunction",""))];
    private bool 文書は未適用か(対象文書状態 文書){if(this.公開情報表示か(文書.DBID)){return false;}return this.対象の定義文書(文書).未適用か;}
    private bool 行変更権限があるか(string DB)=>this.権限があるか(DB,ManagementPermission.WriteRows)||this.権限があるか("*",ManagementPermission.View|ManagementPermission.Execute|ManagementPermission.Edit|ManagementPermission.Validate|ManagementPermission.Apply|ManagementPermission.Manage);
    private bool 公開メニュー操作か(管理対象 対象,string ID){
        if(!this.公開情報表示か(対象.ID)){return true;}
        return ID is "DB表示上" or "DB表示下" or "DB表示既定" or "DB名変更" or "開く" or "更新" or "表データ" or "登録関数データ" or "前のタブ" or "次のタブ" or "閉じる" or "ロール" or "設定" or "利用者" or "監査";
    }
    private void 公開対象を開く(string ID,CatalogObject 項目){
        var 関数=this.登録関数一覧.GetValueOrDefault(ID,[]).FirstOrDefault(関数=>"registered:"+関数.Target.DefinitionId==項目.Id);
        var 文書=this.対象文書を取得(ID,項目,関数:関数);文書.表示="データ";this.作業文書を選ぶ(文書.ID);
    }
    private ブラウザー保存文書? 公開文書参照(対象文書状態 対象){
        var DB=this.公開DB一覧[対象.DBID].DB;if(!this.権限があるか(DB.Name,ManagementPermission.View)){return null;}
        var 定義=DB.DefinitionId;var 版=DB.Version;var 成果物=DB.ArtifactHash;
        if(対象.登録関数か){定義=対象.関数!.Target.DefinitionId;版=対象.関数.Version;成果物=対象.関数.ArtifactHash;}
        var 種類=対象.対象?.Kind??"Database";var 表示="データ";if(対象.対象 is null){表示="DB情報";}if(対象.スキーマか){種類="Schema";}
        var 参照=new ブラウザー保存文書(DB.Id,DB.Version,DB.ArtifactHash,定義,版,成果物,"",種類,対象.対象?.Id??"",対象.スキーマ??対象.対象?.Schema??"",表示){PublishedOnly=true};
        try{参照.検証();return 参照;}catch(InvalidOperationException){this.文書保存診断="版と成果物を確認できない文書は復元情報に含めません。";return null;}
    }
    private async Task<(CatalogObject? 対象,string? スキーマ,FunctionSummary? 関数)?> 公開復元対象を読むAsync(CatalogDatabase DB,ブラウザー保存文書 参照){
        if(this.権限があるか(DB.Name,ManagementPermission.Edit)){this.文書復元案内.Add("公開文書の表示権限が変わりました。対象を選び直してください。");return null;}
        await this.公開DBを読むAsync(DB);
        if(!this.文書復元を続けられるか()||!this.権限があるか(DB.Name,ManagementPermission.View)){return null;}
        var 公開=this.公開DB一覧[DB.Id];FunctionSummary? 関数=null;CatalogObject? 対象=null;string? スキーマ=null;
        if(参照.Kind=="RegisteredFunction"){
            関数=this.登録関数一覧.GetValueOrDefault(DB.Id,[]).SingleOrDefault(関数=>関数.Target.DefinitionId==参照.DefinitionId&&"registered:"+関数.Target.DefinitionId==参照.ObjectId&&関数.Version==参照.Version&&関数.ArtifactHash==参照.ArtifactHash);
            if(関数 is null){this.文書復元案内.Add("定義版・成果物または下書きが変わった文書は復元しませんでした。");return null;}
        }else if(参照.DefinitionId!=DB.DefinitionId||参照.Version!=DB.Version||参照.ArtifactHash!=DB.ArtifactHash){this.文書復元案内.Add("定義版・成果物または下書きが変わった文書は復元しませんでした。");return null;}
        if(参照.Kind=="Schema"){
            if(!公開.スキーマ.Contains(参照.Schema,StringComparer.Ordinal)){this.文書復元案内.Add("保存された文書の対象が見つからないため、復元しませんでした。");return null;}スキーマ=参照.Schema;
        }else if(参照.Kind!="Database"){
            対象=this.公開ツリー項目(公開).SingleOrDefault(項目=>項目.Id==参照.ObjectId&&項目.Kind==参照.Kind&&項目.Schema==参照.Schema);
            if(対象 is null){this.文書復元案内.Add("保存された文書の対象が見つからないため、復元しませんでした。");return null;}
        }
        return(対象,スキーマ,関数);
    }
}
