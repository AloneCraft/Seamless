using System.Globalization;
using System.Text;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkImprovementPromptBuilder{
    public const string FileName = "ImprovementPrompt.md";

    public static BenchmarkRatioSummary CreateSummary(IReadOnlyList<BenchmarkComparisonRow> rows) =>
        new(
            CalculateGeometricMean(rows.Select(static row => row.PairRatio.TimeRatio),out var timeIncluded,out var timeExcluded),
            CalculateGeometricMean(rows.Select(static row => row.PairRatio.AllocatedRatio),out var allocatedIncluded,out var allocatedExcluded),
            timeIncluded,
            timeExcluded,
            allocatedIncluded,
            allocatedExcluded);

    public static string BuildPrompt(ProgressState state){
        var entries = CreateEntries(state).ToArray();
        if(entries.Length==0){
            return "ベンチマーク改善プロンプト\n\n完了済みの比較可能なベンチマーク結果がありません。\n";
        }

        var builder = new StringBuilder();
        builder.AppendLine("ベンチマーク改善プロンプト");
        builder.AppendLine();
        builder.AppendLine("目的: FLinq 側の時間比・メモリ比が大きいベンチマークを優先して改善してください。");
        builder.AppendLine("時間比とメモリ比は 左 / 右 です。1.00 より大きいほど左側実装の負荷が大きいです。");
        builder.AppendLine();
        builder.AppendLine("タブ別幾何平均");
        foreach(var group in entries.GroupBy(static item => item.SourceTab.Id).OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)){
            var sourceTab = group.First().SourceTab;
            var summary = CreateSummary(group.Select(static item => item.Row).ToArray());
            builder.AppendLine("- "+sourceTab.DisplayName+": "+summary.DisplayText);
        }

        builder.AppendLine();
        AppendRanking(builder,"時間比ランキング",entries.OrderByDescending(static item => GetRankValue(item.Row.PairRatio.TimeRatio)).ThenBy(static item => item.Row.Benchmark,StringComparer.OrdinalIgnoreCase));
        builder.AppendLine();
        AppendRanking(builder,"メモリ比ランキング",entries.OrderByDescending(static item => GetRankValue(item.Row.PairRatio.AllocatedRatio)).ThenBy(static item => item.Row.Benchmark,StringComparer.OrdinalIgnoreCase));
        builder.AppendLine();
        builder.AppendLine("改善依頼");
        builder.AppendLine("上位のベンチマークについて、FLinq 側の列挙処理、割り当て、分岐、バッファリング、特殊化漏れを確認し、右側実装との差が大きい順に改善案と実装方針を提案してください。");
        return builder.ToString();
    }

    private static IEnumerable<PromptEntry> CreateEntries(ProgressState state){
        foreach(var group in state.Jobs
            .Where(static job => job.Status==JobStatus.Completed)
            .GroupBy(static job => new { job.SourceTab,job.ElementCount })
            .OrderBy(static group => BenchmarkSourceTabs.Get(group.Key.SourceTab).Id,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.Key.ElementCount)){
            var sourceTab = BenchmarkSourceTabs.Get(group.Key.SourceTab);
            var reportRows = group.Select(static job => job.ToReportRow()).ToArray();
            if(reportRows.Length==0){
                continue;
            }

            IReadOnlyList<BenchmarkComparisonRow> rows;
            try{
                rows=BenchmarkComparisonFormatter.BuildComparisonRows(reportRows,sourceTab,group.Key.ElementCount);
            }catch(InvalidDataException){
                continue;
            }

            foreach(var row in rows){
                var left = group.LastOrDefault(job => string.Equals(job.MethodName,row.MethodName,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(job.Project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase));
                var right = group.LastOrDefault(job => string.Equals(job.MethodName,row.MethodName,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(job.Project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase));
                if(left is null||right is null){
                    continue;
                }

                yield return new PromptEntry(sourceTab,group.Key.ElementCount,row,left,right);
            }
        }
    }

    private static double? CalculateGeometricMean(IEnumerable<RatioValue> ratios,out int included,out int excluded){
        included=0;
        excluded=0;
        var logSum = 0.0;
        foreach(var ratio in ratios){
            if(ratio.NumericValue is { } value&&value>0&&double.IsFinite(value)){
                logSum+=Math.Log(value);
                included++;
            }else{
                excluded++;
            }
        }

        return included==0
            ? null
            : Math.Exp(logSum/included);
    }

    private static void AppendRanking(StringBuilder builder,string title,IEnumerable<PromptEntry> entries){
        builder.AppendLine(title);
        foreach(var entry in entries.Take(30)){
            builder.AppendLine("- "
                +entry.SourceTab.DisplayName
                +" / N="+entry.ElementCount.ToString(CultureInfo.InvariantCulture)
                +" / "+ShortName(entry.Row.Benchmark)
                +" / 時間比 "+FormatRatio(entry.Row.PairRatio.TimeRatio)
                +" / メモリ比 "+FormatRatio(entry.Row.PairRatio.AllocatedRatio)
                +" / 左時間 "+FormatTime(entry.Row.Left.TimeNanoseconds)
                +" / 右時間 "+FormatTime(entry.Row.Right.TimeNanoseconds)
                +" / 左メモリ "+FormatAllocated(entry.Row.Left.AllocatedBytes)
                +" / 右メモリ "+FormatAllocated(entry.Row.Right.AllocatedBytes)
                +" / Seed "+FirstText(entry.LeftJob.RandomSeed?.ToString(CultureInfo.InvariantCulture),entry.RightJob.RandomSeed?.ToString(CultureInfo.InvariantCulture),"-")
                +" / 結果 "+FirstText(entry.LeftJob.ResultCheckText,entry.RightJob.ResultCheckText,"-"));
        }
    }

    private static double GetRankValue(RatioValue ratio) =>
        ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => value,
            RatioValueKind.LeftOnly => double.PositiveInfinity,
            _ => double.NegativeInfinity,
        };

    private static string FormatRatio(RatioValue ratio) =>
        ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => value.ToString("0.00",CultureInfo.InvariantCulture),
            RatioValueKind.BothZero => "0",
            RatioValueKind.LeftOnly => "inf",
            RatioValueKind.RightOnly => "0",
            _ => "-",
        };

    private static string FormatTime(double? value) =>
        value is { } number&&double.IsFinite(number)
            ? FormatNanoseconds(number)
            : "-";

    private static string FormatAllocated(long? value) =>
        value is { } number
            ? FormatBytes(number)
            : "-";

    private static string ShortName(string name) {
        var index = name.LastIndexOf('.');
        return index>=0&&index<name.Length-1
            ? name[(index+1)..]
            : name;
    }

    private static string FormatNanoseconds(double value) =>
        value>=1_000_000
            ? (value/1_000_000).ToString("0.00",CultureInfo.InvariantCulture)+" ms"
            : value>=1_000
                ? (value/1_000).ToString("0.00",CultureInfo.InvariantCulture)+" us"
                : value.ToString("0.00",CultureInfo.InvariantCulture)+" ns";

    private static string FormatBytes(long value) =>
        value>=1024*1024
            ? ((double)value/(1024*1024)).ToString("0.00",CultureInfo.InvariantCulture)+" MB"
            : value>=1024
                ? ((double)value/1024).ToString("0.00",CultureInfo.InvariantCulture)+" KB"
                : value.ToString(CultureInfo.InvariantCulture)+" B";

    private static string FirstText(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;

    private sealed record PromptEntry(
        BenchmarkSourceTabDefinition SourceTab,
        int ElementCount,
        BenchmarkComparisonRow Row,
        BenchmarkJob LeftJob,
        BenchmarkJob RightJob);
}

internal sealed record BenchmarkRatioSummary(
    double? TimeGeometricMean,
    double? AllocatedGeometricMean,
    int TimeIncludedCount,
    int TimeExcludedCount,
    int AllocatedIncludedCount,
    int AllocatedExcludedCount){
    public string DisplayText =>
        "時間比 "+FormatRatio(this.TimeGeometricMean)
        +" / メモリ比 "+FormatRatio(this.AllocatedGeometricMean)
        +" / 対象 "+Math.Max(this.TimeIncludedCount,this.AllocatedIncludedCount).ToString(CultureInfo.InvariantCulture)
        +" / 除外 "+Math.Max(this.TimeExcludedCount,this.AllocatedExcludedCount).ToString(CultureInfo.InvariantCulture);

    private static string FormatRatio(double? value) =>
        value is { } number&&double.IsFinite(number)
            ? number.ToString("0.00",CultureInfo.InvariantCulture)
            : "-";
}
