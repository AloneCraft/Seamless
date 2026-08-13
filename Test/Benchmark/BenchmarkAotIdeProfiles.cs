using System.Text.Json;

namespace Test.Benchmark;

public sealed class BenchmarkAotIdeProfiles {
    [Fact,Trait("Category","Unit")]
    public void VSCodeとVisualStudioにAOT起動プロファイルがある() {
        var ルート=ソリューションルートを検索();
        using var VSCode=JsonDocument.Parse(File.ReadAllText(Path.Combine(ルート,".vscode","launch.json")));
        var プロファイル=VSCode.RootElement.GetProperty("configurations").EnumerateArray()
            .Single(項目=>項目.GetProperty("name").GetString()=="Benchmark.UI AOT");
        Assert.Equal("build-benchmark-ui-aot-release",プロファイル.GetProperty("preLaunchTask").GetString());
        Assert.Equal("AOT",プロファイル.GetProperty("env").GetProperty(global::UI.ベンチマーク起動実行プロファイル.環境変数名).GetString());

        using var タスク=JsonDocument.Parse(File.ReadAllText(Path.Combine(ルート,".vscode","tasks.json")));
        Assert.Contains(タスク.RootElement.GetProperty("tasks").EnumerateArray(),項目=>項目.GetProperty("label").GetString()=="build-benchmark-ui-aot-release");

        using var VisualStudio=JsonDocument.Parse(File.ReadAllText(Path.Combine(ルート,"Benchmark","Benchmark.UI","Properties","launchSettings.json")));
        var VSプロファイル=VisualStudio.RootElement.GetProperty("profiles").GetProperty("Benchmark.UI AOT");
        Assert.Equal("Project",VSプロファイル.GetProperty("commandName").GetString());
        Assert.Equal("AOT",VSプロファイル.GetProperty("environmentVariables").GetProperty(global::UI.ベンチマーク起動実行プロファイル.環境変数名).GetString());
    }

    private static string ソリューションルートを検索() {
        var ディレクトリ=new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null) {
            if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))) {
                return ディレクトリ.FullName;
            }
            ディレクトリ=ディレクトリ.Parent;
        }
        throw new DirectoryNotFoundException("Seamless.slnxが見つかりません。");
    }
}
