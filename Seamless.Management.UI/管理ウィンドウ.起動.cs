using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private string? サーバー実行ファイル;
    private Process? 起動したサーバー;
    private string? 起動したサーバーの停止ファイル;
    private void 自分のサーバーに停止を要求(){
        if(this.起動したサーバー is{} 処理&&!処理.HasExited&&this.起動したサーバーの停止ファイル is{} パス){File.WriteAllText(パス,"stop");}
    }
    private string 起動設定の場所(){
        if(this.起動設定ファイル is not null){return this.起動設定ファイル;}
        try{return ManagementLaunchConfiguration.WorkspacePath();}catch(ManagementException){return @"D:\Seamless\Management\seamless-management.local.json";}
    }
    private string サーバーを探す(){
        if(this.サーバー実行ファイル is{} パス&&File.Exists(パス)){return パス;}
        var 構成="Release";
#if DEBUG
        構成="Debug";
#endif
        var 候補=new List<string>{Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","ServerWindows","Seamless.Management.Server.exe")),Path.Combine(AppContext.BaseDirectory,"Seamless.Management.Server.exe")};
        try{var 作業場所=Path.GetDirectoryName(ManagementLaunchConfiguration.WorkspacePath())!;候補.Add(Path.Combine(作業場所,"Seamless.Management.Server","bin",構成,"net10.0","Seamless.Management.Server.exe"));}catch(ManagementException){}
        var 発見=候補.FirstOrDefault(File.Exists);if(発見 is not null){this.サーバー実行ファイル=発見;return 発見;}
        var 選択=new OpenFileDialog{Title="Windowsサーバーの実行ファイルを選択",Filter="Seamless管理サーバー|Seamless.Management.Server.exe"};if(選択.ShowDialog(this)!=true){throw new OperationCanceledException("サーバーの選択を取り消しました。");}this.サーバー実行ファイル=選択.FileName;return 選択.FileName;
    }
    private ProcessStartInfo 起動情報(string 実行ファイル,params string[] 引数){
        var 情報=new ProcessStartInfo(実行ファイル){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};foreach(var 値 in 引数){情報.ArgumentList.Add(値);}return 情報;
    }
    private async Task ローカル初期設定Async(){
        var 種類=MessageBox.Show(this,"このPCで初めてSeamlessサーバーを使いますか？\n\n「はい」：新しいサーバーの保存先と管理者を作成\n「いいえ」：作成済みのサーバーを登録\n既存のDBやパスワードは上書きしません。","ローカルサーバーの初期設定",MessageBoxButton.YesNoCancel);if(種類==MessageBoxResult.Cancel){return;}
        var 設定場所=this.起動設定の場所();var 新規か=種類==MessageBoxResult.Yes;
        var 入力=this.初期設定ダイアログを作る(新規か,設定場所);if(入力.ShowDialog()!=true){return;}var 値=入力.値;
        var 実行ファイル=this.サーバーを探す();ProcessStartInfo 情報;
        if(新規か){情報=this.起動情報(実行ファイル,"setup-local","--root",値[0],"--config",値[1],"--admin",値[2]);情報.Environment["SEAMLESS_MANAGEMENT_PASSWORD"]=値[3];}
        else{情報=this.起動情報(実行ファイル,"configure-local","--root",値[0],"--config",値[1],"--certificate",値[2]);}
        情報.RedirectStandardOutput=true;情報.RedirectStandardError=true;using var 処理=Process.Start(情報)??throw new IOException("初期設定を開始できません。");var 出力=処理.StandardOutput.ReadToEndAsync();var エラー=処理.StandardError.ReadToEndAsync();await 処理.WaitForExitAsync();await 出力;var 内容=await エラー;if(処理.ExitCode!=0){throw new IOException(内容);}
        this.起動設定ファイル=Path.GetFullPath(値[1]);this.接続案内.Text="初期設定が完了しました。「サーバー起動」を押してください。";this.診断.Text="起動設定: "+this.起動設定ファイル+"\nパスワードは保存していません。サーバー起動後にログインしてください。";
    }
    private 入力ダイアログ 初期設定ダイアログを作る(bool 新規か,string 設定場所){
        入力項目[] 項目=[new("サーバーの保存先（DB・利用者・履歴を保存。通常は変更不要）",@"D:\Seamless\Management\Instance1"),new("共通の起動設定ファイル（通常は変更不要）",設定場所),new("管理者名","admin"),new("新しいパスワード（12文字以上）",秘密か:true),new("新しいパスワードの確認",秘密か:true)];
        if(!新規か){項目=[new("作成済みサーバーの保存先"),new("共通の起動設定ファイル",設定場所),new("サーバー証明書（秘密鍵付きPFX）")];}
        var 入力=new 入力ダイアログ(this,新規か?"新しいローカルサーバーを初期設定":"既存のローカルサーバーを登録",項目);
        if(新規か){入力.決定ラベル="初期設定を実行";入力.入力検証=値=>入力ダイアログ.パスワードを検証(値,3,4);}else{入力.決定ラベル="登録";}return 入力;
    }
    private async Task ローカルサーバー起動Async(){
        var 設定場所=this.起動設定の場所();var 設定=ManagementLaunchConfiguration.Read(設定場所);var 既存=await Task.Run(()=>LocalServerConnection.Read(設定.Root));if(既存 is not null){this.接続案内.Text="サーバーは既に起動しています。ログインしてください。";return;}
        var 停止ファイル=Path.Combine(設定.Root,"manager-"+Guid.NewGuid().ToString("N")+".stop");
        using var 所有者=Process.GetCurrentProcess();var 情報=this.起動情報(this.サーバーを探す(),"serve","--config",設定場所,"--local-ipc","--stop-file",停止ファイル,"--owner-pid",所有者.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),"--owner-start",所有者.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));情報.RedirectStandardOutput=true;情報.RedirectStandardError=true;
        var 処理=Process.Start(情報)??throw new IOException("サーバーを起動できません。");var エラー=処理.StandardError.ReadToEndAsync();var 出力=処理.StandardOutput.ReadToEndAsync();
        var 期限=DateTime.UtcNow.AddSeconds(15);
        while(DateTime.UtcNow<期限){
            if(処理.HasExited){var 内容=await エラー;await 出力;処理.Dispose();throw new IOException("サーバーが終了しました。\n"+内容);}
            if(await Task.Run(()=>LocalServerConnection.Read(設定.Root)) is not null){this.起動設定ファイル=設定場所;this.起動したサーバー=処理;this.起動したサーバーの停止ファイル=停止ファイル;this.状態.Text="サーバーを起動しました。パスワードを入力してログインしてください。";return;}
            await Task.Delay(100);
        }
        this.起動したサーバー=処理;this.起動したサーバーの停止ファイル=停止ファイル;throw new IOException("サーバーの起動結果をまだ確認できません。サーバーの起動状態を確認してください。重複起動はしないでください。");
    }
}
