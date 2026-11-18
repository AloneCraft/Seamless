namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly Dictionary<string,DBオブジェクトツリー> DBツリー画面=new(StringComparer.Ordinal);
    private void 左の構造を開く(string DBID,CatalogObject 対象){
        var 状態=this.DBツリー状態(DBID);状態.展開中か=true;
        if(対象.Schema.Length>0){状態.スキーマを開閉(対象.Schema,true);}
        var 分類=対象.Kind is "ScalarFunction" or "TableFunction"?"Function":対象.Kind;
        状態.分類を開閉(対象.Schema,分類,true);状態.テーブルを開閉(対象.Id,true);
    }
    private void ツリーの入力を確認(文書状態 文書){
        if(文書.定義.Kind==DefinitionKind.Database&&this.DBツリー画面.GetValueOrDefault(文書.DB.Id)?.未保存か is true){
            throw new ManagementException("Validation",表示文言.取得("左のツリーに確定していない入力があります。入力を確定するか、Esc で取り消してから、もう一度操作してください。"));
        }
    }
    private async Task<ObjectDefinitionEdit> ツリー列確定時Async(string DBID,ツリー列編集要求 要求){
        if(!this.文書一覧.TryGetValue(DBID,out var 文書)){throw new ManagementException("NotFound",表示文言.取得("データベースがありません。"));}
        if(!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)){throw new ManagementException("Permission",表示文言.取得("権限がありません。"));}
        if(this.操作中か||文書.未確定操作 is not null){throw new ManagementException("Busy",表示文言.取得("処理が完了してから再度操作してください。"));}
        var 種類=要求.基準.Object.Kind;
        if(要求.基準.DefinitionId!=文書.定義.Id||種類 is not ("Table" or "View" or "ScalarFunction" or "TableFunction")){throw new ManagementException("Conflict",表示文言.取得("編集対象が一致しません。"));}
        foreach(var 対象 in this.対象文書一覧.Values.Where(対象=>対象.DBID==DBID&&対象.対象?.Id==要求.基準.Object.Id&&対象.編集中か)){
            if(!this.対象画面.TryGetValue(対象.ID,out var 画面)||!await 画面.同期Async()){throw new ManagementException("Validation",表示文言.取得("C#の入力を確認してください。"));}
        }
        if(種類=="ScalarFunction"&&要求.戻り型 is{} 型&&DatabaseDesigner.FunctionElementType(型) is not null){
            throw new ManagementException("Validation",表示文言.取得("レコードの集合を返すには、関数に結果列を追加してください。"));
        }
        var 形を変更するか=種類=="ScalarFunction"&&要求.列一覧.Length>0||種類=="TableFunction"&&要求.列一覧.Length==0&&要求.基準.Columns.Length>0;
        var 結果=形を変更するか?DatabaseDesigner.ChangeFunctionResultShape(文書.定義,要求.基準,要求.列一覧):
            DatabaseDesigner.ApplyObjectStructure(文書.定義,要求.基準,要求.列一覧,要求.引数一覧??要求.基準.Parameters,要求.戻り型??要求.基準.ReturnType);
        if(結果.Succeeded){
            文書.定義=結果.Definition!;文書.プレビュー=null;
            if(形を変更するか){
                var 次=文書.オブジェクト.Single(項目=>項目.Id==要求.基準.Object.Id);
                foreach(var 開いた文書 in this.対象文書一覧.Values.Where(対象=>対象.DBID==DBID&&対象.対象?.Id==次.Id&&対象.対象.Kind==種類)){開いた文書.対象=次;}
            }
            this.案内=表示文言.取得(種類=="Table"?"テーブルを下書きへ反映しました。稼働DBへ未適用です。":"問い合わせを下書きへ反映しました。");this.エラーか=false;
        }
        return 結果;
    }
    private async Task ツリー列追加開始Async(管理対象 対象){
        this.DBツリー状態(対象.ID).展開中か=true;
        if(this.DBツリー画面.TryGetValue(対象.ID,out var 木)&&対象.表 is{} 表){await 木.列追加を開始Async(表);}
        else{throw new ManagementException("Busy",表示文言.取得("テーブルを展開してから列を追加してください。"));}
    }
}
