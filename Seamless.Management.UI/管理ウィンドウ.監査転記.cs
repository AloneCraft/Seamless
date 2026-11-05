using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private sealed record 転記表示行(AuditDeliveryState 元){
        public string DB=>this.元.Database;
        public string 世代{get{if(this.元.Archived){return "旧世代";}return "稼働版";}}
        public string 転記状態{get{if(this.元.ErrorCode is not null){return "転記失敗";}if(this.元.DurableLsn is null||this.元.AcknowledgedLsn is null){return "未確認";}if(this.元.DurableLsn>this.元.AcknowledgedLsn){return "転記待ち";}return "転記済み";}}
        public string 未処理LSN{get{if(this.元.DurableLsn is not{} 確定||this.元.AcknowledgedLsn is not{} 処理済み||確定<処理済み){return "不明";}return (確定-処理済み).ToString(CultureInfo.InvariantCulture);}}
        public string 更新UTC{get{if(this.元.StateUpdatedUtc is{} 時刻){return 時刻.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);}return "未確認";}}
        public string 問題{get{if(this.元.ErrorCode is null){return "—";}return this.元.ErrorCode switch{"Capacity"=>"資源の上限","Integrity"=>"識別情報・保存内容の不一致","RecoveryRequired"=>"復旧が必要","ProjectionFailed"=>"転記先への保存などに失敗",_=>this.元.ErrorCode};}}
    }
    private void 監査転記列を設定(){
        this.監査結果.Columns.Clear();foreach(var (名前,幅) in new[]{("DB",180d),("世代",85d),("転記状態",120d),("未処理LSN",110d),("更新UTC",190d),("問題",280d)}){var 列=new DataGridTextColumn{Header=名前,Binding=new Binding(名前),Width=幅};if(名前=="問題"){列.Width=new DataGridLength(1,DataGridLengthUnitType.Star);列.MinWidth=180;}this.監査結果.Columns.Add(列);}
    }
    private async Task 監査転記ページAsync(bool 新規か){
        if(!this.監査利用可能か()){await this.監査を表示Async();return;}if(this.監査取消 is not null){return;}
        if(!this.サーバー能力.Contains("AuditDeliveryV1",StringComparer.Ordinal)){this.監査状態.Text="このサーバーは更新監査の転記状態の取得に未対応です。";return;}
        if(!新規か&&(!this.監査続きがあるか||this.監査条件変更中か)){return;}var カーソル=0L;if(!新規か){カーソル=this.監査カーソル;}
        var 接続=this.必須接続();using var 取消=new CancellationTokenSource();this.監査取消=取消;this.監査操作状態を更新();this.監査状態.Text="転記状態を取得中です。";
        try{
            var 頁=await 接続.AuditDeliveryStatusAsync(カーソル,取消.Token);if(!ReferenceEquals(this.クライアント,接続)||!ReferenceEquals(this.監査取消,取消)||取消.IsCancellationRequested){return;}
            this.監査検索条件=new();this.監査条件変更中か=false;this.監査カーソル=頁.NextCursor;this.監査上限=0;this.監査続きがあるか=頁.HasMore;this.監査転記列を設定();this.監査結果.ItemsSource=頁.Items.Select(static 項目=>new 転記表示行(項目)).ToArray();this.監査結果.SelectedItem=null;this.監査詳細.Visibility=Visibility.Collapsed;
            this.監査状態.Text="このページ: "+頁.Items.Length+" 世代。";if(頁.Running){this.監査状態.Text+="バックグラウンド転記は稼働中です。";}else{this.監査状態.Text+="バックグラウンド転記は停止しています。";}
            if(頁.ErrorCode is not null){this.監査状態.Text+="転記処理の問題: "+頁.ErrorCode+"。";}if(頁.HasMore){this.監査状態.Text+="続きがあります。";}if(頁.Items.Length==0){this.監査状態.Text+="登録された転記元はありません。導入前のDBはこの一覧に含まれません。";}
            this.監査状態.Text+=" 状態は最後に保存された確認結果です。未処理LSNはログの位置の差で、イベント件数ではありません。";
        }catch(OperationCanceledException)when(取消.IsCancellationRequested){if(ReferenceEquals(this.監査取消,取消)){this.監査状態.Text="転記状態の取得を中止しました。表示中の結果は以前の確認結果です。";}}
        finally{if(ReferenceEquals(this.監査取消,取消)){this.監査取消=null;this.監査操作状態を更新();}}
    }
    private static string 監査転記の詳細(転記表示行 行){var 元=行.元;return $"DB: {元.Database}\n世代: {行.世代}\n定義ID: {元.DefinitionId}\nDBのDLL識別: {元.ArtifactHash}\nWAL ID: {元.WalId}\n確定済みLSN: {元.DurableLsn}\n転記処理済みLSN: {元.AcknowledgedLsn}\n状態: {行.転記状態}\n問題: {行.問題}\n状態更新UTC: {行.更新UTC}\n未処理LSNは位置の差です。監査イベントの件数ではありません。";}
}
