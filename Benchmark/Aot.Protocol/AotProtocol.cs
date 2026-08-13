using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Aot.Protocol;

public static class AOT契約 {
    public const int 現在バージョン=1;
    public const int 起動回数=1;
    public const int ウォームアップ回数=1;
    public const int 実測回数=7;
    public const int 実測時間ミリ秒=100;
    public const string 応答ファイル名="response.json";
    public const string 完了ファイル名="completed.json";
    public const string 事前検査応答ファイル名="preflight.json";

    public static JsonSerializerOptions JSON設定{get;}=new() {
        PropertyNamingPolicy=null,
        PropertyNameCaseInsensitive=false,
        WriteIndented=true,
        DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull,
    };

    public static string 指紋を作成(string 値) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(値)));

    public static TimeSpan タイムアウトを計算(int ケース数) {
        if(ケース数<1) {
            throw new ArgumentOutOfRangeException(nameof(ケース数));
        }
        var 分=Math.Max(30,10+ケース数);
        return TimeSpan.FromMinutes(Math.Min(240,分));
    }
}

public sealed record AOTジョブ設定(
    int 起動回数=AOT契約.起動回数,
    int ウォームアップ回数=AOT契約.ウォームアップ回数,
    int 実測回数=AOT契約.実測回数,
    int 実測時間ミリ秒=AOT契約.実測時間ミリ秒);

public sealed record AOT引数要求(string 名前,string 型完全名,string 値);

public sealed record AOTケース要求(
    string プロジェクト,
    string メソッド名,
    string 引数キー,
    IReadOnlyList<AOT引数要求> 引数一覧,
    int 要素数,
    int 実行項目インデックス);

public sealed record AOTワーカー要求(
    int 契約バージョン,
    string 実行ID,
    string アセンブリ名,
    string アセンブリパス,
    string 型完全名,
    IReadOnlyList<AOTケース要求> ケース一覧,
    int 論理CPU,
    string 成果物ディレクトリ,
    AOTジョブ設定 設定,
    string ツールチェーン指紋,
    string 要求指紋);

public sealed record AOTケース結果(
    string メソッド名,
    string 引数キー,
    string 状態,
    double? 平均ナノ秒,
    double? 誤差ナノ秒,
    double? 標準偏差ナノ秒,
    long? 割り当てバイト数,
    string 結果ダイジェスト,
    string 失敗理由="");

public sealed record AOTワーカー応答(
    int 契約バージョン,
    string 状態,
    bool NativeAOTか,
    int 論理CPU,
    bool アフィニティ指定成功か,
    string ツールチェーン指紋,
    string 要求指紋,
    IReadOnlyList<AOTケース結果> 結果一覧,
    string 成果物ディレクトリ,
    string ログパス,
    string 失敗理由="");

public sealed record AOT事前検査応答(
    int 契約バージョン,
    string 状態,
    string ツールチェーン指紋,
    string DotnetSDK,
    string VisualStudioパス,
    string Linkerパス,
    string WindowsSDKパス,
    string Probe実行ファイル,
    string ログパス,
    string 失敗理由="");

public static class AOT要求検証 {
    public static void 検証(AOTワーカー要求 要求) {
        ArgumentNullException.ThrowIfNull(要求);
        if(要求.契約バージョン!=AOT契約.現在バージョン) {
            throw new InvalidDataException($"AOT契約バージョンが一致しません。要求={要求.契約バージョン} 対応={AOT契約.現在バージョン}");
        }
        if(string.IsNullOrWhiteSpace(要求.実行ID)
            ||string.IsNullOrWhiteSpace(要求.アセンブリ名)
            ||string.IsNullOrWhiteSpace(要求.アセンブリパス)
            ||string.IsNullOrWhiteSpace(要求.型完全名)
            ||string.IsNullOrWhiteSpace(要求.成果物ディレクトリ)
            ||string.IsNullOrWhiteSpace(要求.ツールチェーン指紋)
            ||string.IsNullOrWhiteSpace(要求.要求指紋)) {
            throw new InvalidDataException("AOT要求の必須値が不足しています。");
        }
        if(要求.論理CPU<0||要求.論理CPU>=IntPtr.Size*8) {
            throw new InvalidDataException($"AOT affinityで扱えない論理CPUです: {要求.論理CPU}");
        }
        if(要求.設定!=new AOTジョブ設定()) {
            throw new InvalidDataException("AOTジョブ設定はLaunch=1/Warmup=1/Iteration=7/IterationTime=100ms固定です。");
        }
        if(要求.ケース一覧.Count==0) {
            throw new InvalidDataException("AOT要求にケースがありません。");
        }
        var 重複=要求.ケース一覧
            .GroupBy(static ケース=>(ケース.メソッド名,ケース.引数キー))
            .FirstOrDefault(static 組=>組.Count()!=1);
        if(重複 is not null) {
            throw new InvalidDataException($"AOT要求のケースが重複しています: {重複.Key.メソッド名}({重複.Key.引数キー})");
        }
    }
}
