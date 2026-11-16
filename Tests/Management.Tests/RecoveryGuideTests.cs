using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

// 段階8：復元の手順の表示とプロンプト。場面は4つ、プロンプトには場所・版・エラー文を入れ、資格情報は入れない(2026-11-16 の利用者の決定)。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階8。
public sealed class RecoveryGuideTests{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"recovery-"+Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("password",RecoveryScenario.PasswordLost,"reset-password")]
    [InlineData("startup",RecoveryScenario.ServerWontStart,"recovery-guide --scenario migration")]
    [InlineData("migration",RecoveryScenario.InterruptedMigration,"get-operation")]
    [InlineData("restore",RecoveryScenario.RestoreFromBackup,"restore --backup")]
    public void EveryScenarioShowsStepsAndPromptsWithLocationsVersionAndError(string name,RecoveryScenario scenario,string command){
        Assert.Equal(scenario,RecoveryGuide.Parse(name));
        var root=Root();var backup=root+"-backup";
        var guide=RecoveryGuide.Create(scenario,new("seamless",root,backup,"1.2.3","起動に失敗しました",[]));
        Assert.NotEmpty(guide.Title);
        Assert.Contains(guide.Steps,step=>step.Command?.Contains(command,StringComparison.Ordinal)==true);
        foreach(var prompt in new[]{guide.ChatPrompt,guide.AgentPrompt}){
            Assert.Contains(root,prompt);Assert.Contains(backup,prompt);Assert.Contains("1.2.3",prompt);Assert.Contains("起動に失敗しました",prompt);
            Assert.Contains(guide.Title,prompt);
        }
        // エージェントへの指示は、CLI のコマンドだけを使い、パスワードは利用者に頼むことを含む。
        Assert.Contains("CLI のコマンドだけ",guide.AgentPrompt);
        Assert.Contains("利用者に入力を頼む",guide.AgentPrompt);
        Assert.Throws<ArgumentException>(()=>RecoveryGuide.Parse("unknown"));
    }

    [Fact]
    public void InterruptedMigrationsAreFoundFromTheirMarkersAndRolledBackByCommand(){
        var root=Root();Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"management.typed-upgrade.json"),"{}");
        var guide=RecoveryGuideCommand.Create(RecoveryScenario.InterruptedMigration,root,null,null);
        Assert.Contains(guide.Steps,step=>step.Command==RecoveryGuideCommand.Tool+" rollback-managed-types-upgrade --root \""+Path.GetFullPath(root)+"\"");
        Assert.Contains(guide.Steps,step=>step.Command?.Contains("upgrade-managed-types --root",StringComparison.Ordinal)==true);
        Assert.DoesNotContain(guide.Steps,step=>step.Command?.Contains("rollback-audit-upgrade",StringComparison.Ordinal)==true);
        Assert.Contains("型付き管理行への更新",guide.AgentPrompt);
        Assert.Equal(Path.GetFullPath(root),RecoveryGuideCommand.Folder(guide,root));
        var output=new StringWriter();RecoveryGuideCommand.Write(output,guide);
        Assert.Contains("チャットAI",output.ToString());Assert.Contains("rollback-managed-types-upgrade",output.ToString());
        // 印が無ければ、取り消しのコマンドは出さず、更新監査の導入の状態の確かめ方を示す。
        File.Delete(Path.Combine(root,"management.typed-upgrade.json"));
        guide=RecoveryGuideCommand.Create(RecoveryScenario.InterruptedMigration,root,null,null);
        Assert.DoesNotContain(guide.Steps,step=>step.Command?.Contains("rollback-",StringComparison.Ordinal)==true);
        Assert.Contains(guide.Steps,step=>step.Command?.Contains("get-operation",StringComparison.Ordinal)==true);
    }

    // Manager の画面からも同じ手順を取れる(対応機能 RecoveryGuideV1)。管理領域の場所はサーバーが入れる。管理の権限が要る。
    [Fact]
    public async Task ManagerOperationsReturnTheGuideForTheServerRoot(){
        var root=Root();
        using var host=InProcessManagementHost.Create(root);
        IManagementOperations operations=host;
        Assert.Contains("RecoveryGuideV1",(await operations.InfoAsync()).Capabilities);
        var guide=await operations.RecoveryGuideAsync(RecoveryScenario.RestoreFromBackup);
        Assert.Equal(RecoveryScenario.RestoreFromBackup,guide.Scenario);
        Assert.Contains(Path.GetFullPath(root),guide.ChatPrompt);
        Assert.Contains(guide.Steps,step=>step.Command?.Contains("restore --backup",StringComparison.Ordinal)==true);
        // 画面の部品は、選んだ場面の手順をサーバーから受け取って持つ。
        using var screen=new Seamless.Management.Components.復元の手順ビュー();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var type=typeof(Seamless.Management.Components.復元の手順ビュー);
        type.GetProperty("管理操作",flags)!.SetValue(screen,host);
        await (Task)type.GetMethod("選択時Async",flags)!.Invoke(screen,[RecoveryScenario.PasswordLost])!;
        var shown=Assert.IsType<RecoveryGuideDocument>(type.GetField("手順",flags)!.GetValue(screen));
        Assert.Equal(RecoveryScenario.PasswordLost,shown.Scenario);
        Assert.False((bool)type.GetField("エラーか",flags)!.GetValue(screen)!);
    }

    [Fact]
    public void StartupFailureHintCarriesTheRootAndError(){
        var hint=RecoveryGuideCommand.StartupHint(["serve","--root","C:\\data\\management"],"管理ヘッダーがありません。\n\"バックアップ\"から復旧してください。");
        Assert.Contains("recovery-guide --scenario startup --root \"C:\\data\\management\"",hint);
        Assert.Contains("--error \"管理ヘッダーがありません。 'バックアップ'から復旧してください。\"",hint);
    }
}
