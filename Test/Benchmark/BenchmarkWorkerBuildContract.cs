using System.Xml.Linq;

namespace Test.Benchmark;

public sealed class BenchmarkWorkerBuildContract {
    [Fact,Trait("Category","Unit")]
    public void DynamicUIビルドはDynamicWorkerのプロジェクト参照もビルドする() {
        var ルート=ソリューションルートを検索();
        var プロジェクト=XDocument.Load(Path.Combine(ルート,"Benchmark","Benchmark.Dynamic.UI","Benchmark.Dynamic.UI.csproj"));
        // Dynamic Worker はアセンブリ参照せず、UI のプロジェクト参照からビルドして配置する。
        var 参照=プロジェクト.Descendants("ProjectReference")
            .Single(項目=>((string?)項目.Attribute("Include")??string.Empty).EndsWith("Benchmark.Dynamic.Worker.csproj",StringComparison.OrdinalIgnoreCase));
        Assert.Equal("false",(string?)参照.Attribute("ReferenceOutputAssembly"));
        Assert.Equal("BenchmarkDynamicWorkerOutput",(string?)参照.Attribute("OutputItemType"));
        // コピーは参照が返した実際の出力先から行い、独自に Worker をビルドしない。
        var ターゲット=プロジェクト.Descendants("Target")
            .Single(項目=>(string?)項目.Attribute("Name")=="CopyBenchmarkDynamicWorker");
        Assert.Empty(ターゲット.Elements("MSBuild"));
        Assert.Contains("@(BenchmarkDynamicWorkerOutput",ターゲット.ToString(),StringComparison.Ordinal);
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
