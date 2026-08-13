using System.Globalization;
using System.Text;
using System.Text.Json;

using UI;

namespace Benchmark.Headless;

internal static class AotGateRunner {
    public static async Task<int> 実行Async(IReadOnlyList<string> 引数一覧,CancellationToken キャンセルトークン) {
#if DEBUG
        throw new InvalidOperationException("AOTゲートはReleaseビルドでのみ実行できます。");
#else
        var モード=オプション値を取得(引数一覧,"--aot-gate")
            ??throw new ArgumentException("--aot-gate smoke|full を指定してください。");
        if(!string.Equals(モード,"smoke",StringComparison.OrdinalIgnoreCase)
            &&!string.Equals(モード,"full",StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("--aot-gate は smoke または full を指定してください。");
        }
        var 論理CPU=整数オプションを取得(引数一覧,"--cpu",0);
        if(論理CPU<0||論理CPU>=IntPtr.Size*8) {
            throw new ArgumentOutOfRangeException(nameof(論理CPU),論理CPU,"affinityで指定できない論理CPUです。");
        }
        var カタログ=ベンチマークカタログ.読み込む();
        var タブ別対象一覧=対象を解決(カタログ,モード);
        var ケース数=タブ別対象一覧.Sum(static 対象=>対象.ケース一覧.Count);
        if(引数一覧.Contains("--dry-run",StringComparer.OrdinalIgnoreCase)) {
            Console.WriteLine($"aot-gate mode={モード.ToLowerInvariant()} tabs={タブ別対象一覧.Count} cases={ケース数} cpu={論理CPU} N=4096 db=unused");
            return 0;
        }
        var 実行ルート=オプション値を取得(引数一覧,"--run-root");
        if(string.IsNullOrWhiteSpace(実行ルート)) {
            実行ルート=Path.Combine(
                Directory.GetCurrentDirectory(),
                "artifacts",
                "aot-gate",
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N"));
        }
        実行ルート=Path.GetFullPath(実行ルート);
        Directory.CreateDirectory(実行ルート);
        Console.WriteLine($"aot-gate start mode={モード} tabs={タブ別対象一覧.Count} cases={ケース数} cpu={論理CPU} root={実行ルート}");
        var 全結果一覧=new List<ベンチマーク結果項目>(ケース数);
        foreach(var タブ対象 in タブ別対象一覧) {
            キャンセルトークン.ThrowIfCancellationRequested();
            var タブルート=Path.Combine(実行ルート,ベンチマークソースタブ一覧.パス部分に変換(タブ対象.タブ.タブ));
            var 受信一覧=new List<ベンチマーク結果項目>(タブ対象.ケース一覧.Count);
            Console.WriteLine($"aot-gate tab={タブ対象.タブ.ID} cases={タブ対象.ケース一覧.Count} status=running");
            var サービス=new ベンチマーク実行サービス(new ベンチマークワーカープロセスサービス());
            await サービス.実行Async(
                タブ対象.ケース一覧,
                [論理CPU],
                ()=>[論理CPU],
                [4096],
                タブ対象.タブ.タブ,
                ベンチマーク実行プロファイル.AOT,
                TimeSpan.FromMilliseconds(100),
                true,
                タブルート,
                ()=>タブ対象.ケース一覧,
                受信一覧.Add,
                状態=>Console.WriteLine($"aot-gate tab={タブ対象.タブ.ID} {状態}"),
                null,
                キャンセルトークン);
            var 確定一覧=最終結果を検証(タブ対象.ケース一覧,受信一覧,論理CPU,タブ対象.タブ.ID);
            全結果一覧.AddRange(確定一覧);
            Console.WriteLine($"aot-gate tab={タブ対象.タブ.ID} cases={確定一覧.Count} status=completed");
        }
        var 概要=new AotGateSummary(
            "Completed",
            モード.ToLowerInvariant(),
            タブ別対象一覧.Count,
            全結果一覧.Count,
            4096,
            論理CPU,
            DateTimeOffset.UtcNow,
            全結果一覧.Select(static 結果=>new AotGateCaseSummary(
                結果.プロジェクト,
                結果.型完全名,
                結果.メソッド,
                結果.引数キー,
                結果.平均ナノ秒!.Value,
                結果.割り当てバイト数値!.Value,
                結果.結果ダイジェスト,
                結果.割り当て論理CPU!.Value)).ToArray());
        JSONを原子的に保存(Path.Combine(実行ルート,"aot-gate-summary.json"),概要);
        Console.WriteLine($"aot-gate completed tabs={概要.タブ数} cases={概要.ケース数} cpu={概要.論理CPU} root={実行ルート} db=unused");
        return 0;
#endif
    }

    internal static IReadOnlyList<AotGateTabTarget> 対象を解決(IReadOnlyList<ベンチマークケース> カタログ,string モード) {
        var 結果一覧=new List<AotGateTabTarget>(ベンチマークソースタブ一覧.標準全て.Count);
        foreach(var タブ in ベンチマークソースタブ一覧.標準全て) {
            var タブケース一覧=カタログ.Where(タブ.ケースを含むか).ToArray();
            if(タブケース一覧.Length==0) {
                throw new InvalidDataException("AOT対象タブにケースがありません: "+タブ.ID);
            }
            IReadOnlyList<ベンチマークケース> 対象ケース一覧;
            if(string.Equals(モード,"smoke",StringComparison.OrdinalIgnoreCase)) {
                対象ケース一覧=スモークペアを解決(タブ,タブケース一覧);
            } else {
                対象ケース一覧=タブケース一覧
                    .OrderBy(static ケース=>ケース.型完全名,StringComparer.Ordinal)
                    .ThenBy(static ケース=>ケース.メソッド名,StringComparer.Ordinal)
                    .ThenBy(static ケース=>ケース.引数キー,StringComparer.Ordinal)
                    .ThenBy(static ケース=>ケース.プロジェクト,StringComparer.Ordinal)
                    .ToArray();
            }
            結果一覧.Add(new(タブ,対象ケース一覧));
        }
        return 結果一覧;
    }

    private static IReadOnlyList<ベンチマークケース> スモークペアを解決(
        ベンチマークソースタブ定義 タブ,
        IReadOnlyList<ベンチマークケース> タブケース一覧) {
        var 左プロジェクト=タブ.プロジェクト順[0];
        var 右プロジェクト=タブ.プロジェクト順[1];
        var ペア=タブケース一覧
            .GroupBy(static ケース=>(型:型キーを作成(ケース.型完全名),ケース.メソッド名,ケース.引数キー))
            .Select(組=>new {
                左=組.SingleOrDefault(ケース=>string.Equals(ケース.プロジェクト,左プロジェクト,StringComparison.OrdinalIgnoreCase)),
                右=組.SingleOrDefault(ケース=>string.Equals(ケース.プロジェクト,右プロジェクト,StringComparison.OrdinalIgnoreCase)),
            })
            .Where(static 候補=>候補.左 is not null&&候補.右 is not null)
            .OrderBy(static 候補=>候補.左!.型完全名,StringComparer.Ordinal)
            .ThenBy(static 候補=>候補.左!.メソッド名,StringComparer.Ordinal)
            .ThenBy(static 候補=>候補.左!.引数キー,StringComparer.Ordinal)
            .FirstOrDefault()
            ??throw new InvalidDataException("AOTスモーク用の左右ペアを解決できません: "+タブ.ID);
        return [ペア.左!,ペア.右!];
    }

    private static IReadOnlyList<ベンチマーク結果項目> 最終結果を検証(
        IReadOnlyList<ベンチマークケース> 期待ケース一覧,
        IReadOnlyList<ベンチマーク結果項目> 受信一覧,
        int 論理CPU,
        string タブID) {
        var 最終結果一覧=受信一覧
            .GroupBy(static 結果=>string.Join("|",結果.プロジェクト,結果.型完全名,結果.メソッド,結果.引数キー),StringComparer.Ordinal)
            .Select(static 組=>組.Last())
            .ToArray();
        var 失敗結果=最終結果一覧.FirstOrDefault(static 結果=>!string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase));
        if(失敗結果 is not null) {
            throw new InvalidDataException($"AOTゲート測定が失敗しました: {失敗結果.型完全名}.{失敗結果.メソッド}({失敗結果.引数キー}) 理由={失敗結果.失敗理由}");
        }
        var 期待キー一覧=期待ケース一覧.Select(static ケース=>string.Join("|",ケース.プロジェクト,ケース.型完全名,ケース.メソッド名,ケース.引数キー)).ToHashSet(StringComparer.Ordinal);
        var 結果キー一覧=最終結果一覧.Select(static 結果=>string.Join("|",結果.プロジェクト,結果.型完全名,結果.メソッド,結果.引数キー)).ToHashSet(StringComparer.Ordinal);
        if(!期待キー一覧.SetEquals(結果キー一覧)) {
            throw new InvalidDataException($"AOTゲート結果のケース集合が一致しません: {タブID} 期待={期待キー一覧.Count} 実際={結果キー一覧.Count}");
        }
        foreach(var 結果 in 最終結果一覧) {
            if(!string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)
                ||結果.平均ナノ秒 is not { } 平均||!double.IsFinite(平均)
                ||結果.割り当てバイト数値 is not { } 割り当て||割り当て<0
                ||string.IsNullOrWhiteSpace(結果.結果ダイジェスト)
                ||結果.割り当て論理CPU!=論理CPU
                ||!結果.アフィニティ適用成功か
                ||結果.終了コード!=0) {
                throw new InvalidDataException($"AOTゲート結果が不完全です: {結果.型完全名}.{結果.メソッド}({結果.引数キー}) 状態={結果.状態} 理由={結果.失敗理由}");
            }
        }
        return 最終結果一覧;
    }

    private static int 整数オプションを取得(IReadOnlyList<string> 引数一覧,string 名前,int 既定値) {
        var 値=オプション値を取得(引数一覧,名前);
        if(値 is null) {
            return 既定値;
        }
        if(!int.TryParse(値,NumberStyles.Integer,CultureInfo.InvariantCulture,out var 結果)) {
            throw new ArgumentException($"{名前}には整数が必要です: {値}");
        }
        return 結果;
    }

    private static string? オプション値を取得(IReadOnlyList<string> 引数一覧,string 名前) {
        for(var インデックス=0;インデックス<引数一覧.Count;インデックス++) {
            if(string.Equals(引数一覧[インデックス],名前,StringComparison.OrdinalIgnoreCase)) {
                if(インデックス+1>=引数一覧.Count||string.IsNullOrWhiteSpace(引数一覧[インデックス+1])) {
                    throw new ArgumentException(名前+"の値が必要です。");
                }
                return 引数一覧[インデックス+1];
            }
        }
        return null;
    }

    private static string 型キーを作成(string 型完全名) {
        var 区切り=型完全名.LastIndexOf('.');
        var 型名=型完全名[(区切り+1)..];
        return 型名
            .Replace("IEnumerable",string.Empty,StringComparison.Ordinal)
            .Replace("ValueLinq",string.Empty,StringComparison.Ordinal)
            .Replace("FLinq",string.Empty,StringComparison.Ordinal)
            .Replace("ZLinq",string.Empty,StringComparison.Ordinal)
            .Replace("Linq",string.Empty,StringComparison.Ordinal);
    }

    private static void JSONを原子的に保存<T>(string パス,T 値) {
        var 一時パス=パス+".tmp-"+Guid.NewGuid().ToString("N");
        File.WriteAllText(一時パス,JsonSerializer.Serialize(値,new JsonSerializerOptions { WriteIndented=true }),new UTF8Encoding(false));
        File.Move(一時パス,パス,overwrite:true);
    }
}

internal sealed record AotGateTabTarget(ベンチマークソースタブ定義 タブ,IReadOnlyList<ベンチマークケース> ケース一覧);

internal sealed record AotGateSummary(
    string 状態,
    string モード,
    int タブ数,
    int ケース数,
    int 要素数,
    int 論理CPU,
    DateTimeOffset 完了日時,
    IReadOnlyList<AotGateCaseSummary> 結果一覧);

internal sealed record AotGateCaseSummary(
    string プロジェクト,
    string 型完全名,
    string メソッド名,
    string 引数キー,
    double 平均ナノ秒,
    long 割り当てバイト数,
    string 結果ダイジェスト,
    int 論理CPU);
