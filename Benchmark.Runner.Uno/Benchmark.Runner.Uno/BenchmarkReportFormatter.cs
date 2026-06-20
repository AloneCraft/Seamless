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
        writer.AppendLine("    <thead><tr><th>Project</th><th>Method</th><th>Status</th><th>Mean</th><th>Error</th><th>StdDev</th><th>Logical CPU</th><th>Core</th><th>CPU Display</th><th>Artifact</th><th>Exit</th></tr></thead>");
        writer.AppendLine("    <tbody>");
        foreach(var row in rows){
            writer.Append("      <tr><td>").Append(EscapeHtml(row.Project)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Method)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Status)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.Error)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.StdDev)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.LogicalCpu)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Core)).Append("</td><td>");
            writer.Append(EscapeHtml(row.CpuDisplay)).Append("</td><td>");
            writer.Append(EscapeHtml(row.ArtifactFolder)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.ExitCode?.ToString(CultureInfo.InvariantCulture)??string.Empty)).AppendLine("</td></tr>");
        }
        writer.AppendLine("    </tbody>");
        writer.AppendLine("  </table>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }

    private static string EscapeHtml(string value){
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
    }
}
