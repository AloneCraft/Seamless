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
        var halfCount = (ordered.Length+1)/2;
        var onePerThreadCore = ordered
            .GroupBy(static cpu => cpu.PhysicalCoreIndex)
            .OrderBy(static group => group.Key)
            .Select(static group => group.OrderBy(static cpu => cpu.LogicalProcessorIndex).First().LogicalProcessorIndex);

        var presets = new List<CpuPresetDefinition>{
            CreatePreset("ALL","All",ordered.Select(static cpu => cpu.LogicalProcessorIndex)),
            CreatePreset("ONE","One",ordered.Take(1).Select(static cpu => cpu.LogicalProcessorIndex)),
            CreatePreset("HALF","Half",ordered.Take(halfCount).Select(static cpu => cpu.LogicalProcessorIndex)),
            CreatePreset("ONE_THREAD_CORE","One/thread-core",onePerThreadCore),
        };

        return presets;
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
