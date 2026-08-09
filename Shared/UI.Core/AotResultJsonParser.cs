using System.Text.Json;

namespace UI;

internal static class Aot結果JSON解析器{
    public sealed record Aotベンチマーク結果(
        string メソッド名,
        string 引数キー,
        double?平均ナノ秒,
        double?誤差ナノ秒,
        double?標準偏差ナノ秒,
        long?割り当てバイト数);

    public static IReadOnlyList<Aotベンチマーク結果>解析(string 成果物JSONパス){
        using var ストリーム=File.OpenRead(成果物JSONパス);
        using var ドキュメント=JsonDocument.Parse(ストリーム);
        var 結果一覧=new List<Aotベンチマーク結果>();
        if(!ドキュメント.RootElement.TryGetProperty("Benchmarks",out var ベンチマーク一覧)){
            return 結果一覧;
        }
        foreach(var ベンチマーク in ベンチマーク一覧.EnumerateArray()){
            var メソッド名=ベンチマーク.TryGetProperty("Method",out var メソッド要素)?メソッド要素.GetString()??string.Empty:string.Empty;
            var 引数キー=引数キーへ変換(ベンチマーク);
            double?平均ナノ秒=null;
            double?誤差ナノ秒=null;
            double?標準偏差ナノ秒=null;
            if(ベンチマーク.TryGetProperty("Statistics",out var 統計)&&統計.ValueKind!=JsonValueKind.Null){
                平均ナノ秒=数値を取得(統計,"Mean");
                誤差ナノ秒=数値を取得(統計,"StandardError");
                標準偏差ナノ秒=数値を取得(統計,"StandardDeviation");
            }
            long?割り当てバイト数=null;
            if(ベンチマーク.TryGetProperty("Memory",out var メモリ)&&メモリ.ValueKind!=JsonValueKind.Null){
                割り当てバイト数=長整数を取得(メモリ,"BytesAllocatedPerOperation")??長整数を取得(メモリ,"AllocatedBytes");
            }
            結果一覧.Add(new(メソッド名,引数キー,平均ナノ秒,誤差ナノ秒,標準偏差ナノ秒,割り当てバイト数));
        }
        return 結果一覧;
    }

    private static string 引数キーへ変換(JsonElement ベンチマーク){
        //BenchmarkDotNetのParameters文字列("名前=値, 名前2=値2"形式)を、
        //既存のベンチマーク引数フォーマッター.キーを作成と同じ"名前=値,名前2=値2"形式(空白なし)へ正規化する。
        if(!ベンチマーク.TryGetProperty("Parameters",out var パラメータ要素)){
            return string.Empty;
        }
        var 生文字列=パラメータ要素.GetString();
        if(string.IsNullOrEmpty(生文字列)){
            return string.Empty;
        }
        var 分割済み=生文字列.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        return string.Join(",",分割済み);
    }

    private static double?数値を取得(JsonElement 親要素,string プロパティ名){
        if(親要素.TryGetProperty(プロパティ名,out var 要素)&&要素.ValueKind==JsonValueKind.Number){
            return 要素.GetDouble();
        }
        return null;
    }

    private static long?長整数を取得(JsonElement 親要素,string プロパティ名){
        if(親要素.TryGetProperty(プロパティ名,out var 要素)&&要素.ValueKind==JsonValueKind.Number){
            return 要素.GetInt64();
        }
        return null;
    }
}
