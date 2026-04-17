using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;

namespace BenchmarkExe.Benchmarks;

public sealed class BenchmarkColumnsConfig : ManualConfig
{
    public BenchmarkColumnsConfig()
    {
        AddColumnProvider(new PreferredFirstColumnProvider());
    }

    private sealed class PreferredFirstColumnProvider : IColumnProvider
    {
        private static readonly string[] Preferred = ["Method", "Mean", "Allocated", "Gen0", "Gen1", "Gen2"];

        public IEnumerable<IColumn> GetColumns(Summary summary)
        {
            var columns = DefaultColumnProviders.Instance
                .SelectMany(p => p.GetColumns(summary))
                .ToList();
            var preferred = new List<IColumn>();

            foreach (var name in Preferred)
            {
                preferred.AddRange(columns.Where(c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase)));
            }

            var rest = columns.Where(c => !preferred.Contains(c)).ToList();
            return preferred.Concat(rest);
        }
    }
}
