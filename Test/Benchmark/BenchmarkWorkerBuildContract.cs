using System.Xml.Linq;

namespace Test.Benchmark;

public sealed class BenchmarkWorkerBuildContract {
    [Fact,Trait("Category","Unit")]
    public void UIビルドは通常Workerのプロジェクト参照も必ずビルドする() {
        var ルート=ソリューションルートを検索();
        var プロジェクト=XDocument.Load(Path.Combine(ルート,"Benchmark","Benchmark.UI","Benchmark.UI.csproj"));
        var ターゲット=プロジェクト.Descendants("Target")
            .Single(項目=>(string?)項目.Attribute("Name")=="CopyBenchmarkWorkerHost");
        var MSBuild=ターゲット.Elements("MSBuild").Single();
        Assert.Contains("BuildProjectReferences=true",(string?)MSBuild.Attribute("Properties"));
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
