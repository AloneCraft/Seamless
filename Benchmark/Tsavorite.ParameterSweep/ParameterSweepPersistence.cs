using System.Globalization;
using System.Text;

namespace Tsavorite.ParameterSweep;

public sealed record ParameterSweepRunSummary(string RunId,string RunRoot,DateTimeOffset CreatedAt,int ResultCount);

public sealed class ParameterSweepRunStore(string rootDirectory) {
    private const string ResultsFileName = "results.csv";

    public string RootDirectory { get; } = rootDirectory;

    public ParameterSweepRunSummary Save(IReadOnlyList<ParameterSweepResult> results) {
        ArgumentNullException.ThrowIfNull(results);
        Directory.CreateDirectory(this.RootDirectory);
        var runId = results.FirstOrDefault()?.RunId
            ??DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff",CultureInfo.InvariantCulture);
        var runRoot = Path.Combine(this.RootDirectory,runId);
        Directory.CreateDirectory(runRoot);
        var path = Path.Combine(runRoot,ResultsFileName);
        File.WriteAllText(path,ParameterSweepCsv.Write(results),new UTF8Encoding(true));
        return new(runId,runRoot,DateTimeOffset.UtcNow,results.Count);
    }

    public IReadOnlyList<ParameterSweepRunSummary> ListRuns() {
        if(!Directory.Exists(this.RootDirectory)) {
            return [];
        }
        return Directory.EnumerateDirectories(this.RootDirectory)
            .Select(directory => {
                var path = Path.Combine(directory,ResultsFileName);
                var count = File.Exists(path)
                    ? Math.Max(0,File.ReadLines(path).Count()-1)
                    : 0;
                return new ParameterSweepRunSummary(
                    Path.GetFileName(directory),
                    directory,
                    Directory.GetCreationTimeUtc(directory),
                    count);
            })
            .OrderByDescending(static item => item.CreatedAt)
            .ToArray();
    }

    public IReadOnlyList<ParameterSweepResult> Load(ParameterSweepRunSummary summary) =>
        this.Load(summary.RunRoot);

    public IReadOnlyList<ParameterSweepResult> Load(string runRoot) {
        var path = Path.Combine(runRoot,ResultsFileName);
        return ParameterSweepCsv.Read(File.ReadAllText(path,Encoding.UTF8));
    }
}

public static class ParameterSweepComparer {
    public static IReadOnlyList<ParameterSweepComparisonRow> Compare(
        IReadOnlyList<ParameterSweepResult> current,
        IReadOnlyList<ParameterSweepResult> baseline) {
        var baselineByCase = baseline.ToDictionary(static item => item.CaseName,StringComparer.OrdinalIgnoreCase);
        return current
            .Select(item => CreateRow(item,baselineByCase.GetValueOrDefault(item.CaseName)))
            .ToArray();
    }

    private static ParameterSweepComparisonRow CreateRow(ParameterSweepResult current,ParameterSweepResult? baseline) {
        if(baseline is null) {
            return new(current,null,null,null,null,false);
        }
        return new(
            current,
            baseline,
            current.MaxSuccessfulCount-baseline.MaxSuccessfulCount,
            CreateRatio(current.AddElapsedMilliseconds,baseline.AddElapsedMilliseconds),
            current.LookupMeanNanoseconds is { } currentLookup&&baseline.LookupMeanNanoseconds is { } baselineLookup
                ? CreateRatio(currentLookup,baselineLookup)
                : null,
            current.FailureKind!=baseline.FailureKind);
    }

    private static double? CreateRatio(double current,double baseline) =>
        baseline>0 ? current/baseline : null;
}

public static class ParameterSweepCsv {
    private static readonly string[] Headers = [
        "RunId",
        "MeasuredAt",
        "CaseName",
        "Collection",
        "Generic",
        "Backend",
        "Sweep",
        "IndexSize",
        "PageSize",
        "SegmentSize",
        "MemorySize",
        "MutableFraction",
        "MaxSuccessfulCount",
        "EntryCount",
        "FailureKind",
        "FailureMessage",
        "AddElapsedMilliseconds",
        "LookupMeanNanoseconds",
    ];

    public static string Write(IReadOnlyList<ParameterSweepResult> results) {
        var builder = new StringBuilder();
        AppendRow(builder,Headers);
        foreach(var item in results) {
            AppendRow(builder,[
                item.RunId,
                item.MeasuredAt.ToString("O",CultureInfo.InvariantCulture),
                item.CaseName,
                item.Collection.ToString(),
                item.GenericName,
                item.Backend.ToString(),
                item.Sweep.ToString(),
                item.IndexSize.ToString(CultureInfo.InvariantCulture),
                item.PageSize.ToString(CultureInfo.InvariantCulture),
                item.SegmentSize.ToString(CultureInfo.InvariantCulture),
                item.MemorySize.ToString(CultureInfo.InvariantCulture),
                item.MutableFraction.ToString("R",CultureInfo.InvariantCulture),
                item.MaxSuccessfulCount.ToString(CultureInfo.InvariantCulture),
                item.EntryCount.ToString(CultureInfo.InvariantCulture),
                item.FailureKind.ToString(),
                item.FailureMessage,
                item.AddElapsedMilliseconds.ToString("R",CultureInfo.InvariantCulture),
                item.LookupMeanNanoseconds?.ToString("R",CultureInfo.InvariantCulture)??string.Empty,
            ]);
        }
        return builder.ToString();
    }

    public static IReadOnlyList<ParameterSweepResult> Read(string csv) {
        var rows = Parse(csv);
        if(rows.Count<=1) {
            return [];
        }
        return rows.Skip(1)
            .Where(static row => row.Count>=Headers.Length)
            .Select(CreateResult)
            .ToArray();
    }

    private static ParameterSweepResult CreateResult(IReadOnlyList<string> row) =>
        new(
            row[0],
            DateTimeOffset.Parse(row[1],CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),
            row[2],
            Enum.Parse<ParameterSweepCollectionKind>(row[3]),
            row[4],
            Enum.Parse<ParameterSweepBackend>(row[5]),
            Enum.Parse<ParameterSweepAxis>(row[6]),
            long.Parse(row[7],CultureInfo.InvariantCulture),
            long.Parse(row[8],CultureInfo.InvariantCulture),
            long.Parse(row[9],CultureInfo.InvariantCulture),
            long.Parse(row[10],CultureInfo.InvariantCulture),
            double.Parse(row[11],CultureInfo.InvariantCulture),
            long.Parse(row[12],CultureInfo.InvariantCulture),
            long.Parse(row[13],CultureInfo.InvariantCulture),
            Enum.Parse<ParameterSweepFailureKind>(row[14]),
            row[15],
            double.Parse(row[16],CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(row[17])?null:double.Parse(row[17],CultureInfo.InvariantCulture));

    private static void AppendRow(StringBuilder builder,IReadOnlyList<string> values) {
        for(var index = 0;index<values.Count;index++) {
            if(index>0) {
                builder.Append(',');
            }
            AppendField(builder,values[index]);
        }
        builder.AppendLine();
    }

    private static void AppendField(StringBuilder builder,string value) {
        if(value.Contains(',')||value.Contains('"')||value.Contains('\n')||value.Contains('\r')) {
            builder.Append('"');
            builder.Append(value.Replace("\"","\"\"",StringComparison.Ordinal));
            builder.Append('"');
        }
        else {
            builder.Append(value);
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> Parse(string csv) {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuote = false;
        for(var index = 0;index<csv.Length;index++) {
            var ch = csv[index];
            if(inQuote) {
                if(ch=='"') {
                    if(index+1<csv.Length&&csv[index+1]=='"') {
                        field.Append('"');
                        index++;
                    }
                    else {
                        inQuote=false;
                    }
                }
                else {
                    field.Append(ch);
                }
                continue;
            }
            if(ch=='"') {
                inQuote=true;
            }
            else if(ch==',') {
                row.Add(field.ToString());
                field.Clear();
            }
            else if(ch=='\n') {
                row.Add(field.ToString().TrimEnd('\r'));
                field.Clear();
                rows.Add(row.ToArray());
                row.Clear();
            }
            else {
                field.Append(ch);
            }
        }
        if(field.Length>0||row.Count>0) {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }
}
