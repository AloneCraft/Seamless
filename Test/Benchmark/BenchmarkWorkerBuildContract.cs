using System.Xml.Linq;

namespace Test.Benchmark;

public sealed class BenchmarkWorkerBuildContract {
    [Fact,Trait("Category","Unit")]
    public void UIビルドは通常Workerのプロジェクト参照も必ずビルドする() {
        var ルート=ソリューションルートを検索();
        var プロジェクト=XDocument.Load(Path.Combine(ルート,"Benchmark","Benchmark.UI","Benchmark.UI.csproj"));
        // Worker はアセンブリとしては参照しない正式なプロジェクト参照にする。VS と MSBuild が UI より先に Worker をビルドする(BUG-0009)。
        var 参照=プロジェクト.Descendants("ProjectReference")
            .Single(項目=>((string?)項目.Attribute("Include")??string.Empty).EndsWith("Worker.csproj",StringComparison.OrdinalIgnoreCase));
        Assert.Equal("false",(string?)参照.Attribute("ReferenceOutputAssembly"));
        Assert.Equal("BenchmarkWorkerOutput",(string?)参照.Attribute("OutputItemType"));
        // コピーは参照が返した実際の出力先から行い、独自に Worker をビルドしない。
        var ターゲット=プロジェクト.Descendants("Target")
            .Single(項目=>(string?)項目.Attribute("Name")=="CopyBenchmarkWorkerHost");
        Assert.Empty(ターゲット.Elements("MSBuild"));
        Assert.Contains("@(BenchmarkWorkerOutput",ターゲット.ToString(),StringComparison.Ordinal);
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
