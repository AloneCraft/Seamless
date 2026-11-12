namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private bool 操作受付中か,画面イベント準備済みか,画面初期化失敗か;
    private string 起動状態メッセージ{get{if(this.画面初期化失敗か){return "画面の初期化に失敗しました。再読み込みしてください。";}return "接続を準備しています。操作できるまでお待ちください。";}}
    private string 起動状態ロール{get{if(this.画面初期化失敗か){return "alert";}return "status";}}
}
