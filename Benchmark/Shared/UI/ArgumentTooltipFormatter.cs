using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
internal static class ArgumentTooltipFormatter {
    public static string CreateArgumentTooltip(TableRow row) {
        var items = BenchmarkArgumentColumns.Get(row.SourceTab)
            .Select(column => (column.ToString(),GetArgumentValue(row,column)))
            .ToArray();
        var summary = items
            .Where(static item => item.Item1 is "Count" or "Memory" or "Initial")
            .ToArray();
        var summaryText = string.Join(
            " / ",
            summary
                .Where(static item => !string.IsNullOrWhiteSpace(item.Item2))
                .Select(static item => item.Item1+"="+item.Item2));
        var detailText = string.Join(
            " / ",
            items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Item2))
                .Select(static item => item.Item1+"="+item.Item2));
        if(string.IsNullOrWhiteSpace(summaryText)) {
            return detailText;
        }

        return string.IsNullOrWhiteSpace(detailText)
            ? summaryText
            : summaryText+"\n"+detailText;
    }

    private static string GetArgumentValue(TableRow row,ScalableBenchmarkArgumentColumn column) =>
        column switch{
            ScalableBenchmarkArgumentColumn.Count => row.CountArgument,
            ScalableBenchmarkArgumentColumn.Index => row.IndexArgument,
            ScalableBenchmarkArgumentColumn.Page => row.PageArgument,
            ScalableBenchmarkArgumentColumn.Segment => row.SegmentArgument,
            ScalableBenchmarkArgumentColumn.Memory => row.MemoryArgument,
            ScalableBenchmarkArgumentColumn.Fraction => row.FractionArgument,
            ScalableBenchmarkArgumentColumn.Initial => row.InitialArgument,
            ScalableBenchmarkArgumentColumn.Data => row.DataArgument,
            _ => string.Empty,
        };
}
