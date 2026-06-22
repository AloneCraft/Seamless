using global::System.Globalization;
using global::System.Text;

namespace Benchmark.Runner.Uno;

internal static class BenchmarkReportFormatter{
    public static string BuildCsv(IReadOnlyList<ReportRow> rows){
        var writer = new StringBuilder();
        writer.AppendLine("Project,Method,Status,Mean,Error,StdDev,LogicalCpu,Core,CpuDisplay,ArtifactFolder,ExitCode");
        foreach(var row in rows){
            writer.AppendCsv(row.Project).Append(',');
            writer.AppendCsv(row.Method).Append(',');
            writer.AppendCsv(row.Status).Append(',');
            writer.AppendCsv(row.Mean).Append(',');
            writer.AppendCsv(row.Error).Append(',');
            writer.AppendCsv(row.StdDev).Append(',');
            writer.AppendCsv(row.LogicalCpu).Append(',');
            writer.AppendCsv(row.Core).Append(',');
            writer.AppendCsv(row.CpuDisplay).Append(',');
            writer.AppendCsv(row.ArtifactFolder).Append(',');
            writer.AppendCsv(row.ExitCode?.ToString(CultureInfo.InvariantCulture)??string.Empty).AppendLine();
        }

        return writer.ToString();
    }

    public static string BuildHtml(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var comparisonRows = CreateComparisonRows(rows);
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"en\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Comparison</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:#202020;background:#fffefa;}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}");
        writer.AppendLine("    p{margin:0 0 16px;color:#555;}");
        writer.AppendLine("    table{border-collapse:collapse;width:100%;font-size:13px;}");
        writer.AppendLine("    th,td{border:1px solid #d5d1c4;padding:5px 7px;text-align:left;white-space:nowrap;}");
        writer.AppendLine("    th{background:#f0ede4;font-weight:600;}");
        writer.AppendLine("    td.num{text-align:right;}");
        writer.AppendLine("    tbody tr:nth-child(even) td{background:#faf8f0;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Comparison</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(generatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <table>");
        writer.AppendLine("    <thead><tr><th>Method</th><th>linq</th><th>zlinq</th><th>flinq</th></tr></thead>");
        writer.AppendLine("    <tbody>");
        foreach(var row in comparisonRows){
            writer.Append("      <tr><td>").Append(EscapeHtml(row.Method)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.Linq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.ZLinq.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.FLinq.Mean)).AppendLine("</td></tr>");
        }
        writer.AppendLine("    </tbody>");
        writer.AppendLine("  </table>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    public static string BuildChartHtml(IReadOnlyList<ReportRow> rows,DateTimeOffset generatedAt){
        var comparisonRows = CreateComparisonRows(rows);
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"en\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Comparison Chart</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:#202020;background:#fffefa;}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}");
        writer.AppendLine("    p{margin:0 0 16px;color:#555;}");
        writer.AppendLine("    .legend{display:flex;gap:18px;margin:0 0 14px;font-size:13px;}");
        writer.AppendLine("    .legend span{display:inline-flex;align-items:center;gap:6px;}");
        writer.AppendLine("    .swatch{width:12px;height:12px;border-radius:2px;display:inline-block;}");
        writer.AppendLine("    .linq{background:#3a6ea5;}");
        writer.AppendLine("    .zlinq{background:#5b8c5a;}");
        writer.AppendLine("    .flinq{background:#c06c3e;}");
        writer.AppendLine("    .chart{display:grid;grid-template-columns:minmax(180px,28%) 1fr;gap:6px 12px;font-size:13px;}");
        writer.AppendLine("    .method{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;padding:5px 0;}");
        writer.AppendLine("    .bar{height:24px;border:1px solid #d5d1c4;background:#f8f6ed;display:flex;overflow:hidden;}");
        writer.AppendLine("    .segment{height:100%;display:flex;align-items:center;justify-content:center;color:#fff;font-size:11px;min-width:0;}");
        writer.AppendLine("    .segment span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;padding:0 4px;}");
        writer.AppendLine("    .axis-label{font-size:11px;color:#555;}");
        writer.AppendLine("    .axis{grid-column:2;display:grid;grid-template-columns:repeat(5,1fr);margin-top:4px;color:#555;font-size:11px;}");
        writer.AppendLine("    .axis span:nth-child(n+2){text-align:center;}");
        writer.AppendLine("    .axis span:last-child{text-align:right;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Comparison Chart</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(generatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <div class=\"legend\"><span><i class=\"swatch linq\"></i>linq</span><span><i class=\"swatch zlinq\"></i>zlinq</span><span><i class=\"swatch flinq\"></i>flinq</span></div>");
        writer.AppendLine("  <div class=\"chart\">");
        writer.AppendLine("    <div class=\"axis-label\"></div><div class=\"axis-label\">ratio</div>");
        foreach(var row in comparisonRows){
            var linqRatio = GetRatio(row.Linq,row);
            var zlinqRatio = GetRatio(row.ZLinq,row);
            var flinqRatio = GetRatio(row.FLinq,row);
            writer.Append("    <div class=\"method\" title=\"").Append(EscapeHtml(row.Method)).Append("\">").Append(EscapeHtml(row.Method)).AppendLine("</div>");
            writer.AppendLine("    <div class=\"bar\">");
            AppendSegment(writer,"linq","linq",row.Linq,linqRatio);
            AppendSegment(writer,"zlinq","zlinq",row.ZLinq,zlinqRatio);
            AppendSegment(writer,"flinq","flinq",row.FLinq,flinqRatio);
            writer.AppendLine("    </div>");
        }
        writer.AppendLine("    <div></div><div class=\"axis\"><span>0</span><span>0.25</span><span>0.5</span><span>0.75</span><span>1</span></div>");
        writer.AppendLine("  </div>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    private static void AppendSegment(StringBuilder writer,string cssClass,string label,ComparisonValue value,double ratio){
        var width = ratio*100;
        var percentText = width.ToString("0.0",CultureInfo.InvariantCulture)+"%";
        writer.Append("      <div class=\"segment ").Append(cssClass).Append("\" style=\"width:");
        writer.Append(width.ToString("0.###",CultureInfo.InvariantCulture));
        writer.Append("%\" title=\"").Append(EscapeHtml(label)).Append(": ");
        writer.Append(EscapeHtml(value.Mean));
        writer.Append(" (").Append(percentText).AppendLine(")\">");
        if(width>=8){
            writer.Append("        <span>").Append(EscapeHtml(percentText)).AppendLine("</span>");
        }
        writer.AppendLine("      </div>");
    }

    private static double GetRatio(ComparisonValue value,ComparisonRow row){
        var total = GetPositiveValue(row.Linq)+GetPositiveValue(row.ZLinq)+GetPositiveValue(row.FLinq);
        return total>0
            ? GetPositiveValue(value)/total
            : 0;
    }

    private static double GetPositiveValue(ComparisonValue value){
        return value.NumericMean is { } number&&number>0
            ? number
            : 0;
    }

    private static IReadOnlyList<ComparisonRow> CreateComparisonRows(IReadOnlyList<ReportRow> rows){
        var valuesByMethod = new Dictionary<string,ComparisonValue[]>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows){
            var series = GetReportSeries(row.Project);
            if(series is null){
                continue;
            }

            if(!valuesByMethod.TryGetValue(row.Method,out var values)){
                values = [ComparisonValue.Empty,ComparisonValue.Empty,ComparisonValue.Empty];
                valuesByMethod.Add(row.Method,values);
            }

            values[(int)series.Value]=CreateComparisonValue(row);
        }

        return valuesByMethod
            .OrderBy(static pair => pair.Key,StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new ComparisonRow(pair.Key,pair.Value[0],pair.Value[1],pair.Value[2]))
            .ToArray();
    }

    private static ComparisonValue CreateComparisonValue(ReportRow row){
        if(!string.Equals(row.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
            return ComparisonValue.Empty;
        }

        var numericMean = ParseMean(row.Mean);
        return numericMean is null
            ? ComparisonValue.Empty
            : new ComparisonValue(row.Mean,numericMean);
    }

    private static double? ParseMean(string value){
        var text = value.Trim();
        if(text.EndsWith("ns",StringComparison.OrdinalIgnoreCase)){
            text=text[..^2].Trim();
        }

        return double.TryParse(
            text,
            NumberStyles.AllowThousands|NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number)
            &&number>=0
            ? number
            : null;
    }

    private static ReportSeries? GetReportSeries(string project){
        return project switch{
            var value when string.Equals(value,"System LINQ",StringComparison.OrdinalIgnoreCase) => ReportSeries.Linq,
            var value when string.Equals(value,"System ZLinq",StringComparison.OrdinalIgnoreCase) => ReportSeries.ZLinq,
            var value when string.Equals(value,"System FLinq",StringComparison.OrdinalIgnoreCase) => ReportSeries.FLinq,
            _ => null,
        };
    }

    private static string EscapeHtml(string value){
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
    }

    private enum ReportSeries{
        Linq,
        ZLinq,
        FLinq,
    }

    private sealed record ComparisonRow(
        string Method,
        ComparisonValue Linq,
        ComparisonValue ZLinq,
        ComparisonValue FLinq);

    private sealed record ComparisonValue(string Mean,double? NumericMean){
        public static readonly ComparisonValue Empty = new(string.Empty,null);
    }
}
