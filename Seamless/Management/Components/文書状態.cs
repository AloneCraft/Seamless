namespace Seamless.Management.Components;

internal sealed class 文書状態{
    public CatalogDatabase DB{get;private set;}
    public void DB情報を更新(CatalogDatabase 情報){
        if(情報.Id!=this.DB.Id){throw new ArgumentException("異なるDBの情報です。",nameof(情報));}
        this.DB=情報;
    }
    public Definition 定義{get;set;}
    private Definition 公開定義;
    private HashSet<(string 種類,string ID)>? 公開オブジェクト;
    public bool 公開済みか(string 種類,string ID){
        this.公開オブジェクト??=DatabaseDesigner.Objects(this.公開定義).Select(項目=>(項目.Kind,項目.Id)).ToHashSet();
        return this.公開オブジェクト.Contains((種類,ID));
    }
    private string 公開ハッシュ;
    private string 保存済みハッシュ;
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
    public bool 未適用か=>this.定義.Hash()!=this.公開ハッシュ;
    public bool 未保管か=>this.定義.Hash()!=this.保存済みハッシュ;
    public string コード=>this.定義.Source.Files.First(ファイル=>ファイル.Path==this.選択ファイル).Text;
    public string 状態{
        get{
            if(this.未保管か){return "未保管の変更";}
            if(this.未適用か){return "下書き · 稼働DBへ未適用";}
            return "すべての変更を公開済み";
        }
    }
    public 文書状態(CatalogDatabase DB,DefinitionVersion 公開,Draft? 下書き){
        this.DB=DB;
        this.定義=公開.Definition.Freeze() with{Migration=null};
        this.公開定義=this.定義;
        this.基準版=公開.Version;
        this.公開ハッシュ=this.定義.Hash();
        if(下書き is not null){
            this.下書き版=下書き.Revision;
            if(下書き.Definition.Hash()!=公開.Definition.Hash()){
                this.定義=下書き.Definition.Freeze();
                this.基準版=下書き.BaseVersion;
            }
        }
        this.保存済みハッシュ=this.定義.Hash();
        this.選択ファイル=this.定義.Source.Files[0].Path;
    }
    public void 保管完了(Definition 保存対象,Draft 結果){
        if(ReferenceEquals(this.定義,保存対象)){this.定義=結果.Definition.Freeze();}
        this.下書き版=結果.Revision;
        this.保存済みハッシュ=結果.Definition.Hash();
        this.プレビュー=null;
    }
    public bool プレビューを設定(Definition 検証対象,ChangePreview 結果){
        if(!ReferenceEquals(this.定義,検証対象)||結果.BundleHash!=this.検証キー.BundleHash){return false;}
        this.プレビュー=結果;
        return true;
    }
    public void 公開完了(long 版){
        this.未確定操作=null;
        this.基準版=版;
        // 適用済みの移行設定を次の編集へ持ち越すと、既存の表を新規表として扱ってしまう。
        this.定義=this.定義 with{Migration=null};
        this.公開定義=this.定義;this.公開オブジェクト=null;
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
