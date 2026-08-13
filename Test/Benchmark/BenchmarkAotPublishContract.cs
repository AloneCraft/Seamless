using System.Text.Json;
using System.Xml.Linq;

namespace Test.Benchmark;

public sealed class BenchmarkAotPublishContract {
    [Fact,Trait("Category","Unit")]
    public void VSCodeのAOTタスクはUIをReleaseでビルドしRIDを伝播しない() {
        var ルート=ソリューションルートを検索();
        using var 文書=JsonDocument.Parse(File.ReadAllText(Path.Combine(ルート,".vscode","tasks.json")));
        var タスク=文書.RootElement.GetProperty("tasks").EnumerateArray()
            .Single(項目=>項目.GetProperty("label").GetString()=="build-benchmark-ui-aot-release");
        var 引数一覧=タスク.GetProperty("args").EnumerateArray().Select(static 項目=>項目.GetString()).ToArray();
        Assert.Contains("Release",引数一覧);
        Assert.DoesNotContain("-r",引数一覧);
        Assert.DoesNotContain("win-x64",引数一覧);
    }

    [Fact,Trait("Category","Unit")]
    public void UIビルドはAOTWorkerへframeworkDependentRelease一式をpublishする() {
        var ルート=ソリューションルートを検索();
        var プロジェクト=XDocument.Load(Path.Combine(ルート,"Benchmark","Benchmark.UI","Benchmark.UI.csproj"));
        var ターゲット=プロジェクト.Descendants("Target")
            .Single(項目=>(string?)項目.Attribute("Name")=="PublishBenchmarkAotWorkerHost");
        var MSBuild=ターゲット.Elements("MSBuild").Single();
        Assert.Equal("Restore;Publish",(string?)MSBuild.Attribute("Targets"));
        Assert.Contains("Configuration=Release",(string?)MSBuild.Attribute("Properties"));
        Assert.Contains("SelfContained=false",(string?)MSBuild.Attribute("Properties"));
        Assert.Contains("PublishDir=",(string?)MSBuild.Attribute("Properties"));
        Assert.Contains("TargetFramework",(string?)MSBuild.Attribute("RemoveProperties"));
        Assert.Contains("RuntimeIdentifier",(string?)MSBuild.Attribute("RemoveProperties"));
        Assert.Contains("AOTWorker",ターゲット.ToString());
        Assert.Contains("**\\*",ターゲット.ToString());
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
