namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly Dictionary<string,対象文書状態> 対象文書一覧=new(StringComparer.Ordinal);
    private readonly Dictionary<string,文書状態> 関数定義文書=new(StringComparer.Ordinal);
    private readonly Dictionary<string,FunctionSummary[]> 登録関数一覧=new(StringComparer.Ordinal);
    private readonly Dictionary<string,対象文書ビュー> 対象画面=new(StringComparer.Ordinal);
    private string? 選択作業文書ID;
    private bool 資源計測対応か;
    private long 対象選択世代;
    private IEnumerable<文書状態> 全定義文書=>this.文書一覧.Values.Concat(this.関数定義文書.Values);
    private IEnumerable<string> 表示文書タブ()=>this.タブ順序.順序.Where(ID=>this.対象文書一覧.TryGetValue(ID,out var 文書)&&!文書.DBか&&(this.文書一覧.ContainsKey(文書.DBID)||this.公開情報表示か(文書.DBID)));
    private bool DBに変更があるか(string ID){
        if(this.DBツリー画面.GetValueOrDefault(ID)?.未保存か is true||this.対象文書一覧.Values.Any(文書=>文書.DBID==ID&&(文書.編集中か||文書.データ編集中か))){return true;}
        if(this.公開情報表示か(ID)){return false;}
        return this.文書一覧.GetValueOrDefault(ID)?.未適用か is true||this.関数定義文書.Values.Any(文書=>文書.DB.Id==ID&&文書.未適用か);
    }
    private static long? 適用前の版(文書状態 文書){if(文書.未適用か&&文書.下書き版>0){return 文書.下書き版;}return null;}
    private string 対象の保存状態(文書状態 文書){if(this.対象文書一覧.Values.Any(対象=>対象.定義ID==文書.定義.Id&&対象.編集中か)){return "未保管の変更";}return 文書.状態;}
    private async Task 対象表示を開くAsync(string 名前,string 表示){
        if(this.現在文書?.オブジェクト.FirstOrDefault(項目=>項目.Id==名前) is not{} 項目){return;}
        this.対象を文書で開く(項目,表示);
        if(項目.Kind=="Table"&&表示=="構造"){return;}
        if(this.現在対象文書 is{} 文書){
            if(this.対象画面.TryGetValue(文書.ID,out var 画面)){await 画面.表示変更時Async(表示);}else{文書.表示=表示;}
        }
    }
    private 対象文書状態? 現在対象文書{get{if(this.選択作業文書ID is{} ID&&this.対象文書一覧.TryGetValue(ID,out var 文書)){return 文書;}return null;}}
    private 文書状態 対象の定義文書(対象文書状態 対象){if(対象.登録関数か){return this.関数定義文書[対象.定義ID];}return this.文書一覧[対象.DBID];}
    private 対象文書状態 対象文書を取得(string DBID,CatalogObject? 対象=null,string? スキーマ=null,FunctionSummary? 関数=null){
        var 定義ID=this.文書一覧.GetValueOrDefault(DBID)?.定義.Id;if(this.公開情報表示か(DBID)){定義ID=this.公開DB一覧[DBID].DB.DefinitionId;}if(定義ID is null){throw new ManagementException("NotFound",表示文言.取得("データベースがありません。"));}if(関数?.Target.Origin==FunctionOrigin.RegisteredEntry){定義ID=関数.Target.DefinitionId;}
        var 文書=this.対象文書一覧.Values.FirstOrDefault(文書=>文書.一致するか(DBID,定義ID,対象,スキーマ));
        if(文書 is null){
            string? ID=null;if(対象 is null&&スキーマ is null){ID=DBID;}
            文書=new(DBID,定義ID,対象,スキーマ,ID);this.対象文書一覧.Add(文書.ID,文書);
        }
        文書.関数=関数;return 文書;
    }
    private void 作業文書を選ぶ(string ID){
        if(!this.対象文書一覧.TryGetValue(ID,out var 対象)){return;}
        this.文書を選ぶ(対象.DBID);this.選択作業文書ID=対象.ID;
        this.ツリー所属選択中か=対象.対象 is null;this.選択スキーマ名=対象.スキーマ??対象.対象?.Schema??"";
        if(対象.対象?.Kind is "Table" or "Set"){this.選択表名=対象.対象.Id;this.実行問い合わせ=null;}
        else if(対象.対象 is not null){this.選択表名=null;}
        this.タブ順序.追加(ID);
    }
    private void 対象を文書で開く(CatalogObject 項目,string 表示="データ"){
        if(this.選択DBID is not{} ID||!this.文書一覧.ContainsKey(ID)&&!this.公開情報表示か(ID)){return;}
        var 対象=this.対象文書を取得(ID,項目);var 初回=!this.タブ順序.順序.Contains(対象.ID);
        this.作業文書を選ぶ(対象.ID);
        if(項目.Kind=="Table"&&表示=="構造"){this.表の列を開く(ID,項目);}
        else if(初回){対象.表示=表示;}
    }
    private async Task 登録関数を開くAsync(string DBID,CatalogObject 項目){
        var 世代=++this.対象選択世代;
        var 関数=this.登録関数一覧.GetValueOrDefault(DBID,[]).Single(関数=>"registered:"+関数.Target.DefinitionId==項目.Id);
        if(!this.関数定義文書.ContainsKey(関数.Target.DefinitionId)){
            var 公開=await this.管理操作.GetDefinitionAsync(関数.Target.DefinitionId,this.寿命.Token);
            var 下書き=await this.管理操作.FindDraftAsync(関数.Target.DefinitionId,this.寿命.Token);
            if(this.破棄済みか||世代!=this.対象選択世代){return;}
            this.関数定義文書[関数.Target.DefinitionId]=new(this.文書一覧[DBID].DB,公開,下書き);
        }
        if(this.破棄済みか||世代!=this.対象選択世代){return;}
        var 文書=this.対象文書を取得(DBID,項目,関数:関数);this.作業文書を選ぶ(文書.ID);
    }
    private async Task 登録関数表示時Async(管理対象 対象,string 表示){
        if(this.公開情報表示か(対象.ID)){
            if(表示!="データ"){throw new ManagementException("Permission",表示文言.取得("権限がありません。"));}
            var 公開項目=this.公開ツリー項目(this.公開DB一覧[対象.ID]).Single(項目=>項目.Id==対象.表);this.公開対象を開く(対象.ID,公開項目);return;
        }
        var 項目=this.ツリーオブジェクト(this.文書一覧[対象.ID]).Single(項目=>項目.Id==対象.表);
        await this.登録関数を開くAsync(対象.ID,項目);
        if(this.現在対象文書 is not{} 現在||現在.DBID!=対象.ID||現在.対象?.Id!=項目.Id){return;}
        if(this.現在対象文書 is{} 文書){if(this.対象画面.TryGetValue(文書.ID,out var 画面)){await 画面.表示変更時Async(表示);}else{文書.表示=表示;}}
    }
    private async Task 登録関数を読むAsync(CatalogDatabase DB){
        try{
            var 一覧=new List<FunctionSummary>();long 次=0;var 版="";
            do{
                var 頁=await this.管理操作.FunctionsAsync(new(DB.Id,版,次),this.寿命.Token);
                一覧.AddRange(頁.Items.Where(関数=>関数.Target.Origin==FunctionOrigin.RegisteredEntry));
                if(!頁.HasMore){break;}次=頁.NextCursor;版=頁.VersionToken;
            }while(true);
            this.登録関数一覧[DB.Id]=一覧.ToArray();
        }catch(ManagementException 例外)when(例外.Code=="Unsupported"){this.登録関数一覧[DB.Id]=[];}
    }
    private CatalogObject[] ツリーオブジェクト(文書状態 文書)=>[..文書.オブジェクト,..this.登録関数一覧.GetValueOrDefault(文書.DB.Id,[]).Select(関数=>new CatalogObject("registered:"+関数.Target.DefinitionId,関数.Name,"","RegisteredFunction",""))];
    private bool 対象公開済みか(対象文書状態 対象){if(対象.登録関数か){return true;}return this.文書一覧[対象.DBID].公開済みか(対象.対象!.Kind,対象.対象.Id);}
    private Task 対象定義変更時Async(対象文書状態 対象,Definition 定義){var 文書=this.対象の定義文書(対象);文書.定義=定義;文書.プレビュー=null;return Task.CompletedTask;}
    private async Task 定義編集を同期Async(文書状態 文書){
        foreach(var 対象 in this.対象文書一覧.Values.Where(対象=>対象.定義ID==文書.定義.Id&&対象.編集中か)){
            if(this.対象画面.TryGetValue(対象.ID,out var 画面)&&!await 画面.同期Async()){
                this.作業文書を選ぶ(対象.ID);throw new ManagementException("Validation",表示文言.取得("C#の入力を確認してください。"));
            }
        }
    }
    private void 作業文書を閉じる(string ID){
        this.タブ順序.削除(ID);
        var 次=this.タブ順序.順序.LastOrDefault();
        if(次 is not null){this.作業文書を選ぶ(次);}else{this.選択作業文書ID=null;this.選択DBID=null;}
        this.案内=表示文言.取得("閉じた文書の作業はこの画面内に保持しています。");
    }
}
