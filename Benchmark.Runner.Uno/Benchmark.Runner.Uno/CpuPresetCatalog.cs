namespace Benchmark.Runner.Uno;

internal sealed record CpuPresetDefinition(string Key,string Label,IReadOnlyList<int> LogicalProcessorIndexes);

internal static class CpuPresetCatalog{
    public static IReadOnlyList<CpuPresetDefinition> Build(IReadOnlyList<CpuCoreInfo> cpuCores){
        if(cpuCores.Count==0){
            return [];
        }

        var ordered = cpuCores
            .OrderBy(static cpu => cpu.LogicalProcessorIndex)
            .ToArray();
        var presets = new List<CpuPresetDefinition>{
            CreatePreset("ALL","ALL",ordered.Select(static cpu => cpu.LogicalProcessorIndex)),
            CreatePreset("CLEAR","CLEAR",[]),
        };

        AddThreadPresets(presets,ordered,CpuCoreKind.Performance,"P");
        AddThreadPresets(presets,ordered,CpuCoreKind.Efficiency,"E");

        return presets;
    }

    private static void AddThreadPresets(
        List<CpuPresetDefinition> presets,
        IEnumerable<CpuCoreInfo> cpuCores,
        CpuCoreKind coreKind,
        string keyPrefix){
        var coreGroups = cpuCores
            .Where(cpu => cpu.CoreKind==coreKind)
            .GroupBy(static cpu => cpu.PhysicalCoreIndex)
            .OrderBy(static group => group.Key)
            .Select(static group => group
                .OrderBy(static cpu => cpu.LogicalProcessorIndex)
                .ToArray())
            .Where(static group => group.Length>0)
            .ToArray();
        if(coreGroups.Length==0){
            return;
        }

        var maxThreadCount = coreGroups.Max(static group => group.Length);
        for(var threadCount=1;threadCount<=maxThreadCount;threadCount++){
            var key = $"{keyPrefix}{threadCount}T";
            presets.Add(CreatePreset(
                key,
                key,
                coreGroups.SelectMany(group => group
                    .Take(threadCount)
                    .Select(static cpu => cpu.LogicalProcessorIndex))));
        }
    }

    private static CpuPresetDefinition CreatePreset(string key,string label,IEnumerable<int> logicalProcessorIndexes){
        return new CpuPresetDefinition(
            key,
            label,
            logicalProcessorIndexes
                .Distinct()
                .Order()
                .ToArray());
    }
}
