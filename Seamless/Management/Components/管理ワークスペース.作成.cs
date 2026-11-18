using Microsoft.AspNetCore.Components;

namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    internal static string 新規ビュー本体(string 名前)=>
        "// 例: Items と Id を実際のテーブル名・列名に置き換え、この return を使います。\n"+
        "// return q.Items.Where(row => row.Id > 0).Select(row => new @"+名前+"(row.Id));\n"+
        "return global::Seamless.Linq.Set.Empty<@"+名前+">();";
    private string 選択スキーマ名="";
    private string? スキーマ作成DBID;
    private bool スキーマ入力中か;
    private QueryDesign? 問い合わせ入力;
    private QueryDesign? 実行問い合わせ;
    private static bool 追加項目か(管理対象 対象,string 種類)=>(対象.種類 is "作成" or "DB" or "スキーマ" or "オブジェクト分類")&&(対象.追加種類 is null||対象.追加種類==種類);
    private bool 作成できるか(管理対象 対象)=>this.認証有効か&&this.対象文書(対象) is{未確定操作:null} 文書&&this.権限があるか(文書.DB.Name,ManagementPermission.Edit);
    private void 作成入力を閉じる(){if(this.新規作成入力 is not null){return;}this.表入力中か=false;this.スキーマ入力中か=false;this.スキーマ作成DBID=null;this.問い合わせ入力=null;}
    private Task 追加メニュー要求時Async(対象操作要求 要求)=>this.メニュー要求時Async(要求);
    private void スキーマ作成開始時(管理対象 対象){
        this.別の作成入力を確認();
        this.DBツリー状態(対象.ID).展開中か=true;
        this.文書を選ぶ(対象.ID);this.作成入力を閉じる();this.DB名編集中か=false;this.表示=表示種別.構造;this.スキーマ作成DBID=対象.ID;this.スキーマ入力中か=true;
    }
    private void スキーマ名確定時(string 名前){
        if(!this.スキーマ入力中か||this.スキーマ作成DBID is null||!this.文書一覧.TryGetValue(this.スキーマ作成DBID,out var 文書)){return;}
        if(string.IsNullOrWhiteSpace(名前)){this.作成入力を閉じる();return;}
        if(文書.未確定操作 is not null||!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)){this.案内=表示文言.取得("スキーマを作成する権限または操作状態を確認してください。");this.エラーか=true;return;}
        try{文書.定義=DatabaseDesigner.AddSchema(文書.定義,名前);文書.プレビュー=null;this.選択スキーマ名=名前;this.スキーマ入力中か=false;this.スキーマ作成DBID=null;this.案内=表示文言.取得("スキーマを下書きへ反映しました。");this.エラーか=false;}
        catch(ManagementException 例外){this.案内=例外.Message;this.エラーか=true;}
    }
    private void スキーマを選ぶ(string 名前){this.ツリー所属選択中か=true;this.作成入力を閉じる();this.選択スキーマ名=名前;this.表示=表示種別.構造;this.プロジェクト表示か=false;if(this.選択DBID is{} DBID&&(this.文書一覧.ContainsKey(DBID)||this.公開情報表示か(DBID))){対象文書状態 文書;if(名前.Length==0){文書=this.対象文書を取得(DBID);}else{文書=this.対象文書を取得(DBID,スキーマ:名前);}this.選択作業文書ID=文書.ID;this.タブ順序.追加(文書.ID);}}
    private void DB直下を選ぶ時()=>this.スキーマを選ぶ("");
    private static string 問い合わせ種類名(string 種類){if(種類=="View"){return "ビュー";}return "関数";}
    private void オブジェクトを選ぶ時(CatalogObject 項目){
        if(this.選択DBID is{} ID&&this.公開情報表示か(ID)){this.公開対象を開く(ID,項目);return;}
        this.選択スキーマ名=項目.Schema;
        this.計画対象=null;this.計画対象名=null;
        if(項目.Kind is "Table" or "Set"){this.実行問い合わせ=null;this.表データ表示時(項目.Id);}
        else if(this.現在文書 is{} 文書){this.選択表名=null;this.問い合わせ表示時(文書,項目.Id);}
    }
    private void クエリ一覧表示時(){this.選択表名=null;this.実行問い合わせ=null;this.計画対象=null;this.計画対象名=null;this.表示=表示種別.クエリ一覧;}
    private void クエリ計画表示時(){
        if(this.現在文書 is not{} 文書||this.実行問い合わせ is not{} 問い合わせ){return;}
        if(文書.未保管か){this.案内=表示文言.取得("実行計画を取得する前に下書きを保存してください。");this.エラーか=true;return;}
        long? 下書き版=null;
        if(文書.未適用か){下書き版=文書.下書き版;}
        this.計画対象=new(文書.定義.Id,問い合わせ.Path,文書.基準版,下書き版,文書.選択成果物?.BundleHash);
        this.計画対象名=問い合わせ.Path;this.表示=表示種別.計画;
    }
    private void 公開クエリ選択時(DefinitionSummary クエリ){
        this.選択トレース実行ID=null;
        this.実行問い合わせ=null;this.選択表名=null;
        this.計画対象=new(クエリ.Definition.Id,クエリ.Definition.Id,クエリ.Version);
        this.計画対象名=クエリ.Definition.EntryType+"."+クエリ.Definition.EntryMember;
        this.表示=表示種別.計画;
    }
    private void 問い合わせ表示時(文書状態 文書,string 名前){
        this.選択トレース実行ID=null;
        try{this.実行問い合わせ=DatabaseDesigner.ReadQuery(文書.定義,名前);this.ツリー所属選択中か=false;this.選択表名=null;this.計画対象=null;this.計画対象名=null;this.表示=表示種別.問い合わせ;if(文書.オブジェクト.FirstOrDefault(項目=>項目.Id==名前) is{} 項目){this.対象を文書で開く(項目);}}
        catch(ManagementException 例外){this.案内=例外.Message;this.エラーか=true;}
    }
    private void 問い合わせ作成開始時(管理対象 対象,DatabaseQueryKind 種類){
        this.作成入力を閉じる();if(!this.作成場所を開く(対象.ID,対象.スキーマ,種類.ToString())){return;}this.表示=表示種別.構造;this.選択スキーマ名=対象.スキーマ;
        var 名前="NewFunction";if(種類==DatabaseQueryKind.View){名前="NewView";}
        // ビュー・テーブル関数は ISet<行> を返す。本体は利用者が書くまで、呼ぶと分かる例外にしておく。
        var 本体="throw new global::System.NotImplementedException();";
        if(種類==DatabaseQueryKind.View){本体=新規ビュー本体(名前);}
        else if(種類==DatabaseQueryKind.ScalarFunction){本体="return default;";}
        var 戻り型="int";if(種類==DatabaseQueryKind.TableFunction){戻り型="global::Seamless.Collections.ISet<"+名前+"Row>";}
        this.問い合わせ入力=new(名前,対象.スキーマ,種類,[new("Id","int",false)],[],戻り型,本体);
    }
    private void 問い合わせ編集開始時(文書状態 文書,string 名前){
        this.別の作成入力を確認();
        try{this.作成入力を閉じる();this.問い合わせ入力=DatabaseDesigner.ReadQuery(文書.定義,名前);this.選択スキーマ名=this.問い合わせ入力.Schema;this.表示=表示種別.構造;}
        catch(ManagementException 例外){this.案内=例外.Message;this.エラーか=true;}
    }
    private Task 問い合わせを下書きに追加時Async(QueryDesign 設計){
        if(this.現在文書 is not{} 文書||!this.作成先が一致するか(文書)||文書.未確定操作 is not null||!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)){return Task.CompletedTask;}
        try{文書.定義=DatabaseDesigner.ApplyQuery(文書.定義,設計);文書.プレビュー=null;this.新規作成入力=null;this.問い合わせ入力=null;this.案内=表示文言.取得("問い合わせを下書きへ反映しました。");this.エラーか=false;this.対象を文書で開く(文書.オブジェクト.Single(項目=>項目.Id==設計.Path),"構造");}
        catch(ManagementException 例外){this.案内=例外.Message;this.エラーか=true;}
        return Task.CompletedTask;
    }
}
