namespace Benchmark.Worker;

public sealed record CpuTableSectionDefinition(string Title,CpuCoreKind CoreKind,IReadOnlyList<CpuTableRowDefinition> Rows){
    public int ColumnCount => Rows.Count==0 ? 0 : Rows.Max(static row => row.LogicalProcessorIndexes.Count);
}

public sealed record CpuTableRowDefinition(string Label,IReadOnlyList<int> LogicalProcessorIndexes);

public static class CpuSelectionTableLogic{
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

public readonly record struct UiSortValue(string? Text,double? Number,bool IsMissing){
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
