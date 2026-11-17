namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly List<(int 位置,ブラウザー保存文書 参照)> 文書復元待ち=[];
    private readonly SortedDictionary<int,string> 復元文書ID=[];
    private readonly HashSet<string> 文書復元案内=new(StringComparer.Ordinal);
    private int? 復元選択位置;
    private string? 文書復元操作印,文書保存診断;
    private bool 復元タブを準備したか,文書復元要求か;
    private string 現在の文書操作印()=>System.Text.Json.JsonSerializer.Serialize(new{this.対象選択世代,this.選択作業文書ID,this.選択DBID,表示=this.現在対象文書?.表示,順序=this.タブ順序.順序.ToArray(),this.ロール表示か,this.設定表示か,this.利用者管理表示か,this.監査表示か,this.プロジェクト表示か});
    private void 文書復元を準備(ブラウザー表示状態 状態){
        if(状態.Documents is null){return;}
        this.文書復元待ち.Clear();this.復元文書ID.Clear();this.復元タブを準備したか=false;
        this.文書復元待ち.AddRange(状態.Documents.Select((参照,位置)=>(位置,参照)));this.復元選択位置=状態.SelectedDocument;this.文書復元操作印=this.現在の文書操作印();this.文書復元要求か=true;
    }
    private bool 文書復元を続けられるか()=>!this.破棄済みか&&this.認証有効か&&this.利用者 is{Disabled:false} 利用者&&this.表示状態インスタンスID!=Guid.Empty&&this.表示状態キー==ブラウザー表示状態.保存キー(this.表示状態インスタンスID,利用者)&&this.文書復元操作印==this.現在の文書操作印()&&!this.未保管の作業があるか;
    private bool 文書参照権限があるか(CatalogDatabase DB,ブラウザー保存文書 参照){
        if(参照.PublishedOnly){return this.権限があるか(DB.Name,ManagementPermission.View);}
        if(!this.権限があるか(DB.Name,ManagementPermission.View)||!this.権限があるか(DB.Name,ManagementPermission.Edit)){return false;}
        if(参照.Kind is "Table" or "Set"&&参照.View=="データ"&&!this.権限があるか(DB.Name,ManagementPermission.ReadRows)){return false;}
        if(参照.View is "アクセス履歴" or "実行履歴"&&!this.権限があるか(DB.Name,ManagementPermission.Manage)){return false;}
        return true;
    }
    private async Task<bool> 保存文書を復元Async(){
        if(!this.文書復元要求か){return false;}
        if(this.文書復元待ち.Count==0){
            if(this.文書復元を続けられるか()){foreach(var ID in this.タブ順序.順序.ToArray()){this.タブ順序.削除(ID);}this.選択作業文書ID=null;this.選択DBID=null;}
            else{this.文書復元案内.Add("画面操作または未保存の変更があるため、残りの文書復元を中止しました。");}
            this.文書復元要求か=false;return true;
        }
        var 変更したか=false;
        foreach(var 待ち in this.文書復元待ち.ToArray()){
            if(!this.文書復元を続けられるか()){
                this.文書復元案内.Add("画面操作または未保存の変更があるため、残りの文書復元を中止しました。");this.文書復元待ち.Clear();this.文書復元要求か=false;return true;
            }
            var 参照=待ち.参照;var DB=this.DB一覧.FirstOrDefault(DB=>DB.Id==参照.DatabaseId);
            if(DB is null&&this.次ページがあるか){continue;}
            this.文書復元待ち.Remove(待ち);変更したか=true;
            if(DB is null){this.文書復元案内.Add("保存された文書のDBが見つからないため、復元しませんでした。");continue;}
            if(!参照.DBが一致するか(DB)){this.文書復元案内.Add("定義版・成果物または下書きが変わった文書は復元しませんでした。");continue;}
            if(!this.文書参照権限があるか(DB,参照)){this.文書復元案内.Add("文書の参照権限が不足しているため、復元しませんでした。");continue;}
            try{
                if(参照.PublishedOnly){
                    var 公開対象=await this.公開復元対象を読むAsync(DB,参照);
                    if(!this.文書復元を続けられるか()){this.文書復元案内.Add("画面操作または未保存の変更があるため、残りの文書復元を中止しました。");this.文書復元待ち.Clear();this.文書復元要求か=false;return true;}
                    if(公開対象 is{} 復元対象){var 公開文書=this.対象文書を取得(DB.Id,復元対象.対象,復元対象.スキーマ,復元対象.関数);公開文書.表示=参照.View;this.復元文書を追加(待ち.位置,公開文書);}continue;
                }
                await this.DB文書を読み込むAsync(DB);
                if(!this.文書復元を続けられるか()){this.文書復元案内.Add("画面操作または未保存の変更があるため、残りの文書復元を中止しました。");this.文書復元待ち.Clear();this.文書復元要求か=false;return true;}
                var 定義文書=this.文書一覧[DB.Id];FunctionSummary? 関数=null;
                if(参照.Kind=="RegisteredFunction"){
                    関数=this.登録関数一覧.GetValueOrDefault(DB.Id,[]).SingleOrDefault(関数=>関数.Target.DefinitionId==参照.DefinitionId&&"registered:"+関数.Target.DefinitionId==参照.ObjectId&&関数.Schema==参照.Schema);
                    if(関数 is null){this.文書復元案内.Add("保存された文書の対象が見つからないため、復元しませんでした。");continue;}
                    if(!this.関数定義文書.TryGetValue(参照.DefinitionId,out 定義文書)){
                        var 公開=await this.管理操作.GetDefinitionAsync(参照.DefinitionId,this.寿命.Token);var 下書き=await this.管理操作.FindDraftAsync(参照.DefinitionId,this.寿命.Token);
                        if(!this.文書復元を続けられるか()){this.文書復元案内.Add("画面操作または未保存の変更があるため、残りの文書復元を中止しました。");this.文書復元待ち.Clear();this.文書復元要求か=false;return true;}
                        定義文書=new(DB,公開,下書き);this.関数定義文書.Add(参照.DefinitionId,定義文書);
                    }
                }
                var 版=DB.Version;var 成果物=DB.ArtifactHash;if(関数 is not null){版=関数.Version;成果物=関数.ArtifactHash;}
                if(!参照.定義が一致するか(定義文書,版,成果物)){this.文書復元案内.Add("定義版・成果物または下書きが変わった文書は復元しませんでした。");continue;}
                CatalogObject? 対象=null;string? スキーマ=null;
                if(参照.Kind=="Schema"){
                    if(!定義文書.スキーマ.Contains(参照.Schema,StringComparer.Ordinal)){this.文書復元案内.Add("保存された文書の対象が見つからないため、復元しませんでした。");continue;}スキーマ=参照.Schema;
                }else if(参照.Kind!="Database"){
                    対象=this.ツリーオブジェクト(this.文書一覧[DB.Id]).SingleOrDefault(項目=>項目.Id==参照.ObjectId&&項目.Kind==参照.Kind&&項目.Schema==参照.Schema);
                    if(対象 is null){this.文書復元案内.Add("保存された文書の対象が見つからないため、復元しませんでした。");continue;}
                }
                if(!this.文書参照権限があるか(DB,参照)){this.文書復元案内.Add("文書の参照権限が不足しているため、復元しませんでした。");continue;}
                var 文書=this.対象文書を取得(DB.Id,対象,スキーマ,関数);文書.表示=参照.View;
                this.復元文書を追加(待ち.位置,文書);
            }catch(ManagementException 例外){
                if(例外.Code is "Permission" or "Unauthorized" or "Forbidden"){this.文書復元案内.Add("文書の参照権限が不足しているため、復元しませんでした。");}
                else{this.文書復元案内.Add("保存された文書を取得できませんでした。対象を選び直してください。");}
            }
        }
        if(this.文書復元待ち.Count==0&&this.復元タブを準備したか&&this.文書復元を続けられるか()){
            foreach(var ID in this.タブ順序.順序.ToArray()){this.タブ順序.削除(ID);}foreach(var ID in this.復元文書ID.Values){this.タブ順序.追加(ID);}
            if(this.復元選択位置 is{} 位置&&this.復元文書ID.TryGetValue(位置,out var 選択ID)){this.作業文書を選ぶ(選択ID);}
            else{this.選択作業文書ID=null;this.選択DBID=null;if(this.復元選択位置 is not null){this.文書復元案内.Add("選択されていた文書を復元できませんでした。対象を選び直してください。");}}
        }
        if(this.文書復元待ち.Count==0){this.文書復元要求か=false;}
        return 変更したか;
    }
    private void 復元文書を追加(int 位置,対象文書状態 文書){
        if(!this.復元タブを準備したか){foreach(var ID in this.タブ順序.順序.ToArray()){this.タブ順序.削除(ID);}this.復元タブを準備したか=true;}
        this.復元文書ID.Add(位置,文書.ID);this.タブ順序.追加(文書.ID);this.文書復元操作印=this.現在の文書操作印();
    }
    private (ブラウザー保存文書[] 文書,int? 選択)? 保存文書を取得(){
        if(this.文書復元待ち.Count>0){return null;}
        var 一覧=new List<ブラウザー保存文書>();int? 選択=null;this.文書保存診断=null;
        foreach(var ID in this.タブ順序.順序){
            if(this.対象文書一覧.TryGetValue(ID,out var 公開対象)&&this.公開情報表示か(公開対象.DBID)){
                var 公開参照=this.公開文書参照(公開対象);if(公開参照 is not null){if(ID==this.選択作業文書ID){選択=一覧.Count;}一覧.Add(公開参照);}continue;
            }
            if(!this.対象文書一覧.TryGetValue(ID,out var 対象)||!this.文書一覧.TryGetValue(対象.DBID,out var DB文書)){continue;}
            var DB=this.DB一覧.FirstOrDefault(DB=>DB.Id==対象.DBID);if(DB is null||DB.IsSystem){continue;}
            var 文書=this.対象の定義文書(対象);
            if(対象.編集中か||対象.データ編集中か||文書.未保管か||文書.未確定操作 is not null||this.DBツリー画面.TryGetValue(DB.Id,out var 木)&&木.未保存か){this.文書保存診断="未保存の変更がある文書は復元情報に含めません。";continue;}
            var 版=DB.Version;var 成果物=DB.ArtifactHash;if(対象.登録関数か){版=対象.関数!.Version;成果物=対象.関数.ArtifactHash;}
            var 種類=対象.対象?.Kind??"Database";if(対象.スキーマか){種類="Schema";}
            var 表示=対象.表示;if(対象.対象 is null){表示="DB情報";}
            var 参照=new ブラウザー保存文書(DB.Id,DB.Version,DB.ArtifactHash,文書.定義.Id,版,成果物,文書.定義.Hash(),種類,対象.対象?.Id??"",対象.スキーマ??対象.対象?.Schema??"",表示);
            try{参照.検証();}catch(InvalidOperationException){this.文書保存診断="版と成果物を確認できない文書は復元情報に含めません。";continue;}
            if(!参照.定義が一致するか(文書,版,成果物)||!this.文書参照権限があるか(DB,参照)){continue;}
            if(ID==this.選択作業文書ID){選択=一覧.Count;}一覧.Add(参照);
        }
        if(一覧.Count>32){this.文書保存診断="文書の復元情報は32件まで保存できます。タブを閉じてください。";return null;}
        return(一覧.ToArray(),選択);
    }
}
