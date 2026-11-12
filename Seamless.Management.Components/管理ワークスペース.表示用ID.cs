namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private bool 表示用ID対応か,表示用ID処理中か;
    private string? 表示用ID報告DBID,表示用ID報告ハッシュ;
    private int 表示用ID報告件数;
    private string[] 表示用ID報告診断=[];
    private bool 表示用ID操作が可能か(管理対象 対象)=>this.表示用ID対応か&&!this.表示用ID処理中か&&対象.種類=="DB"
        &&this.対象DB(対象) is{IsSystem:false} DB&&this.文書一覧.TryGetValue(DB.Id,out var 文書)&&文書.未確定操作 is null
        &&this.権限があるか(DB.Name,ManagementPermission.View)&&this.権限があるか(DB.Name,ManagementPermission.Edit);
    private bool 表示用IDの保護対象入力があるか(string DBID)=>this.対象文書一覧.Values.Any(対象=>対象.DBID==DBID&&(対象.編集中か||対象.データ編集中か))
        ||this.DBツリー画面.TryGetValue(DBID,out var 木)&&木.未保存か||this.新規作成入力?.DBID==DBID||this.プロジェクト未保存か;
    private async Task 表示用ID付与時Async(管理対象 対象){
        if(!this.表示用ID対応か){throw new ManagementException("Unsupported",表示文言.取得("この接続先は表示用IDの付与に対応していません。"));}
        if(!this.表示用ID操作が可能か(対象)){throw new ManagementException("Permission",表示文言.取得("表示用IDの付与にはDBの参照・編集権限が必要です。"));}
        var 文書=this.文書一覧[対象.ID];
        if(this.表示用IDの保護対象入力があるか(対象.ID)){throw new ManagementException("Validation",表示文言.取得("入力中の編集を下書きへ反映してから、表示用IDを付与してください。入力内容は保持しています。"));}
        var 基準=文書.定義;var ハッシュ=基準.Hash();var 版=文書.基準版;var 取消=this.寿命.Token;
        this.表示用ID処理中か=true;this.案内=表示文言.取得("{0}の表示用IDを下書きへ付与しています。",文書.DB.Name);
        try{
            var 候補=await Task.Run(()=>DatabaseDesigner.AssignStableIdentities(基準.Freeze()),取消);
            if(this.破棄済みか||取消.IsCancellationRequested){return;}
            var 識別=DatabaseDesigner.ReadStableIdentities(候補);
            // 保存と同じSource認可をサーバーで再検査し、待機中の公開変更を拒否する。
            var 公開=await this.管理操作.GetDefinitionAsync(基準.Id,取消);
            if(this.破棄済みか||取消.IsCancellationRequested){return;}
            if(!this.表示用ID対応か||!this.権限があるか(文書.DB.Name,ManagementPermission.View)||!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)){
                throw new ManagementException("Permission",表示文言.取得("表示用IDの付与にはDBの参照・編集権限が必要です。"));
            }
            if(!this.文書一覧.TryGetValue(対象.ID,out var 現在)||!ReferenceEquals(現在,文書)||文書.定義.Hash()!=ハッシュ||文書.基準版!=版||公開.Version!=版||公開.Database!=文書.DB.Name||文書.未確定操作 is not null||this.表示用IDの保護対象入力があるか(対象.ID)){
                throw new ManagementException("Conflict",表示文言.取得("ID付与中に定義・公開版または入力状態が変わりました。現在の編集を保持したまま処理を中止しました。"));
            }
            if(候補.Hash()!=ハッシュ){文書.定義=候補;文書.プレビュー=null;}
            this.表示用ID報告DBID=対象.ID;this.表示用ID報告ハッシュ=文書.定義.Hash();this.表示用ID報告件数=識別.Entries.Count;
            this.表示用ID報告診断=識別.Diagnostics.Select(表示用ID診断文言).Distinct(StringComparer.Ordinal).ToArray();
            this.案内=識別.Entries.Count==0?表示文言.取得("{0}に表示用IDを付与する対象がありません。スキーマまたはテーブルを定義してください。",文書.DB.Name)
                :候補.Hash()==ハッシュ?表示文言.取得("{0}の表示用IDは付与済みです。現在の下書きを継続します。",文書.DB.Name):表示文言.取得("{0}の表示用IDを下書きへ反映しました。保存・変更確認・明示適用へ進んでください。",文書.DB.Name);
            this.エラーか=false;
        }catch(ManagementException 例外){throw new ManagementException(例外.Code,表示文言.取得(例外.Message));}
        finally{this.表示用ID処理中か=false;}
    }
    private static string 表示用ID診断文言(DiagnosticInfo 診断)=>診断.Code switch{
        "IdentityDeclaration"=>表示文言.取得("表示用IDは元宣言のマーカーです。IDの転用や別版との同一性には別途検証が必要です。"),
        "IdentityUnbound"=>表示文言.取得("管理対象へ対応しないIDマーカーがあります。名前からの補完は行いません。"),
        "IdentityAmbiguous"=>表示文言.取得("複数の対象が同じ元宣言を共有しているため、一意の表示用IDを提供できません。"),
        _=>表示文言.取得(診断.Message)
    };
}
