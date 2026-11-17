using System.Diagnostics;
using System.Reflection;

namespace Seamless.Management.Server;

/// <summary>
/// CLI の recovery-guide と、起動に失敗したときの案内。Manager に入れないとき(パスワードの紛失・起動できないなど)も、画面と同じ手順とプロンプトを示す。
/// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階8。
/// </summary>
internal static class RecoveryGuideCommand{
    internal static string Version=>typeof(RecoveryGuideCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"(版不明)";

    // 実行ファイルの名前。dotnet から DLL を起動したときは dotnet と DLL の場所にする。
    internal static string Tool{get{
        var process=Environment.ProcessPath;var name=Path.GetFileNameWithoutExtension(process);
        if(process is null){return "Seamless.Management.Server";}
        if(string.Equals(name,"dotnet",StringComparison.OrdinalIgnoreCase)){return "dotnet \""+global::Seamless.Execution.CompilationReferences.PathFor(typeof(RecoveryGuideCommand).Assembly)+"\"";}
        return "\""+process+"\"";
    }}

    // 管理領域の中の印のファイルから、中断した移行を見つける。
    internal static InterruptedUpgrade[] Detect(string? root){
        if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root)){return [];}
        return RecoveryGuide.KnownUpgrades.Where(upgrade=>File.Exists(Path.Combine(root,upgrade.Marker))).ToArray();
    }

    internal static RecoveryGuideDocument Create(RecoveryScenario scenario,string? root,string? backup,string? error){
        if(root is not null){root=Path.GetFullPath(root);}
        if(backup is not null){backup=Path.GetFullPath(backup);}
        return RecoveryGuide.Create(scenario,new(Tool,root,backup,Version,error,Detect(root)));
    }

    internal static void Write(TextWriter output,RecoveryGuideDocument guide){
        output.WriteLine("【"+guide.Title+"】の手順");
        for(var index=0;index<guide.Steps.Length;index++){
            var step=guide.Steps[index];
            output.WriteLine((index+1)+". "+step.Text);
            if(step.Command is not null){output.WriteLine("   "+step.Command);}
            if(step.Folder is not null){output.WriteLine("   フォルダー: "+step.Folder);}
        }
        output.WriteLine();
        output.WriteLine("―― チャットAI に貼る問い合わせ文 ――");
        output.WriteLine(guide.ChatPrompt);
        output.WriteLine("―― エージェントに渡す自動復元の指示文 ――");
        output.WriteLine(guide.AgentPrompt);
    }

    // 手順に出てくる最初のフォルダー(無ければ管理領域)を開く。
    internal static string? Folder(RecoveryGuideDocument guide,string? root)=>guide.Steps.Select(step=>step.Folder).FirstOrDefault(folder=>folder is not null&&Directory.Exists(folder))??(root is not null&&Directory.Exists(root)?root:null);
    internal static void OpenFolder(string folder){
        if(!OperatingSystem.IsWindows()){throw new PlatformNotSupportedException("フォルダーを開くのは Windows だけです。");}
        using var _=Process.Start(new ProcessStartInfo("explorer.exe"){ArgumentList={folder},UseShellExecute=false});
    }
    // 管理領域を作業フォルダーにした新しいコンソールを開く(手順のコマンドをそのまま打てる)。
    internal static void OpenConsole(string folder){
        if(!OperatingSystem.IsWindows()){throw new PlatformNotSupportedException("コンソールを開くのは Windows だけです。");}
        using var _=Process.Start(new ProcessStartInfo("cmd.exe"){UseShellExecute=true,WorkingDirectory=folder});
    }

    // serve が起動に失敗したときに、続けて出す案内(エラー文を付けた recovery-guide のコマンド)。
    internal static string StartupHint(string[] args,string error){
        string? Value(string name){var index=Array.IndexOf(args,name);return index>=0&&index+1<args.Length?args[index+1]:null;}
        var root=Value("--root")??Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT");
        var command=Tool+" recovery-guide --scenario startup";
        if(root is not null){command+=" --root \""+root+"\"";}
        command+=" --error \""+error.Replace('"','\'').ReplaceLineEndings(" ")+"\"";
        return "起動できないときの手順と、チャットAI・エージェントに渡すプロンプトは次のコマンドで表示できます(--open-folder・--open-console でフォルダーとコンソールも開けます)。\n"+command;
    }
}
