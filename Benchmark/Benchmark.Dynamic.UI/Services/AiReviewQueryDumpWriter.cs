using Benchmark.Dynamic.Core;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Benchmark.Dynamic.UI;

public enum AiReviewDumpFilter {
    RandomOnly,
    AllStages,
    NeedsImprovementOnly,
}

/// <summary>
/// ランダム探索やライブ検証で生成されたクエリとFLinq最適化後展開コードのペアを、
/// AI（Gemini/ChatGPT）でバッチ監査しやすい連番テキストファイル群に分割出力するライターです。
/// </summary>
public sealed class AiReviewQueryDumpWriter : IDisposable {
    private readonly string outputDirectory;
    private readonly string runPrefix;
    private readonly int chunkSize;
    private readonly AiReviewDumpFilter filter;
    private readonly object gate = new();
    private int totalWritten;
    private int currentChunkIndex;
    private StreamWriter? currentWriter;
    private bool disposed;

    public string OutputDirectory => this.outputDirectory;

    public AiReviewQueryDumpWriter(string? baseDirectory = null, int chunkSize = 100, AiReviewDumpFilter filter = AiReviewDumpFilter.RandomOnly) {
        this.chunkSize = Math.Max(1, chunkSize);
        this.filter = filter;
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? ResolveDefaultDumpRoot()
            : Path.GetFullPath(baseDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        this.outputDirectory = root;
        this.runPrefix = $"run_{timestamp}";
        Directory.CreateDirectory(this.outputDirectory);
        this.WritePromptRulesFile();
    }

    public static string ResolveDefaultDumpRoot() {
        var dataRoot = Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(dataRoot)) {
            return Path.Combine(Path.GetFullPath(dataRoot), "AiReviewDumps");
        }
        var slnRoot = FindSolutionRoot() ?? AppContext.BaseDirectory;
        return Path.Combine(Path.GetFullPath(slnRoot), "Work", "Benchmark", "AiReviewDumps");
    }

    private static string? FindSolutionRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "Seamless.slnx"))) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    public bool ShouldDump(動的ライブ結果 結果) {
        if (結果 is null) {
            return false;
        }
        if (this.filter == AiReviewDumpFilter.RandomOnly && 結果.段階 != 動的ライブ段階.ランダム探索) {
            return false;
        }
        if (this.filter == AiReviewDumpFilter.NeedsImprovementOnly) {
            var system = 結果.計測一覧.FirstOrDefault(static m => m.実装 == "System.Linq");
            var flinq = 結果.計測一覧.FirstOrDefault(static m => m.実装 == "FLinq");
            var zlinq = 結果.計測一覧.FirstOrDefault(static m => m.実装 == "ZLinq");
            if (system is not null && flinq is not null) {
                // FLinqがSystemより遅い、または割当が多い
                var slowerThanSystem = flinq.中央値ナノ秒 > system.中央値ナノ秒 * 1.05;
                var moreAllocThanSystem = flinq.割当バイト > system.割当バイト;
                var slowerThanZLinq = zlinq is not null && flinq.中央値ナノ秒 > zlinq.中央値ナノ秒 * 1.05;
                if (!slowerThanSystem && !moreAllocThanSystem && !slowerThanZLinq && 結果.合格か) {
                    return false;
                }
            }
        }
        return true;
    }

    public void Write(動的ライブ結果 結果) {
        if (!this.ShouldDump(結果)) {
            return;
        }

        lock (this.gate) {
            if (this.disposed) {
                return;
            }

            if (this.currentWriter is null || this.totalWritten % this.chunkSize == 0) {
                this.RotateChunk();
            }

            var pairText = FormatQueryPair(結果, this.totalWritten + 1);
            this.currentWriter!.Write(pairText);
            this.currentWriter.Flush();
            this.totalWritten++;
        }
    }

    private void RotateChunk() {
        this.currentWriter?.Dispose();
        this.currentChunkIndex++;
        var startNum = (this.currentChunkIndex - 1) * this.chunkSize + 1;
        var endNum = this.currentChunkIndex * this.chunkSize;
        var fileName = $"{this.runPrefix}_part_{this.currentChunkIndex:D4}_q{startNum:D6}_{endNum:D6}.txt";
        var path = Path.Combine(this.outputDirectory, fileName);
        this.currentWriter = new StreamWriter(path, append: false, Encoding.UTF8);
        this.currentWriter.WriteLine(GetPromptHeader(this.currentChunkIndex, startNum, endNum));
        this.currentWriter.Flush();
    }

    private void WritePromptRulesFile() {
        var path = Path.Combine(this.outputDirectory, "00_PROMPT_RULES.md");
        File.WriteAllText(path, GetPromptRulesMarkdown(), Encoding.UTF8);
    }

    public static string CleanExpandedSource(string expandedSource) {
        if (string.IsNullOrWhiteSpace(expandedSource)) {
            return "// (展開コードなし)";
        }

        var lines = expandedSource.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var filteredLines = new List<string>(lines.Length);
        var skipFile = false;

        foreach (var line in lines) {
            // 自動生成ヘッダーやTraceの巨大Base64をスキップ
            if (line.StartsWith("// FLinqOptimizationTrace:", StringComparison.Ordinal)
                || line.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase)
                || line.Contains("InterceptsLocationAttribute", StringComparison.Ordinal)
                || line.Contains("LinqRewriteAttributes", StringComparison.Ordinal)) {
                continue;
            }

            // ファイル区切りや属性定義ファイルのスキップ
            if (line.StartsWith("// Source: InterceptsLocationAttribute.g.cs", StringComparison.Ordinal)
                || line.StartsWith("// Source: LinqRewriteAttributes.g.cs", StringComparison.Ordinal)
                || line.Contains(".OptimizationTrace.g.cs", StringComparison.Ordinal)) {
                skipFile = true;
                continue;
            }
            if (skipFile && line.StartsWith("// Source: ", StringComparison.Ordinal)) {
                skipFile = false;
            }
            if (skipFile) {
                continue;
            }

            filteredLines.Add(line);
        }

        var text = string.Join(Environment.NewLine, filteredLines).Trim();

        // RunFLinqCore のインターセプター部分を優先抽出（可能なら）
        var optimizedCoreIndex = text.IndexOf("RunFLinqCore", StringComparison.Ordinal);
        if (optimizedCoreIndex >= 0) {
            // 周辺メソッドとクラス構造を残す
            return text;
        }

        return string.IsNullOrWhiteSpace(text) ? expandedSource.Trim() : text;
    }

    public static string FormatQueryPair(動的ライブ結果 結果, int index) {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($"[ペア #{index}] QueryId: {結果.QueryId} | 段階: {結果.段階} | Target: {結果.対象} | Type: {結果.要素型} | 要素数: {結果.入力要素数}");
        sb.AppendLine($"【判定】 {結果.状態} (全経路一致: {結果.全経路一致か})");
        sb.Append("【計測サマリ】 ");
        if (結果.計測一覧.Count == 0) {
            sb.AppendLine("未計測");
        } else {
            var metrics = string.Join(" | ", 結果.計測一覧.Select(static m => $"{m.実装}: {m.中央値ナノ秒:N1}ns / {m.割当バイト:N0}B"));
            sb.AppendLine(metrics);
        }
        sb.AppendLine("【LINQ クエリ式】");
        sb.AppendLine(string.IsNullOrWhiteSpace(結果.式) ? "(式なし)" : 結果.式.Trim());
        sb.AppendLine("【FLinq 展開後 C# コード】");
        sb.AppendLine("```csharp");
        sb.AppendLine(CleanExpandedSource(結果.展開コード));
        sb.AppendLine("```");
        sb.AppendLine();
        return sb.ToString();
    }

    public static string GetPromptHeader(int partIndex, int startNum, int endNum) {
        return $@"# FLinq 展開コード 理想形チェック依頼 (Part {partIndex:D4}: #{startNum} ～ #{endNum})

あなたは C# / .NET ランタイム / SIMD / 高性能 LINQ コンパイラ (Source Generator) のシニアパフォーマンスエンジニアです。
以下に、自動生成された【入力 LINQ クエリ式】【実測パフォーマンス比較】【FLinq が生成した展開 C# コード】のペアを提示します。
各ペアの展開コードを精査し、「手書きの理想的な C# コード」と比較して非効率な箇所（最適化漏れ・不要なアロケーション・冗長なループ等）があるペアを特定し、改善案を提示してください。
（※問題のないペアは出力を省略し、改善余地のあるペアのみ報告してください。）

{GetSharedChecklist()}
";
    }

    public static string GetPromptRulesMarkdown() {
        return $@"# FLinq 展開コード 監査ルール・チェックリスト仕様書

{GetSharedChecklist()}
";
    }

    private static string GetSharedChecklist() {
        return @"## 1. FLinq 固有のアーキテクチャ・厳守ルール（誤検知を防ぐための前提知識）
1. **System.Linq への実行時フォールバック禁止**: すべての呼び出しはコンパイル時に展開されます。
2. **意味・順序・例外型の完全一致**:
   - `System.Linq` と「戻り値」「要素順序」「送出される例外の型」「短絡によるラムダ評価回数」が 100% 一致する必要があります。
   - **空要素例外（Average/Min/Max/First 等）**: ループ内では状態更新のみを行って1ループ融合し、ループ直後の確定フェーズで元の記述順に例外（InvalidOperationException）を投げるのは正当かつ理想的な動作です。
   - **ループ内例外（checked 加算の OverflowException / Single の2要素目例外等）や短絡演算子（&&, ||）の競合**: 例外順序や短絡未評価を守るために、意図的にループを複数グループへ分割し中間作業領域で繋いでいる場合があります。例外順序を破壊する無理な1ループ融合は提案しないでください。
3. **永続セット（PersistedSet / Tsavorite）のトランザクション保護**:
   - QueryGrouping.Materializer や QueryOrdering.InputBudget / KeyBudget、同一スレッド制約（SameThread）のチェックはストレージ安全性のために必須です。

## 2. 厳しくチェックしてほしい 8 つの観点（アンチパターン）
1. **【冗長な複数ループ / 融合漏れ】**: 1パスで同時に集計・射影できるものが複数ループに分かれていないか。
2. **【不要な GC ヒープ割当（new）の残存】**: new 配列、List、Dictionary、DictionarySlim、HashSetSlim、匿名型 new { ... }、クロージャクラス、ボックス化が残っていないか（ValueTuple / スカラー変数 / スタック構造体に置換できないか）。
3. **【stackalloc / [InlineArray(16)] への置換余地】**: 小サイズ・有界サイズ（ElementAt(^n), TakeLast(n), 16要素以下の OrderBy/Reverse/作業領域/小規模ハッシュ表）で、stackalloc や [InlineArray(16)] を使わずに毎回 ArrayPool.Rent を呼んでいないか。
4. **【不要な全件バッファリング】**: Take(K), TakeLast(N), First 等で、有界作業領域で済むのに入力全件を ArrayPool に貯め込んでいないか。
5. **【Where の葉移動（Filter Pushdown）や不変式のホイスト漏れ】**: GroupBy や Join の後のキー条件 Where を上流へ押し下げられないか。ループ不変な内側パイプライン（SelectMany 等）を毎回再評価していないか。
6. **【短絡評価（Any/All/First/Contains/Take）の早期 break 漏れ】**: 確定時点で即座に break / return しているか。
7. **【SIMD ベクトル化の機会損失】**: 連続 Span に対する Sum/Min/Max 等で SimdAggregator や Multi-Metric SIMD が使えるのにスカラーになっていないか。
8. **【実測値の逆転原因】**: 各ペアの実測値で FLinq が System.Linq や ZLinq より遅い、または割当が多い場合、どの処理がボトルネックか。

## 3. 回答フォーマット
改善余地があるペアについて、以下を出力してください：
- **対象ペア**: `#通番 (QueryId) - クエリ式`
- **該当カテゴリ**: （上記 1～8 のどれか）
- **現状の展開コードの問題点**: （どの行・構造がなぜ非効率か）
- **理想的な展開コード案**: （具体的な C# コードスニペット）
- **期待される効果**: （ループ削減、メモリ削減、SIMD化など）";
    }

    public void OpenInExplorer() {
        OpenInExplorer(this.outputDirectory);
    }

    public static void OpenInExplorer(string? path) {
        var targetPath = string.IsNullOrWhiteSpace(path) ? ResolveDefaultDumpRoot() : path;
        if (!Directory.Exists(targetPath)) {
            Directory.CreateDirectory(targetPath);
        }
        Process.Start(new ProcessStartInfo {
            FileName = "explorer.exe",
            Arguments = $"\"{targetPath}\"",
            UseShellExecute = true,
        });
    }

    public void Dispose() {
        lock (this.gate) {
            if (!this.disposed) {
                this.disposed = true;
                this.currentWriter?.Dispose();
                this.currentWriter = null;
            }
        }
    }
}
