namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private async Task<string> 監査集計状態Async(ManagementClient 接続,CancellationToken 取消){
        if(!this.サーバー能力.Contains("AuditReadBufferV1",StringComparer.Ordinal)){return " このサーバーは現在の集計収集状態の取得に未対応です。";}
        try{var 値=await 接続.ReadAuditStatusAsync(取消);if(!値.Enabled){return " 本起動の集計収集は未設定です。表示中の保存済み集計とは別の状態です。";}var 状態=$" 本起動の収集: 受付 {値.AcceptedCount:N0} / 保存確認 {値.SavedCount:N0} / 既知の欠落 {値.DroppedCount:N0} 件。待機 {値.PendingKeys:N0} キー・概算 {値.PendingBytes:N0} バイト。集計期間 {値.WindowSeconds:g} 秒、保存間隔 {値.SaveIntervalSeconds:g} 秒。";if(値.LastSavedAt is{} 時刻){状態+=" 最終保存確認UTC: "+時刻.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture)+"。";}if(値.ErrorCode is not null){状態+=" 保存に問題があります: "+値.ErrorCode+"。検索は継続します。";}if(値.UnconfirmedCount>0){状態+=" 保存結果が未確認のまま解放した集計 "+値.UnconfirmedCount.ToString("N0")+" 件。";}if(値.CountersSaturated){状態+=" カウンターは上限に達しており、表示値以上です。";}状態+=" 異常終了時の最後の未保存分の欠落量は、この件数には含みません。";return 状態;}
        catch(OperationCanceledException)when(取消.IsCancellationRequested){throw;}
        catch{return " 集計の収集状態を取得できませんでした。保存済みの検索結果を表示しています。";}
    }
}
