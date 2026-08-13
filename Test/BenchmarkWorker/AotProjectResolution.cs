using Benchmark.Aot.Protocol;

namespace Test.BenchmarkWorker;

public sealed class AOT正規プロジェクト解決テスト {
    [Fact,Trait("Category","Unit")]
    public void 同名退避成果物があってもBenchmark直下の正規パスだけを選ぶ() {
        var ルート=Path.Combine(AppContext.BaseDirectory,"AotProjectResolution",Guid.NewGuid().ToString("N"));
        var アセンブリ名="Benchmark.Sample";
        var 正規パス=Path.Combine(ルート,"Benchmark",アセンブリ名,アセンブリ名+".csproj");
        var 退避パス=Path.Combine(ルート,"Artifacts","decompiled",アセンブリ名+".csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(正規パス)!);
        Directory.CreateDirectory(Path.GetDirectoryName(退避パス)!);
        File.WriteAllText(正規パス,"<Project />");
        File.WriteAllText(退避パス,"<Project />");

        var 実際=AOT正規プロジェクト解決.解決(ルート,アセンブリ名);

        Assert.Equal(Path.GetFullPath(正規パス),実際);
    }

    [Fact,Trait("Category","Unit")]
    public void 正規パス以外へはフォールバックしない() {
        var ルート=Path.Combine(AppContext.BaseDirectory,"AotProjectResolution",Guid.NewGuid().ToString("N"));
        var 退避パス=Path.Combine(ルート,"Artifacts","decompiled","Benchmark.Sample.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(退避パス)!);
        File.WriteAllText(退避パス,"<Project />");

        Assert.Throws<FileNotFoundException>(()=>AOT正規プロジェクト解決.解決(ルート,"Benchmark.Sample"));
        Assert.Throws<InvalidDataException>(()=>AOT正規プロジェクト解決.解決(ルート,".."));
    }
}
