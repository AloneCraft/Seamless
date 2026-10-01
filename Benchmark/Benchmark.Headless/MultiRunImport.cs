using System.Text.Json;
using UI;
using UI.Storage;

namespace Benchmark.Headless;

/// <summary>
/// --verify-db-schema と --import-jsonl。
/// どちらも DB を作り直さない。版や形状が合わないときは、移行スクリプト(docs/db-migrations)を先に実行するよう促して失敗する。
/// </summary>
internal static class 計測結果取込 {
    /// <summary>--verify-db-schema [--db &lt;path&gt;]: Benchmark.db が現行の版・形状と一致するかだけを調べる。</summary>
    public static int スキーマを検査(string[] 引数) {
        var DBパス = DBパスを取得(引数);
        var ストア = new UI実行ストア(DBパス,UI実行アプリ種別.ベンチマーク);
        if(!ストア.既存スキーマが一致するか()) {
            Console.Error.WriteLine("Benchmark.db の版または形状が現行と一致しません(移行スクリプト docs/db-migrations を確認): "+DBパス);
            return 1;
        }
        Console.WriteLine("Benchmark.db は現行の版・形状と一致しています: "+DBパス);
        return 0;
    }

    /// <summary>
    /// --import-jsonl &lt;jsonl&gt;... [--db &lt;path&gt;]: --multi-run の結果を1つの実行として DB へ保存する。
    /// FLinq→左、Baseline(較正役)→右、Ideal→理想 の列へ入れる。ダイジェストが一致しなかった行は3列とも失敗として残す。
    /// 同じタブの同じ行を2回含めると後の行で上書きされるので、1回の取り込みには1周ぶんだけを渡す。
    /// </summary>
    public static int 取り込む(string[] 引数,IReadOnlyList<ベンチマークケース> カタログ) {
        var パス一覧 = 引数.SkipWhile(static 値 => !string.Equals(値,"--import-jsonl",StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .TakeWhile(static 値 => !値.StartsWith("--",StringComparison.Ordinal))
            .ToArray();
        if(パス一覧.Length==0) {
            throw new ArgumentException("--import-jsonl requires one or more JSONL paths.");
        }
        var DBパス = DBパスを取得(引数);
        var ストア = new UI実行ストア(DBパス,UI実行アプリ種別.ベンチマーク);
        if(!ストア.既存スキーマが一致するか()) {
            Console.Error.WriteLine("Benchmark.db の版または形状が現行と一致しないため取り込みません(移行スクリプト docs/db-migrations を先に実行): "+DBパス);
            return 1;
        }

        var 項目一覧 = new List<UIベンチマークスナップショット項目書込>();
        var タブ一覧 = new List<ベンチマークソースタブ定義>();
        var 論理CPU一覧 = new SortedSet<int>();
        foreach(var パス in パス一覧) {
            foreach(var 行 in File.ReadLines(パス)) {
                if(string.IsNullOrWhiteSpace(行)) {
                    continue;
                }
                using var 文書 = JsonDocument.Parse(行);
                var 項目 = 項目を作成(文書.RootElement,カタログ,out var タブ,out var 論理CPU);
                項目一覧.Add(項目);
                if(!タブ一覧.Contains(タブ)) {
                    タブ一覧.Add(タブ);
                }
                if(論理CPU is { } CPU) {
                    論理CPU一覧.Add(CPU);
                }
            }
        }
        if(項目一覧.Count==0) {
            throw new InvalidDataException("取り込む行がありません。");
        }

        var 時刻 = DateTimeOffset.UtcNow;
        var 実行 = ストア.実行を作成(new(
            UI実行アプリ種別.ベンチマーク,
            "理想3者計測 "+string.Join(",",パス一覧.Select(static パス => Path.GetFileNameWithoutExtension(パス)))+" "+時刻.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture),
            "MultiRun",
            タブ一覧[0].タブ.ToString(),
            時刻));
        ストア.実行設定を保存(実行.実行ID,Path.GetFullPath(パス一覧[0]),[1],10_000_000d,論理CPU一覧.ToArray());
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,項目一覧);
        ストア.実行状態を設定(実行.実行ID,UI実行状態.完了,$"Imported rows={項目一覧.Count}");
        Console.WriteLine($"imported-run={実行.実行ID} rows={項目一覧.Count} tabs={string.Join(",",タブ一覧.Select(static タブ => タブ.ID))}");
        return 0;
    }

    private static UIベンチマークスナップショット項目書込 項目を作成(
        JsonElement 行,
        IReadOnlyList<ベンチマークケース> カタログ,
        out ベンチマークソースタブ定義 タブ,
        out int? 論理CPU) {
        var タブID = 行.GetProperty("Tab").GetString()??string.Empty;
        タブ=ベンチマークソースタブ一覧.IDから取得を試みる(タブID)??throw new InvalidDataException("不明なタブです: "+タブID);
        論理CPU=null;
        if(行.TryGetProperty("LogicalCpu",out var CPU値)&&CPU値.ValueKind==JsonValueKind.Number) {
            論理CPU=CPU値.GetInt32();
        }
        var 引数キー = 行.GetProperty("ArgumentKey").GetString()??string.Empty;
        var 結果 = 行.GetProperty("Result");
        var 一致か = 結果.TryGetProperty("DigestMatched",out var 一致値)&&一致値.ValueKind==JsonValueKind.True;
        int? ループ回数 = null;
        if(結果.TryGetProperty("LoopCount",out var ループ値)&&ループ値.ValueKind==JsonValueKind.Number) {
            ループ回数=ループ値.GetInt32();
        }

        var 側一覧 = new Dictionary<string,(UIベンチマークグリッド側書込 側,UIベンチマークスナップショットケース書込 ケース,ベンチマークケース? カタログケース)>(StringComparer.Ordinal);
        foreach(var 実装 in 結果.GetProperty("Implementations").EnumerateArray()) {
            var キー = 実装.GetProperty("Key").GetString()??string.Empty;
            var アセンブリ = 実装.GetProperty("AssemblyName").GetString()??string.Empty;
            var 型 = 実装.GetProperty("TypeName").GetString()??string.Empty;
            var メソッド = 実装.GetProperty("MethodName").GetString()??string.Empty;
            var カタログケース = カタログ.FirstOrDefault(ケース =>
                string.Equals(ケース.アセンブリ名,アセンブリ,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(ケース.型完全名,型,StringComparison.Ordinal)
                &&string.Equals(ケース.メソッド名,メソッド,StringComparison.Ordinal)
                &&string.Equals(ケース.引数キー,引数キー,StringComparison.Ordinal));
            var 引数JSON = JsonSerializer.Serialize(カタログケース?.引数一覧??[]);
            var ケース = new UIベンチマークスナップショットケース書込(アセンブリ,型,メソッド,引数キー,引数JSON);
            側一覧[キー]=(側を作成(実装,一致か,ループ回数),ケース,カタログケース);
        }
        if(!側一覧.TryGetValue("FLinq",out var 左)||!側一覧.TryGetValue("Baseline",out var 右)) {
            throw new InvalidDataException("FLinq と Baseline の両方が必要です: "+タブID+" "+行.GetProperty("Method").GetString());
        }
        UIベンチマークグリッド側書込? 理想側 = null;
        UIベンチマークスナップショットケース書込? 理想ケース = null;
        if(側一覧.TryGetValue("Ideal",out var 理想)) {
            if(!タブ.理想ありか) {
                throw new InvalidDataException("理想列の無いタブに理想の結果があります: "+タブID);
            }
            理想側=理想.側;
            理想ケース=理想.ケース;
        }
        // 型列は UI と同じく、ベンチマークのパス表示(例 Array<int>)を使う。
        var 型名 = 左.カタログケース?.ベンチマークパス;
        if(string.IsNullOrWhiteSpace(型名)) {
            型名=左.ケース.型完全名;
        }
        return new UIベンチマークスナップショット項目書込(
            タブ.ID,
            型名,
            左.ケース.メソッド名,
            論理CPU,
            true,
            左.側,
            右.側,
            左.ケース,
            右.ケース,
            理想側,
            理想ケース);
    }

    private static UIベンチマークグリッド側書込 側を作成(JsonElement 実装,bool 一致か,int? ループ回数) {
        var 時刻 = DateTimeOffset.UtcNow;
        if(!一致か) {
            return new UIベンチマークグリッド側書込(UI実行状態.失敗,"3者の結果ダイジェストが一致しないため計測していません。",時刻,null,null,ループ回数);
        }
        if(!実装.TryGetProperty("Measurement",out var 計測)||計測.ValueKind!=JsonValueKind.Object) {
            return new UIベンチマークグリッド側書込(UI実行状態.失敗,"計測値がありません。",時刻,null,null,ループ回数);
        }
        var 中央値 = 計測.GetProperty("中央値ナノ秒").GetDouble();
        var 割当 = (long)Math.Round(計測.GetProperty("割当バイト数").GetDouble());
        double? 合計 = null;
        if(ループ回数 is { } 回数) {
            合計=中央値*回数;
        }
        return new UIベンチマークグリッド側書込(UI実行状態.完了,string.Empty,時刻,中央値,割当,ループ回数,合計,true);
    }

    private static string DBパスを取得(string[] 引数) {
        var 指定 = 引数.SkipWhile(static 値 => !string.Equals(値,"--db",StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if(!string.IsNullOrWhiteSpace(指定)) {
            return Path.GetFullPath(指定);
        }
        return UI実行DBパスリゾルバー.解決(UI実行アプリ種別.ベンチマーク);
    }
}
