using System.Globalization;

namespace UI;

public sealed record BenchmarkRankedRatioPoint(string Label,double Value,string DetailText);
public sealed record BenchmarkCategoryRatioPoint(string Category,double AvgMeanRatio,double AvgAllocationRatio,int SampleCount);
public sealed record BenchmarkTrendPoint(double X,double Y);
public sealed record BenchmarkTrendSeries(string SeriesLabel,IReadOnlyList<BenchmarkTrendPoint> Points);

public enum BenchmarkBadgeSeverity{
    Ok,
    Warning,
    Critical,
    Info,
}

public sealed record BenchmarkBadgeItem(string Title,string Detail,BenchmarkBadgeSeverity Severity);
public sealed record BenchmarkQualityGaugePoint(string Label,double StdDevRatio,double ErrorRatio,string DetailText);

/// <summary>
/// SQLite 分析 View の汎用行(IReadOnlyDictionary)を、インフォグラフィック描画向けの型付きモデルへ変換する。
/// MAUI 依存を持たない純ロジック。列名は UiRunStore.cs の CREATE VIEW 定義に合わせる。
/// </summary>
[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkAnalysisViewMapper{
    public static IReadOnlyList<BenchmarkRankedRatioPoint> MapImprovementCandidates(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows) =>
        rows.Select(row => new BenchmarkRankedRatioPoint(
                GetText(row,"Method"),
                GetDouble(row,"ImprovementScore"),
                GetText(row,"LeftProject")+" / "+GetText(row,"RightProject")+" / N="+GetText(row,"ElementCount")))
            .OrderByDescending(static p => p.Value)
            .ToArray();

    public static IReadOnlyList<BenchmarkRankedRatioPoint> 時間シェアワーストを変換(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows) =>
        rows.Select(row => new BenchmarkRankedRatioPoint(
                GetText(row,"ベンチマーク"),
                GetDouble(row,"左シェア"),
                "サンプル "+GetText(row,"サンプル数")))
            .OrderByDescending(static p => p.Value)
            .ToArray();

    public static IReadOnlyList<BenchmarkRankedRatioPoint> メモリシェアワーストを変換(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows) =>
        rows.Select(row => new BenchmarkRankedRatioPoint(
                GetText(row,"ベンチマーク"),
                GetDouble(row,"左シェア"),
                "サンプル "+GetText(row,"サンプル数")))
            .OrderByDescending(static p => p.Value)
            .ToArray();

    public static IReadOnlyList<BenchmarkCategoryRatioPoint> MapCategorySummary(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows) =>
        rows.Select(row => new BenchmarkCategoryRatioPoint(
                GetText(row,"Category"),
                GetDouble(row,"AvgMeanRatio"),
                GetDouble(row,"AvgAllocationRatio"),
                (int)GetDouble(row,"SampleCount")))
            .OrderByDescending(static p => p.AvgMeanRatio)
            .ToArray();

    public static IReadOnlyList<BenchmarkTrendSeries> MapHistoryTrend(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows,int maxSeries = 8) =>
        rows.GroupBy(row => GetText(row,"Method"),StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(maxSeries)
            .Select(g => new BenchmarkTrendSeries(
                g.Key,
                g.OrderBy(row => GetTicks(row,"LeftFinishedAt"))
                    .Select((row,index) => new BenchmarkTrendPoint(index,GetDouble(row,"MeanRatio")))
                    .ToArray()))
            .ToArray();

    public static IReadOnlyList<BenchmarkTrendSeries> 時間シェア履歴を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows,int maxSeries = int.MaxValue) =>
        シェア履歴を変換(rows,maxSeries);

    public static IReadOnlyList<BenchmarkTrendSeries> メモリシェア履歴を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows,int maxSeries = int.MaxValue) =>
        シェア履歴を変換(rows,maxSeries);

    private static IReadOnlyList<BenchmarkTrendSeries> シェア履歴を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows,int maxSeries) =>
        rows.GroupBy(row => GetText(row,"タブ名")+" / "+GetText(row,"型")+" / "+GetText(row,"ベンチマーク"),StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Max(row => GetDouble(row,"左シェア")))
            .Take(maxSeries)
            .SelectMany(g => new[]{
                new BenchmarkTrendSeries(
                    g.Key+" / Left",
                    g.OrderBy(row => GetDouble(row,"実行ID"))
                        .Select(row => new BenchmarkTrendPoint(GetDouble(row,"実行ID"),GetDouble(row,"左シェア")))
                        .ToArray()),
                new BenchmarkTrendSeries(
                    g.Key+" / Right",
                    g.OrderBy(row => GetDouble(row,"実行ID"))
                        .Select(row => new BenchmarkTrendPoint(GetDouble(row,"実行ID"),GetDouble(row,"右シェア")))
                        .ToArray()),
            })
            .ToArray();

    public static IReadOnlyList<BenchmarkTrendSeries> MapScaleTrend(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows,int maxSeries = 8) =>
        rows.GroupBy(row => GetText(row,"Method"),StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Max(row => GetDouble(row,"AvgMeanRatio")))
            .Take(maxSeries)
            .Select(g => new BenchmarkTrendSeries(
                g.Key,
                g.OrderBy(row => GetDouble(row,"ElementCount"))
                    .Select(row => new BenchmarkTrendPoint(GetDouble(row,"ElementCount"),GetDouble(row,"AvgMeanRatio")))
                    .ToArray()))
            .ToArray();

    public static IReadOnlyList<BenchmarkBadgeItem> MapMissingFailureMatrix(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows) =>
        rows.Select(row => {
                var failed = string.Equals(GetText(row,"JobStatus"),"Failed",StringComparison.OrdinalIgnoreCase);
                var missing = GetDouble(row,"OppositeMissing")>0;
                var severity = failed ? BenchmarkBadgeSeverity.Critical : missing ? BenchmarkBadgeSeverity.Warning : BenchmarkBadgeSeverity.Info;
                var detail = GetText(row,"Project")+" / "+GetText(row,"Side")+" / N="+GetText(row,"ElementCount")
                    +(failed ? " / "+GetText(row,"FailureKind") : missing ? " / 対向側が欠測" : "");
                return new BenchmarkBadgeItem(GetText(row,"Method"),detail,severity);
            })
            .ToArray();

    public static IReadOnlyList<BenchmarkQualityGaugePoint> MapMeasurementQuality(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows) =>
        rows.Select(row => new BenchmarkQualityGaugePoint(
                GetText(row,"Method")+" ("+GetText(row,"Project")+")",
                Math.Clamp(GetDouble(row,"StdDevRatio"),0,1),
                Math.Clamp(GetDouble(row,"ErrorRatio"),0,1),
                "N="+GetText(row,"ElementCount")+" / CPU "+GetText(row,"AssignedLogicalCpu")))
            .ToArray();

    public static IReadOnlyList<BenchmarkBadgeItem> MapFallbackDiagnostics(IReadOnlyList<IReadOnlyDictionary<string,object?>> rows) =>
        rows.Select(row => new BenchmarkBadgeItem(
                GetText(row,"Method")+" / "+GetText(row,"CounterName"),
                GetText(row,"Project")+" / 値 "+GetText(row,"CounterValue"),
                GetDouble(row,"CounterValue")>0 ? BenchmarkBadgeSeverity.Warning : BenchmarkBadgeSeverity.Ok))
            .ToArray();

    private static string GetText(IReadOnlyDictionary<string,object?> row,string column) =>
        row.TryGetValue(column,out var value)&&value is not null ? Convert.ToString(value,CultureInfo.InvariantCulture)??"-" : "-";

    private static double GetDouble(IReadOnlyDictionary<string,object?> row,string column) =>
        row.TryGetValue(column,out var value)&&value is not null&&value is not DBNull
            ? Convert.ToDouble(value,CultureInfo.InvariantCulture)
            : 0;

    private static long GetTicks(IReadOnlyDictionary<string,object?> row,string column) =>
        row.TryGetValue(column,out var value)&&value is string text&&DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out var dto)
            ? dto.Ticks
            : 0;
}
