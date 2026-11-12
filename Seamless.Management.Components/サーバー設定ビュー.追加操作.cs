namespace Seamless.Management.Components;

public partial class サーバー設定ビュー{
    private コマンド登録<string>? 追加登録;
    private bool 通信予算展開か,外部受付展開か;
    private long 設定展開世代;
    private コマンド登録<string> 追加コマンド=>this.追加登録??=new([
        new("request","受信バッファ予算",_=>!this.固定か(nameof(ManagementServerSettings.MaximumBufferedRequestBytes)),_=>!this.処理中か&&this.情報 is not null,_=>{this.通信予算展開か=true;this.設定展開世代++;return Task.CompletedTask;},["buffer","request","予算"],アイコン:"設定"),
        new("response","送信待ち予算",_=>!this.固定か(nameof(ManagementServerSettings.MaximumPendingWriteBytes)),_=>!this.処理中か&&this.情報 is not null,_=>{this.通信予算展開か=true;this.設定展開世代++;return Task.CompletedTask;},["buffer","response","予算"],アイコン:"設定"),
        new("timeout","応答送信期限",_=>!this.固定か(nameof(ManagementServerSettings.ResponseWriteTimeout)),_=>!this.処理中か&&this.情報 is not null,_=>{this.通信予算展開か=true;this.設定展開世代++;return Task.CompletedTask;},["timeout","期限"],アイコン:"設定"),
        new("external","外部ブラウザーの受付",_=>!this.固定か(nameof(ManagementServerSettings.ExternalManagerOrigin)),_=>!this.処理中か&&this.情報 is not null,_=>{this.外部受付展開か=true;this.設定展開世代++;return Task.CompletedTask;},["https","browser","受付"],アイコン:"開く")
    ]);
}
