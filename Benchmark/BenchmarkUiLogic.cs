namespace Benchmark;

public enum BenchmarkProjectLiveState{
    Idle,
    Running,
    Completed,
}

internal static class BenchmarkTimestampDisplay{
    public const string Format = "yyyy/MM/dd HH:mm:ss";

    public static string FormatLocal(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString(Format,System.Globalization.CultureInfo.InvariantCulture);
}

internal static class BenchmarkCsvTimestampParser{
    public static DateTimeOffset? TryExtractTimestamp(string path){
        var fileTimestamp = TryParseTimestamp(System.IO.Path.GetFileNameWithoutExtension(path));
        if(fileTimestamp is not null){
            return fileTimestamp;
        }

        var directory = System.IO.Path.GetDirectoryName(path);
        while(!string.IsNullOrWhiteSpace(directory)){
            var folderTimestamp = TryParseTimestamp(System.IO.Path.GetFileName(directory));
            if(folderTimestamp is not null){
                return folderTimestamp;
            }

            directory=System.IO.Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static DateTimeOffset? TryParseTimestamp(string text){
        foreach(var pattern in TimestampPatterns()){
            var match = System.Text.RegularExpressions.Regex.Match(text,pattern.Regex,System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if(!match.Success){
                continue;
            }

            var value = match.Groups["value"].Value;
            if(DateTime.TryParseExact(value,pattern.Format,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeLocal,out var date)){
                return new DateTimeOffset(date);
            }
        }

        return null;
    }

    private static IEnumerable<(string Regex,string Format)> TimestampPatterns(){
        yield return (@"(?<value>\d{8}-\d{6})","yyyyMMdd-HHmmss");
        yield return (@"(?<value>\d{14})","yyyyMMddHHmmss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}[-_ ]\d{2}[-_]\d{2}[-_]\d{2})","yyyy-MM-dd HH-mm-ss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})","yyyy-MM-dd_HH-mm-ss");
        yield return (@"(?<value>\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2})","yyyy-MM-dd-HH-mm-ss");
    }
}

internal static class BenchmarkProjectLiveStateLogic{
    public static bool IsPending(BenchmarkProjectLiveState state,bool hasMetrics) =>
        state==BenchmarkProjectLiveState.Idle&&!hasMetrics;

    public static bool IsRunning(BenchmarkProjectLiveState state) =>
        state==BenchmarkProjectLiveState.Running;

    public static bool IsCompleted(BenchmarkProjectLiveState state) =>
        state==BenchmarkProjectLiveState.Completed;
}

internal static class BenchmarkElementCountLogic{
    public static IReadOnlyList<int> BuildAdaptiveCandidates(IReadOnlyList<int> elementCounts,int maxElementCount){
        var candidates = elementCounts
            .Where(count => count<=maxElementCount)
            .Distinct()
            .Order()
            .ToArray();
        return candidates.Length==0 ? [BenchmarkElementCounts.DefaultInitial[0]] : candidates;
    }
}

internal sealed record CpuTableSectionDefinition(string Title,CpuCoreKind CoreKind,IReadOnlyList<CpuTableRowDefinition> Rows){
    public int ColumnCount => Rows.Count==0 ? 0 : Rows.Max(static row => row.LogicalProcessorIndexes.Count);
}

internal sealed record CpuTableRowDefinition(string Label,IReadOnlyList<int> LogicalProcessorIndexes);

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal static class CpuSelectionTableLogic{
    public static IReadOnlyList<CpuTableSectionDefinition> BuildSections(IReadOnlyList<CpuCoreInfo> cpuCores){
        return [
            ..BuildSection("P core",CpuCoreKind.Performance,cpuCores),
            ..BuildSection("E core",CpuCoreKind.Efficiency,cpuCores),
            ..BuildSection("Unknown core",CpuCoreKind.Unknown,cpuCores),
        ];
    }

    public static void ApplyBulkSelection(IReadOnlyList<CpuCoreInfo> cpuCores,IEnumerable<int> targetLogicalProcessorIndexes,bool selected){
        var targets = targetLogicalProcessorIndexes
            .Distinct()
            .Order()
            .ToHashSet();
        if(targets.Count==0){
            return;
        }

        if(!selected){
            var selectedOutsideTargets = cpuCores.Any(cpu => cpu.IsSelected&&!targets.Contains(cpu.LogicalProcessorIndex));
            if(!selectedOutsideTargets&&cpuCores.Any(static cpu => cpu.IsSelected)){
                var keep = cpuCores
                    .Where(cpu => targets.Contains(cpu.LogicalProcessorIndex)&&cpu.IsSelected)
                    .OrderBy(static cpu => cpu.LogicalProcessorIndex)
                    .FirstOrDefault()
                    ??cpuCores
                        .Where(cpu => targets.Contains(cpu.LogicalProcessorIndex))
                        .OrderBy(static cpu => cpu.LogicalProcessorIndex)
                        .FirstOrDefault();
                foreach(var cpu in cpuCores.Where(cpu => targets.Contains(cpu.LogicalProcessorIndex))){
                    cpu.IsSelected=ReferenceEquals(cpu,keep);
                }
                return;
            }
        }

        foreach(var cpu in cpuCores.Where(cpu => targets.Contains(cpu.LogicalProcessorIndex))){
            cpu.IsSelected=selected;
        }
    }

    private static IEnumerable<CpuTableSectionDefinition> BuildSection(string title,CpuCoreKind kind,IReadOnlyList<CpuCoreInfo> cpuCores){
        var rows = cpuCores
            .Where(cpu => cpu.CoreKind==kind)
            .GroupBy(static cpu => cpu.PhysicalCoreIndex)
            .OrderBy(static group => group.Min(static cpu => cpu.LogicalProcessorIndex))
            .Select(static group => group
                .OrderBy(static cpu => cpu.LogicalProcessorIndex)
                .Select(static cpu => cpu.LogicalProcessorIndex)
                .ToArray())
            .Where(static row => row.Length>0)
            .Select(static row => new CpuTableRowDefinition(CreateRangeLabel(row),row))
            .ToArray();
        if(rows.Length>0){
            yield return new CpuTableSectionDefinition(title,kind,rows);
        }
    }

    private static string CreateRangeLabel(IReadOnlyList<int> indexes){
        var min = indexes.Min();
        var max = indexes.Max();
        return min==max ? min.ToString(System.Globalization.CultureInfo.InvariantCulture) : min+"-"+max;
    }
}

internal readonly record struct UiSortValue(string? Text,double? Number,bool IsMissing){
    public static readonly UiSortValue Missing = new(null,null,true);
    public static UiSortValue FromText(string text) => new(text,null,string.IsNullOrWhiteSpace(text));
    public static UiSortValue FromNumber(double? value) => value is { } number&&double.IsFinite(number) ? new(null,number,false) : Missing;

    public static int Compare(UiSortValue left,UiSortValue right,bool descending){
        if(left.IsMissing&&right.IsMissing){
            return 0;
        }

        if(left.IsMissing){
            return 1;
        }

        if(right.IsMissing){
            return -1;
        }

        var result = left.Number is { } leftNumber&&right.Number is { } rightNumber
            ? leftNumber.CompareTo(rightNumber)
            : string.Compare(left.Text,right.Text,StringComparison.OrdinalIgnoreCase);
        return descending ? -result : result;
    }
}
