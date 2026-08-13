using Benchmark.Aot.Protocol;

namespace Test.BenchmarkWorker;

public sealed class AOT完全JSONレポート検証テスト {
    [Fact,Trait("Category","Unit")]
    public void 正常な一件だけを受理する() {
        var ルート=一時ルートを作成();
        JSONを保存(ルート,"10.0.400",".NET 10.0.11",BenchmarkJSON("Method","Count=4096",平均:"1"));

        var 実際=AOT完全JSONレポート検証.検証(ルート,"10.0.400",[ケースを作成("Method","Count=4096")]);

        Assert.EndsWith("-report-full.json",実際,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void 欠落重複SDK不一致と不完全統計を拒否する() {
        var 欠落=一時ルートを作成();
        Assert.Throws<DirectoryNotFoundException>(()=>AOT完全JSONレポート検証.検証(欠落,"10.0.400",[ケースを作成("Method","")]));

        var 重複=一時ルートを作成();
        JSONを保存(重複,"10.0.400",".NET 10.0.11",BenchmarkJSON("Method","",平均:"1"),BenchmarkJSON("Method","",平均:"1"));
        Assert.Throws<InvalidDataException>(()=>AOT完全JSONレポート検証.検証(重複,"10.0.400",[ケースを作成("Method",""),ケースを作成("Other","")]));

        var SDK不一致=一時ルートを作成();
        JSONを保存(SDK不一致,"11.0.100",".NET 10.0.11",BenchmarkJSON("Method","",平均:"1"));
        Assert.Throws<InvalidDataException>(()=>AOT完全JSONレポート検証.検証(SDK不一致,"10.0.400",[ケースを作成("Method","")]));

        var Null統計=一時ルートを作成();
        JSONを保存(Null統計,"10.0.400",".NET 10.0.11","{\"Method\":\"Method\",\"Parameters\":\"\",\"Statistics\":null}");
        Assert.Throws<InvalidDataException>(()=>AOT完全JSONレポート検証.検証(Null統計,"10.0.400",[ケースを作成("Method","")]));

        var 非有限=一時ルートを作成();
        JSONを保存(非有限,"10.0.400",".NET 10.0.11",BenchmarkJSON("Method","",平均:"1e400"));
        Assert.Throws<InvalidDataException>(()=>AOT完全JSONレポート検証.検証(非有限,"10.0.400",[ケースを作成("Method","")]));
    }

    private static AOTケース要求 ケースを作成(string メソッド,string 引数) =>
        new("Project",メソッド,引数,[],4096,0);

    private static string 一時ルートを作成() {
        var ルート=Path.Combine(AppContext.BaseDirectory,"AotJsonReportValidation",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ルート);
        return ルート;
    }

    private static void JSONを保存(string ルート,string SDK,string Runtime,params string[] Benchmarks) {
        var 結果=Path.Combine(ルート,"results");
        Directory.CreateDirectory(結果);
        File.WriteAllText(
            Path.Combine(結果,"sample-report-full.json"),
            "{\"HostEnvironmentInfo\":{\"DotNetCliVersion\":\""+SDK+"\",\"RuntimeVersion\":\""+Runtime+"\"},\"Benchmarks\":["+string.Join(',',Benchmarks)+"]}");
    }

    private static string BenchmarkJSON(string メソッド,string 引数,string 平均) =>
        "{\"Method\":\""+メソッド+"\",\"Parameters\":\""+引数+"\",\"Statistics\":{\"Mean\":"+平均+",\"StandardError\":1,\"StandardDeviation\":1}}";
}
