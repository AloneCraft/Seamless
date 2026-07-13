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
}
