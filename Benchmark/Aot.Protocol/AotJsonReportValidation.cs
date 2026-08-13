using System.Text.Json;

namespace Benchmark.Aot.Protocol;

public static class AOT完全JSONレポート検証 {
    public static string 検証(
        string 成果物ディレクトリ,
        string DotnetSDK,
        IReadOnlyList<AOTケース要求> ケース一覧) {
        ArgumentException.ThrowIfNullOrWhiteSpace(成果物ディレクトリ);
        ArgumentException.ThrowIfNullOrWhiteSpace(DotnetSDK);
        ArgumentNullException.ThrowIfNull(ケース一覧);
        var 結果ディレクトリ=Path.Combine(Path.GetFullPath(成果物ディレクトリ),"results");
        if(!Directory.Exists(結果ディレクトリ)) {
            throw new DirectoryNotFoundException("BenchmarkDotNet JSON結果ディレクトリがありません: "+結果ディレクトリ);
        }
        var レポート一覧=Directory.EnumerateFiles(結果ディレクトリ,"*-report-full.json",SearchOption.TopDirectoryOnly).ToArray();
        if(レポート一覧.Length!=1) {
            throw new InvalidDataException("BenchmarkDotNet完全JSONレポート件数が一致しません: "+レポート一覧.Length);
        }

        using var 文書=JsonDocument.Parse(File.ReadAllText(レポート一覧[0]));
        var ルート=文書.RootElement;
        if(!ルート.TryGetProperty("HostEnvironmentInfo",out var 環境)
            ||環境.ValueKind!=JsonValueKind.Object
            ||!環境.TryGetProperty("DotNetCliVersion",out var SDK要素)
            ||!string.Equals(SDK要素.GetString(),DotnetSDK,StringComparison.Ordinal)
            ||!環境.TryGetProperty("RuntimeVersion",out var Runtime要素)
            ||Runtime要素.GetString() is not { } Runtime
            ||!Runtime.StartsWith(".NET 10.",StringComparison.Ordinal)) {
            throw new InvalidDataException("BenchmarkDotNet完全JSONレポートの.NET 10 SDK/runtime情報が一致しません。");
        }
        if(!ルート.TryGetProperty("Benchmarks",out var Benchmark一覧)
            ||Benchmark一覧.ValueKind!=JsonValueKind.Array
            ||Benchmark一覧.GetArrayLength()!=ケース一覧.Count) {
            throw new InvalidDataException("BenchmarkDotNet完全JSONレポートのケース件数が一致しません。");
        }

        var 期待キー一覧=ケース一覧.Select(static ケース=>(ケース.メソッド名,引数:正規化(ケース.引数キー))).ToHashSet();
        var 実際キー一覧=new HashSet<(string メソッド名,string 引数)>();
        foreach(var Benchmark in Benchmark一覧.EnumerateArray()) {
            if(!Benchmark.TryGetProperty("Method",out var メソッド要素)
                ||メソッド要素.GetString() is not { } メソッド名
                ||!Benchmark.TryGetProperty("Parameters",out var 引数要素)) {
                throw new InvalidDataException("BenchmarkDotNet完全JSONレポートのケース識別値が不足しています。");
            }
            var キー=(メソッド名,正規化(引数要素.GetString()??string.Empty));
            if(!実際キー一覧.Add(キー)) {
                throw new InvalidDataException($"BenchmarkDotNet完全JSONレポートに重複ケースがあります: {キー.メソッド名}({キー.Item2})");
            }
            if(!Benchmark.TryGetProperty("Statistics",out var 統計)
                ||統計.ValueKind!=JsonValueKind.Object
                ||!有限値か(統計,"Mean")
                ||!有限値か(統計,"StandardError")
                ||!有限値か(統計,"StandardDeviation")) {
                throw new InvalidDataException($"BenchmarkDotNet完全JSONレポートの統計が不完全です: {キー.メソッド名}({キー.Item2})");
            }
        }
        if(!期待キー一覧.SetEquals(実際キー一覧)) {
            throw new InvalidDataException("BenchmarkDotNet完全JSONレポートに欠落または未要求ケースがあります。");
        }
        return レポート一覧[0];
    }

    private static bool 有限値か(JsonElement 統計,string 名前) =>
        統計.TryGetProperty(名前,out var 要素)
        &&要素.ValueKind==JsonValueKind.Number
        &&要素.TryGetDouble(out var 値)
        &&double.IsFinite(値);

    private static string 正規化(string 値) =>
        string.Join(",",値.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries));
}
