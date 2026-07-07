using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class ComparisonService:IComparisonService {
    private readonly IResultService resultService;

    public ComparisonService(IResultService resultService) {
        this.resultService=resultService;
    }

    public IReadOnlyList<BenchmarkComparisonRow> BuildRows(IEnumerable<BenchmarkResultItem> results) {
        var reportRows = results.Select(this.resultService.ToReportRow).ToArray();
        return BenchmarkComparisonFormatter.BuildComparisonRows(reportRows);
    }

    public IReadOnlyList<ChartComparisonPoint> BuildChartPoints(
        IReadOnlyList<BenchmarkComparisonRow> rows,
        ComparisonPairOption pair,
        MetricKind metric) {
        return rows
            .Select(row => CreateChartPoint(row,pair,metric))
            .Where(static point => point is not null)
            .Select(static point => point!)
            .OrderByDescending(static point => point.Ratio??(point.LeftValue>0&&point.RightValue==0?double.PositiveInfinity:0))
            .ThenByDescending(static point => Math.Abs(point.LeftValue-point.RightValue))
            .ThenBy(static point => point.Benchmark,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ChartComparisonPoint? CreateChartPoint(BenchmarkComparisonRow row,ComparisonPairOption pair,MetricKind metric) {
        var left = row.GetProject(pair.LeftProject);
        var right = row.GetProject(pair.RightProject);
        if(!left.HasMeasuredValue||!right.HasMeasuredValue) {
            return null;
        }

        var leftValue = GetMetricValue(left,metric);
        var rightValue = GetMetricValue(right,metric);
        if(leftValue is null||rightValue is null) {
            return null;
        }

        var leftNumber = Math.Max(0,leftValue??0);
        var rightNumber = Math.Max(0,rightValue??0);
        var shareTexts = BenchmarkChartSelectionLayoutLogic.CreateShareTexts(leftNumber,rightNumber);
        double? ratio = BenchmarkChartSelectionLayoutLogic.CreateRatio(leftNumber,rightNumber);
        var ratioText = ratio is { } ratioValue
            ? ratioValue.ToString("0.00",CultureInfo.InvariantCulture)
            : "-";
        var metricName = metric==MetricKind.Time?"Time":"Allocated";
        var leftText = shareTexts.LeftText;
        var rightText = shareTexts.RightText;
        var tooltip=$"{row.Benchmark}\n{pair.LeftProject} {metricName}: {FirstText(FormatMetric(leftValue,metric),"-")} / Share {leftText}\n{pair.RightProject} {metricName}: {FirstText(FormatMetric(rightValue,metric),"-")} / Share {rightText}\nRatio(left/right): {ratioText}";
        return new ChartComparisonPoint(
            row.Benchmark,
            BenchmarkFormatting.ShortName(row.Benchmark),
            leftNumber,
            rightNumber,
            leftText,
            rightText,
            tooltip,
            ratio);
    }

    private static double? GetMetricValue(ProjectMetrics metrics,MetricKind metric) {
        if(!metrics.HasMeasuredValue) {
            return null;
        }

        return metric==MetricKind.Time
            ? metrics.TimeNanoseconds
            : metrics.AllocatedBytes;
    }

    private static string FormatMetric(double? value,MetricKind metric) {
        if(value is null) {
            return string.Empty;
        }

        return metric==MetricKind.Time
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : BenchmarkFormatting.FormatBytes((long)Math.Round(value.Value,MidpointRounding.AwayFromZero));
    }

    private static string FirstText(params string[] values) {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;
    }
}
