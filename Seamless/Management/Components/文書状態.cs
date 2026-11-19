namespace Seamless.Management.Components;

internal sealed class 文書状態{
    public CatalogDatabase DB{get;private set;}
    public void DB情報を更新(CatalogDatabase 情報){
        if(情報.Id!=this.DB.Id){throw new ArgumentException("異なるDBの情報です。",nameof(情報));}
        this.DB=情報;
    }
    public Definition 定義{get;set;}
    public Definition? 公開定義{get;private set;}
    private CatalogObject[]? 公開オブジェクト;
    private string[]? 公開スキーマ;
    private QueryDraftRename[] 問い合わせ改名=[];
    public QueryDraftRename[]? 保管する問い合わせ改名=>this.基準版>0&&this.定義.Kind==DefinitionKind.Database?this.問い合わせ改名:null;
    public string[] 公開スキーマ一覧=>this.公開スキーマ??=this.公開定義 is{} 公開?DatabaseDesigner.Schemas(公開):[];
    public CatalogObject[] 公開オブジェクト一覧=>this.公開オブジェクト??=this.公開定義 is{} 公開?DatabaseDesigner.Objects(公開):[];
    public CatalogObject? 公開時の項目(CatalogObject 項目){
        var ID=項目.Id;
        if(項目.Kind=="Table"){
            if(this.定義.Migration?.NewTables?.Contains(ID,StringComparer.Ordinal) is true){return null;}
            ID=this.定義.Migration?.Tables.FirstOrDefault(表=>表.Target==ID)?.Source??ID;
        }else{
            if(this.問い合わせ改名.FirstOrDefault(行=>行.Part==QueryDraftRenamePart.Query&&行.Query==ID) is{} 改名){ID=改名.Before;}
            else if(this.問い合わせ改名.Any(行=>行.Part==QueryDraftRenamePart.Query&&行.Before==ID)){return null;}
        }
        return this.公開オブジェクト一覧.FirstOrDefault(旧=>旧.Id==ID&&同種か(旧.Kind,項目.Kind));
    }
    public void 問い合わせ改名を記録(string 元,string 次){
        if(元==次){return;}
        var 公開名=this.問い合わせ改名.FirstOrDefault(行=>行.Part==QueryDraftRenamePart.Query&&行.Query==元)?.Before;
        if(公開名 is null&&!this.問い合わせ改名.Any(行=>行.Part==QueryDraftRenamePart.Query&&行.Before==元)&&
            this.公開オブジェクト一覧.Any(項目=>項目.Id==元&&項目.Kind is "View" or "ScalarFunction" or "TableFunction")){公開名=元;}
        this.問い合わせ改名=this.問い合わせ改名.Where(行=>行.Part!=QueryDraftRenamePart.Query||行.Query!=元)
            .Select(行=>行.Query==元?行 with{Query=次}:行).ToArray();
        if(公開名 is not null&&公開名!=次){this.問い合わせ改名=[..this.問い合わせ改名,new(次,公開名,次,QueryDraftRenamePart.Query)];}
    }
    public void 問い合わせ改名を解除(string ID)=>this.問い合わせ改名=this.問い合わせ改名.Where(行=>行.Query!=ID).ToArray();
    public string? 公開時の問い合わせ列(CatalogObject 項目,bool 引数か,string 名前){
        var 種類=引数か?QueryDraftRenamePart.Parameter:QueryDraftRenamePart.ResultColumn;
        var 行=this.問い合わせ改名.FirstOrDefault(候補=>候補.Query==項目.Id&&候補.Part==種類&&候補.After==名前);
        if(行 is not null){return 行.Before;}
        if(this.問い合わせ改名.Any(候補=>候補.Query==項目.Id&&候補.Part==種類&&候補.Before==名前)){return null;}
        return 名前;
    }
    public void 問い合わせ列の改名を記録(CatalogObject 項目,bool 引数か,string 元,string? 次){
        if(元==次){return;}
        var 元の名前=this.公開時の問い合わせ列(項目,引数か,元);
        var 種類=引数か?QueryDraftRenamePart.Parameter:QueryDraftRenamePart.ResultColumn;
        this.問い合わせ改名=this.問い合わせ改名.Where(行=>行.Query!=項目.Id||行.Part!=種類||行.After!=元).ToArray();
        if(次 is null||元の名前 is null||元の名前==次||this.公開定義 is not{} 公開||this.公開時の項目(項目) is not{} 公開項目){return;}
        try{
            var 設計=DatabaseDesigner.ReadObjectDefinition(公開,公開項目);
            if(引数か?設計.Parameters.Any(列=>列.Name==元の名前):設計.Columns.Any(列=>列.Name==元の名前)){
                this.問い合わせ改名=[..this.問い合わせ改名,new(項目.Id,元の名前,次,種類)];
            }
        }catch(ManagementException){/* 比較できない公開定義には元の列を割り当てない。 */}
    }
    public CatalogObject[] 削除したオブジェクト(){
        var 残す=this.オブジェクト.Select(this.公開時の項目).ToHashSet();
        return this.公開オブジェクト一覧.Where(項目=>!残す.Contains(項目)).ToArray();
    }
    private static bool 同種か(string 左,string 右)=>左==右||(左 is "ScalarFunction" or "TableFunction")&&(右 is "ScalarFunction" or "TableFunction");
    public bool 公開済みか(string 種類,string ID){
        return this.公開オブジェクト一覧.Any(項目=>項目.Kind==種類&&項目.Id==ID);
    }
    private string 公開ハッシュ;
    private string 保存済みハッシュ;
    private DatabaseDraftChange? 保存済みDB変更;
    public DatabaseDraftChange? DB変更{get;set;}
    public long 下書き版{get;private set;}
    public long 基準版{get;private set;}
    public string 選択ファイル{get;set;}
    private ChangePreview? 確認内容;
    public ChangePreview? プレビュー{
        get=>this.確認内容;
        set{this.確認内容=value;this.データ削除確認済みか=false;}
    }
    public bool データ削除確認済みか{get;set;}
    public ApplyRequest? 未確定操作{get;set;}
    private ImportedSourceBuild? 取込済み成果物;
    public SourceProjectBuild? 選択成果物{
        get{
            var 成果物=this.取込済み成果物?.Build;
            if(成果物 is not null&&成果物.DefinitionId==this.定義.Id&&成果物.DraftRevision==this.下書き版&&成果物.BaseVersion==this.基準版&&成果物.DefinitionHash==this.定義.Hash()&&!this.未保管か){return 成果物;}
            return null;
        }
    }
    public DraftKey 検証キー=>new(this.定義.Id,this.下書き版){BundleHash=this.選択成果物?.BundleHash};
    public bool 成果物を選択(ImportedSourceBuild 成果物){
        var 前=this.取込済み成果物;this.取込済み成果物=成果物;
        if(this.選択成果物 is null||this.未確定操作 is not null){this.取込済み成果物=前;return false;}
        this.プレビュー=null;return true;
    }
    public bool 未適用か=>this.基準版==0||this.DB変更 is not null||this.定義.Hash()!=this.公開ハッシュ;
    public bool 未保管か=>this.DB変更!=this.保存済みDB変更||this.定義.Hash()!=this.保存済みハッシュ;
    public string コード=>this.定義.Source.Files.First(ファイル=>ファイル.Path==this.選択ファイル).Text;
    public string 状態{
        get{
            if(this.未保管か){return "未保管の変更";}
            if(this.未適用か){return "下書き · 稼働DBへ未適用";}
            return "すべての変更を公開済み";
        }
    }
    public 文書状態(CatalogDatabase DB,DefinitionVersion? 公開,Draft? 下書き){
        this.DB=DB;
        this.定義=公開 is null?下書き!.Definition.Freeze():公開.Definition.Freeze() with{Migration=null};
        this.公開定義=公開 is null?null:this.定義;
        this.基準版=公開?.Version??0;
        this.公開ハッシュ=公開 is null?"":this.定義.Hash();
        if(下書き is not null){
            this.下書き版=下書き.Revision;
            if(公開 is null||下書き.Definition.Hash()!=公開.Definition.Hash()){
                this.定義=下書き.Definition.Freeze();
                this.基準版=下書き.BaseVersion;
            }
            this.DB変更=下書き.DatabaseChange;
            if(公開?.Version==下書き.BaseVersion){this.問い合わせ改名=下書き.QueryRenames??[];}
        }
        this.保存済みDB変更=this.DB変更;
        this.保存済みハッシュ=公開 is null&&this.下書き版==0?"":this.定義.Hash();
        this.選択ファイル=this.定義.Source.Files[0].Path;
    }
    public void 保管完了(Definition 保存対象,Draft 結果){
        if(ReferenceEquals(this.定義,保存対象)){this.定義=結果.Definition.Freeze();}
        this.下書き版=結果.Revision;
        this.保存済みハッシュ=結果.Definition.Hash();
        this.保存済みDB変更=結果.DatabaseChange;
        this.プレビュー=null;
    }
    internal void 管理ソースの下書きを反映(Draft 下書き){
        this.保管完了(this.定義,下書き);
        this.基準版=下書き.BaseVersion;
        this.DB変更=下書き.DatabaseChange;
        this.問い合わせ改名=下書き.QueryRenames??[];
        if(!this.定義.Source.Files.Any(ファイル=>ファイル.Path==this.選択ファイル)){this.選択ファイル=this.定義.Source.Files[0].Path;}
    }
    public bool プレビューを設定(Definition 検証対象,ChangePreview 結果,bool 確認時ビルドか=false){
        if(!ReferenceEquals(this.定義,検証対象)||!確認時ビルドか&&結果.BundleHash!=this.検証キー.BundleHash){return false;}
        this.プレビュー=結果;
        return true;
    }
    public void 公開完了(long 版){
        this.未確定操作=null;
        this.基準版=版;
        this.DB変更=null;this.保存済みDB変更=null;
        // 適用済みの移行設定を次の編集へ持ち越すと、既存の表を新規表として扱ってしまう。
        this.定義=this.定義 with{Migration=null};
        this.公開定義=this.定義;this.公開オブジェクト=null;this.公開スキーマ=null;this.問い合わせ改名=[];
        this.公開ハッシュ=this.定義.Hash();
        this.保存済みハッシュ=this.公開ハッシュ;
        this.プレビュー=null;
        this.取込済み成果物=null;
    }
    private Definition? 解析した定義;
    private string[] 表一覧=[];
    private string[] スキーマ一覧=[];
    private CatalogObject[] オブジェクト一覧=[];
    public string[] スキーマ{get{this.構造を解析();return this.スキーマ一覧;}}
    public CatalogObject[] オブジェクト{get{this.構造を解析();return this.オブジェクト一覧;}}
    public string? 構造診断{get;private set;}
    public string[] テーブル{
        get{
            this.構造を解析();
            return this.表一覧;
        }
    }
    private void 構造を解析(){
        if(ReferenceEquals(this.解析した定義,this.定義)){return;}
        this.解析した定義=this.定義;this.構造診断=null;
        try{this.オブジェクト一覧=DatabaseDesigner.Objects(this.定義);this.スキーマ一覧=DatabaseDesigner.Schemas(this.定義);this.表一覧=this.オブジェクト一覧.Where(項目=>項目.Kind=="Table").Select(項目=>項目.Id).ToArray();}
        catch(ManagementException 例外){this.表一覧=[];this.スキーマ一覧=[];this.オブジェクト一覧=[];this.構造診断=例外.Message;}
    }
}
