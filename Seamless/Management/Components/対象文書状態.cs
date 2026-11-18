namespace Seamless.Management.Components;

public sealed class 対象文書状態{
    public string ID{get;}
    public string DBID{get;}
    public string 定義ID{get;}
    public CatalogObject? 対象{get;set;}
    public string? スキーマ{get;}
    public FunctionSummary? 関数{get;set;}
    public string 表示{get;set;}="データ";
    public bool 編集中か{get;set;}
    public bool データ編集中か{get;set;}
    public bool 編集部品を開いたか{get;set;}
    public bool データを開いたか{get;set;}
    public bool 計画を開いたか{get;set;}
    public bool 履歴を開いたか{get;set;}
    public ObjectExecutionStatistics? 実測{get;set;}
    public QueryExecutionObservation? 実績{get;set;}
    public Guid? トレース実行ID{get;set;}
    public QueryPlanRequest? 履歴計画{get;set;}
    public ResourceHeatTrace? 資源記録{get;set;}
    public ObjectExecutionProfile? ノード実測{get;set;}
    public bool DBか=>this.対象 is null&&this.スキーマ is null;
    public bool スキーマか=>this.対象 is null&&this.スキーマ is not null;
    public bool 登録関数か=>this.関数?.Target.Origin==FunctionOrigin.RegisteredEntry;
    public string 名前{get{if(this.登録関数か){return this.関数!.Name;}return this.対象?.Id??this.スキーマ??"";}}
    public string アイコン{get{if(this.DBか){return "DB";}if(this.スキーマか){return "スキーマ";}if(this.対象?.Kind is "Table" or "Set"){return "テーブル";}if(this.対象?.Kind=="View"){return "ビュー";}return "関数";}}
    public 対象文書状態(string DBID,string 定義ID,CatalogObject? 対象=null,string? スキーマ=null,string? ID=null){
        this.ID=ID??Guid.NewGuid().ToString("N");this.DBID=DBID;this.定義ID=定義ID;this.対象=対象;this.スキーマ=スキーマ;
    }
    public bool 一致するか(string DBID,string 定義ID,CatalogObject? 対象,string? スキーマ)=>
        this.DBID==DBID&&this.定義ID==定義ID&&this.対象?.Id==対象?.Id&&this.対象?.Kind==対象?.Kind&&this.スキーマ==スキーマ;
}
