using System.Text;

namespace Seamless.Management;

/// <summary>復元の手順を示す場面(2026-11-16 に利用者が決めた4つ)。</summary>
public enum RecoveryScenario{PasswordLost,ServerWontStart,InterruptedMigration,RestoreFromBackup}

/// <summary>中断した明示の移行。管理領域の中の印のファイルで見分け、対応する取り消しのコマンドで戻す。</summary>
public sealed record InterruptedUpgrade(string Marker,string Name,string RollbackCommand,string RetryCommand);

/// <summary>管理RPCの recoveryGuide の要求。</summary>
public sealed record RecoveryGuideRequest(RecoveryScenario Scenario);

/// <summary>手順の1つ。Command は CLI で打つコマンド、Folder は開くとよいフォルダー。</summary>
public sealed record RecoveryStep(string Text,string? Command=null,string? Folder=null);

/// <summary>手順と、チャットAI に貼る問い合わせ文・エージェントに渡す自動復元の指示文。</summary>
public sealed record RecoveryGuideDocument(RecoveryScenario Scenario,string Title,RecoveryStep[] Steps,string ChatPrompt,string AgentPrompt){
    public RecoveryBackupLocation[] Backups{get;init;}=[];
}
public sealed record RecoveryBackupLocation(string Database,DateTimeOffset StartedAt,string Path);

/// <summary>
/// 手順とプロンプトを作るときの材料。資格情報(パスワード・トークン)は持たない(2026-11-16 の利用者の決定：場所と版とエラー文まで)。
/// Tool は CLI の実行ファイルの名前、Root は管理領域、Backup はバックアップの場所、Error は起動の失敗などのエラー文。
/// </summary>
public sealed record RecoveryContext(string Tool,string? Root,string? Backup,string Version,string? Error,InterruptedUpgrade[] Interrupted);

/// <summary>
/// 復元の手順の表示(Manager の画面と CLI の両方で同じものを示す、2026-11-12 の利用者の指示)。
/// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階8。
/// </summary>
public static class RecoveryGuide{
    /// <summary>明示の移行のうち、中断すると管理領域に印が残るもの。印の名前は ManagementStoreUpgrade と同じ。</summary>
    public static readonly InterruptedUpgrade[] KnownUpgrades=[
        new("management.audit-upgrade.json","監査用テーブルへの更新","rollback-audit-upgrade","upgrade-audit"),
        new("management.prepared-wal-upgrade.json","分割WALへの更新","rollback-prepared-wal-upgrade","upgrade-prepared-wal"),
        new("management.typed-upgrade.json","型付き管理行への更新","rollback-managed-types-upgrade","upgrade-managed-types")];

    public static RecoveryScenario Parse(string value)=>value.Trim().ToLowerInvariant() switch{
        "password"=>RecoveryScenario.PasswordLost,
        "startup"=>RecoveryScenario.ServerWontStart,
        "migration"=>RecoveryScenario.InterruptedMigration,
        "restore"=>RecoveryScenario.RestoreFromBackup,
        _=>throw new ArgumentException("場面は password・startup・migration・restore のどれかにしてください。")};

    public static RecoveryGuideDocument Create(RecoveryScenario scenario,RecoveryContext context){
        ArgumentNullException.ThrowIfNull(context);
        var root=Quote(context.Root,"<管理領域>");var backup=Quote(context.Backup,"<バックアップ>");var tool=context.Tool;
        var stop=new RecoveryStep("Seamless Management Server を止めます(管理領域は動いているサーバーが占有しているため、止めないとコマンドが失敗します)。");
        var (title,steps)=scenario switch{
            RecoveryScenario.PasswordLost=>("パスワードを忘れた",new[]{
                stop,
                new RecoveryStep("念のための写しを置く、空の新しいフォルダーを決めます(コマンドがここへ管理領域を写してから書き換えます)。"),
                new RecoveryStep("利用者名を指定してパスワードを再設定します。新しいパスワードを2回たずねられます(12文字以上)。権限と有効・無効の状態は変わりません。",
                    tool+" reset-password --root "+root+" --user <利用者名> --backup \"<空の新しいフォルダー>\"",context.Root),
                new RecoveryStep("サーバーを起動し直し、新しいパスワードでログインします。")}),
            RecoveryScenario.ServerWontStart=>("サーバーが起動できない",new[]{
                new RecoveryStep("エラー文を確かめます。「排他取得できません」なら、同じ管理領域を使う別のサーバーが動いています。それを止めてから起動し直します。"),
                new RecoveryStep("「中断した…を先に明示復旧してください」なら、移行が途中で止まっています。「移行の中断」の手順へ進みます。",tool+" recovery-guide --scenario migration --root "+root),
                new RecoveryStep("「形式を…明示的に更新してください」なら、明示の移行が要ります。コマンドの一覧で更新のコマンドを確かめ、空の新しい退避先を指定して実行します。",tool+" --help"),
                new RecoveryStep("「管理ヘッダーがありません」「バックアップから復旧してください」など、管理領域が壊れているときは「バックアップから戻す」の手順へ進みます。",tool+" recovery-guide --scenario restore --root "+root+" --backup "+backup),
                new RecoveryStep("管理領域のフォルダーを開いて、management.format(形式の番号)と、名前が management.*-upgrade.json のファイル(移行の中断の印)があるかを確かめます。",null,context.Root)}),
            RecoveryScenario.InterruptedMigration=>("移行が途中で止まった",Migration(context,stop,tool,root)),
            RecoveryScenario.RestoreFromBackup=>("バックアップから戻す",new[]{
                stop,
                new RecoveryStep("今の管理領域を、念のため空の新しいフォルダーへ写します(壊れていて写せないときは飛ばします)。",tool+" backup --root "+root+" --destination \"<空の新しいフォルダー>\"",context.Root),
                new RecoveryStep("バックアップを、空の新しいフォルダーへ写して新しい管理領域にします(バックアップそのものは書き換えません)。",tool+" restore --backup "+backup+" --destination \"<新しい管理領域>\"",context.Backup),
                new RecoveryStep("起動の設定(--root、または起動設定ファイルの Root)を新しい管理領域へ向けて、サーバーを起動します。"),
                new RecoveryStep("Manager にログインして、DB と利用者が戻っていることを確かめます。バックアップを取った後の変更は戻りません。")}),
            _=>throw new ArgumentOutOfRangeException(nameof(scenario))};
        return new(scenario,title,steps,ChatPrompt(title,steps,context),AgentPrompt(title,steps,context));
    }

    private static RecoveryStep[] Migration(RecoveryContext context,RecoveryStep stop,string tool,string root){
        var steps=new List<RecoveryStep>{stop};
        if(context.Interrupted.Length==0){
            steps.Add(new("管理領域に移行の中断の印(management.*-upgrade.json)は見つかりませんでした。DB への更新監査の導入(enable-update-audit)が止まった場合は、出力された操作IDで状態を確かめます。",
                tool+" get-operation --root "+root+" --operation-id <操作ID>",context.Root));
        }
        foreach(var upgrade in context.Interrupted){
            steps.Add(new("「"+upgrade.Name+"」が途中で止まっています。取り消して、移行の前の状態へ戻します。",tool+" "+upgrade.RollbackCommand+" --root "+root,context.Root));
            steps.Add(new("もう一度移行します。退避先には空の新しいフォルダーを指定します。",tool+" "+upgrade.RetryCommand+" --root "+root+" --backup \"<空の新しいフォルダー>\""));
        }
        steps.Add(new("サーバーを起動し直します。取り消しも失敗するときは「バックアップから戻す」の手順へ進みます。"));
        return steps.ToArray();
    }

    private static string Quote(string? path,string placeholder)=>"\""+(string.IsNullOrWhiteSpace(path)?placeholder:path)+"\"";

    private static void Facts(StringBuilder text,RecoveryContext context){
        text.AppendLine("- 製品：Seamless Management Server "+context.Version);
        text.AppendLine("- 管理領域："+(context.Root??"(未指定)"));
        text.AppendLine("- バックアップ："+(context.Backup??"(未指定)"));
        if(!string.IsNullOrWhiteSpace(context.Error)){text.AppendLine("- エラー文："+context.Error.Trim());}
        foreach(var upgrade in context.Interrupted){text.AppendLine("- 中断している移行："+upgrade.Name+"(印 "+upgrade.Marker+")");}
    }
    private static void Steps(StringBuilder text,RecoveryStep[] steps){
        for(var index=0;index<steps.Length;index++){
            text.Append(index+1).Append(". ").AppendLine(steps[index].Text);
            if(steps[index].Command is{} command){text.AppendLine("   コマンド: "+command);}
        }
    }
    private static string ChatPrompt(string title,RecoveryStep[] steps,RecoveryContext context){
        var text=new StringBuilder();
        text.AppendLine("Seamless Management Server の管理領域で「"+title+"」が起きました。状況は次のとおりです。");
        Facts(text,context);
        text.AppendLine("製品が示した手順は次のとおりです。");
        Steps(text,steps);
        text.AppendLine("この手順で安全に進めてよいか、注意点と、手順どおりにいかないときに確かめることを教えてください。パスワードなどの資格情報はこの文に含めていません。");
        return text.ToString();
    }
    private static string AgentPrompt(string title,RecoveryStep[] steps,RecoveryContext context){
        var text=new StringBuilder();
        text.AppendLine("あなたは Seamless Management Server の復元を手伝うエージェントです。「"+title+"」から復旧してください。状況は次のとおりです。");
        Facts(text,context);
        text.AppendLine("守ること:");
        text.AppendLine("- 始める前に、サーバーが止まっていることを確かめる。");
        text.AppendLine("- 管理領域・バックアップの中のファイルを直接書き換えたり消したりしない。製品の CLI のコマンドだけを使う。");
        text.AppendLine("- 退避先・写し先には、空の新しいフォルダーを使う。既存のフォルダーを上書きしない。");
        text.AppendLine("- パスワードの入力が要るときは、利用者に入力を頼む。資格情報を記録・表示しない。");
        text.AppendLine("- 各コマンドの出力を確かめ、失敗したらそこで止めて、出力をそのまま利用者に報告する。");
        text.AppendLine("手順:");
        Steps(text,steps);
        return text.ToString();
    }
}
