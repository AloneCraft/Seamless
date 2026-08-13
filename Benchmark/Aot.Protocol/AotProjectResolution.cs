namespace Benchmark.Aot.Protocol;

public static class AOT正規プロジェクト解決 {
    public static string 解決(string ソリューションルート,string アセンブリ名) {
        ArgumentException.ThrowIfNullOrWhiteSpace(ソリューションルート);
        ArgumentException.ThrowIfNullOrWhiteSpace(アセンブリ名);
        if(!string.Equals(Path.GetFileName(アセンブリ名),アセンブリ名,StringComparison.Ordinal)
            ||アセンブリ名.IndexOfAny(Path.GetInvalidFileNameChars())>=0) {
            throw new InvalidDataException("不正なAOTアセンブリ名です: "+アセンブリ名);
        }

        var ルート=Path.GetFullPath(ソリューションルート);
        var プロジェクトパス=Path.GetFullPath(Path.Combine(ルート,"Benchmark",アセンブリ名,アセンブリ名+".csproj"));
        var ベンチマークルート=Path.GetFullPath(Path.Combine(ルート,"Benchmark"))+Path.DirectorySeparatorChar;
        if(!プロジェクトパス.StartsWith(ベンチマークルート,StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("AOT正規プロジェクトがBenchmarkルート外です。");
        }
        if(!File.Exists(プロジェクトパス)) {
            throw new FileNotFoundException($"AOT正規プロジェクトが見つかりません: {アセンブリ名}",プロジェクトパス);
        }
        return プロジェクトパス;
    }
}
