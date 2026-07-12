namespace UI;

public enum BenchmarkSourceTab{
    OptimizationRewrite,
    OptimizationPipelineFusion,
    OptimizationPredicatePushdown,
    SystemArray,
    SystemIEnumerable,
    SystemRange,
    SystemList,
    SeamlessPersisted,
    SeamlessTsavorite,
    ScalableSet,
    ScalableDictionary,
    ScalablePersistedList,
    ScalablePersistedSet,
    ScalableTsavoriteSet,
    ScalablePersistedDictionary,
    ScalableTsavoriteDictionary,
}

public sealed record ComparisonPairOption(string Key,string DisplayName,string LeftProject,string RightProject);

public sealed record BenchmarkSourceTabDefinition(
    BenchmarkSourceTab Tab,
    string Id,
    string DisplayName,
    string LeftProject,
    string LeftHeader,
    string LeftAssemblyName,
    string RightProject,
    string RightHeader,
    string RightAssemblyName,
    string LeftTypePrefix="",
    string RightTypePrefix=""){
    public IReadOnlyList<string> ProjectOrder { get; } =
        new[] { LeftProject,RightProject }.Where(static project => !string.IsNullOrWhiteSpace(project)).ToArray();

    public IReadOnlyList<string> WarningProjectOrder { get; } =
        new[] { LeftProject,RightProject }.Where(static project => !string.IsNullOrWhiteSpace(project)).ToArray();

    public ComparisonPairOption Pair => new(
        this.Id,
        this.DisplayName,
        this.LeftProject,
        this.RightProject);

    public bool ContainsProject(string project) =>
        this.ProjectOrder.Contains(project,StringComparer.OrdinalIgnoreCase);

    public bool ContainsCase(BenchmarkCase benchmark) {
        if(!this.ContainsProject(benchmark.Project)) {
            return false;
        }

        var assemblyName = this.GetAssemblyName(benchmark.Project);
        if(string.IsNullOrWhiteSpace(assemblyName)||string.IsNullOrWhiteSpace(benchmark.AssemblyName)) {
            return true;
        }

        if(!string.Equals(assemblyName,benchmark.AssemblyName,StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        var prefix=string.Equals(benchmark.Project,this.LeftProject,StringComparison.OrdinalIgnoreCase)?LeftTypePrefix:RightTypePrefix;
        return string.IsNullOrWhiteSpace(prefix)||benchmark.TypeFullName.StartsWith(prefix,StringComparison.Ordinal);
    }

    public bool ShouldWarnForProject(string project) =>
        this.WarningProjectOrder.Contains(project,StringComparer.OrdinalIgnoreCase);

    public string GetAssemblyName(string project) {
        if(string.Equals(project,this.LeftProject,StringComparison.OrdinalIgnoreCase)){
            return this.LeftAssemblyName;
        }

        if(string.Equals(project,this.RightProject,StringComparison.OrdinalIgnoreCase)){
            return this.RightAssemblyName;
        }

        return string.Empty;
    }
}

public static class BenchmarkSourceTabs{
    public const string OptimizationRewriteId="Optimization.Rewrite";
    public const string OptimizationPipelineFusionId="Optimization.PipelineFusion";
    public const string OptimizationPredicatePushdownId="Optimization.PredicatePushdown";
    public const string SystemArrayId = "System.Array";
    public const string SystemIEnumerableId = "System.Collections.Generic.IEnumerable<>";
    public const string SystemRangeId = "System.Range";
    public const string SystemListId = "System.List";
    public const string SeamlessPersistedId = "Seamless.Persisted";
    public const string SeamlessTsavoriteId = "Seamless.Tsavorite";
    public const string ScalableSetId = "Scalable.Set";
    public const string ScalableDictionaryId = "Scalable.Dictionary";
    public const string ScalablePersistedSetId = "Scalable.Persisted.Set";
    public const string ScalableTsavoriteSetId = "Scalable.Tsavorite.Set";
    public const string ScalablePersistedDictionaryId = "Scalable.Persisted.Dictionary";
    public const string ScalableTsavoriteDictionaryId = "Scalable.Tsavorite.Dictionary";
    public const string ScalablePersistedListId = "Scalable.Persisted.List";

    public static IReadOnlyList<BenchmarkSourceTabDefinition> ScalableAll { get; } = [
        new(
            BenchmarkSourceTab.ScalablePersistedSet,
            ScalablePersistedSetId,
            "Persisted.Set<T> InitialCapacity_InitialDataCapacity",
            "Scalable.Persisted.Set",
            "Persisted",
            "Benchmark.Scalable.Persisted.Collections",
            "Scalable.Persisted.Set.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalableTsavoriteSet,
            ScalableTsavoriteSetId,
            "Tsavorite.Set<T> IndexSize_PageSize_SegmentSize_MemorySize_MutableFraction",
            "Scalable.Tsavorite.Set",
            "Tsavorite",
            "Benchmark.Scalable.Tsavorite.Collections",
            "Scalable.Tsavorite.Set.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalablePersistedDictionary,
            ScalablePersistedDictionaryId,
            "Persisted.Dictionary<TKey,TValue> InitialCapacity_InitialDataCapacity",
            "Scalable.Persisted.Dictionary",
            "Persisted",
            "Benchmark.Scalable.Persisted.Collections",
            "Scalable.Persisted.Dictionary.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalableTsavoriteDictionary,
            ScalableTsavoriteDictionaryId,
            "Tsavorite.Dictionary<TKey,TValue> IndexSize_PageSize_SegmentSize_MemorySize_MutableFraction",
            "Scalable.Tsavorite.Dictionary",
            "Tsavorite",
            "Benchmark.Scalable.Tsavorite.Collections",
            "Scalable.Tsavorite.Dictionary.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalablePersistedList,
            ScalablePersistedListId,
            "Persisted.List<T> InitialCapacity_InitialDataCapacity",
            "Scalable.Persisted.List",
            "Persisted",
            "Benchmark.Scalable.Persisted.Collections",
            "Scalable.Persisted.List.Previous",
            "前回",
            string.Empty),
    ];

    public static IReadOnlyList<BenchmarkSourceTabDefinition> StandardAll { get; } = [
        new(BenchmarkSourceTab.OptimizationRewrite,OptimizationRewriteId,"Optimization.Rewrite","Optimization.Rewrite.All","全最適化","Benchmark.Optimization.All","Optimization.Rewrite.Disabled","Rewrite無効","Benchmark.Optimization.NoRewrite","Benchmark.Optimization.Rewrite.","Benchmark.Optimization.Rewrite."),
        new(BenchmarkSourceTab.OptimizationPipelineFusion,OptimizationPipelineFusionId,"Optimization.PipelineFusion","Optimization.PipelineFusion.All","全最適化","Benchmark.Optimization.All","Optimization.PipelineFusion.Disabled","PipelineFusion無効","Benchmark.Optimization.NoPipelineFusion","Benchmark.Optimization.PipelineFusion.","Benchmark.Optimization.PipelineFusion."),
        new(BenchmarkSourceTab.OptimizationPredicatePushdown,OptimizationPredicatePushdownId,"Optimization.PredicatePushdown","Optimization.PredicatePushdown.All","全最適化","Benchmark.Optimization.All","Optimization.PredicatePushdown.Disabled","PredicatePushdown無効","Benchmark.Optimization.NoPredicatePushdown","Benchmark.Optimization.PredicatePushdown.","Benchmark.Optimization.PredicatePushdown."),
        new(
            BenchmarkSourceTab.SystemArray,
            SystemArrayId,
            "System.Array",
            "System.Array.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.FLinq",
            "System.Array.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.ZLinq"),
        new(
            BenchmarkSourceTab.SystemIEnumerable,
            SystemIEnumerableId,
            "System.Collections.Generic.IEnumerable<>",
            "System.IEnumerable.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.IEnumerable.FLinq",
            "System.IEnumerable.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.IEnumerable.ZLinq"),
        new(
            BenchmarkSourceTab.SystemRange,
            SystemRangeId,
            "System.Range",
            "System.Range.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.Range.FLinq",
            "System.Range.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.Range.ZLinq"),
        new(
            BenchmarkSourceTab.SystemList,
            SystemListId,
            "System.Collections.Generic.List",
            "System.List.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.List.FLinq",
            "System.List.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.List.ZLinq"),
        new(
            BenchmarkSourceTab.SeamlessPersisted,
            SeamlessPersistedId,
            "Seamless.Persisted",
            "Seamless.Persisted.FLinq",
            "FLinq",
            "Benchmark.Persisted.Linq.Enumerable.FLinq",
            "Seamless.Linq",
            "Linq",
            "Benchmark.Persisted.Linq.Enumerable.Linq"),
        new(
            BenchmarkSourceTab.SeamlessTsavorite,
            SeamlessTsavoriteId,
            "Seamless.Tsavorite",
            "Seamless.Tsavorite.FLinq",
            "FLinq",
            "Benchmark.Tsavorite.Linq.Enumerable.FLinq",
            "Seamless.Linq",
            "Linq",
            "Benchmark.Tsavorite.Linq.Enumerable.Linq"),
    ];

#if SCALABLE_BENCHMARK
    public static IReadOnlyList<BenchmarkSourceTabDefinition> All => ScalableAll;
#else
    public static IReadOnlyList<BenchmarkSourceTabDefinition> All => StandardAll;
#endif

    public static BenchmarkSourceTabDefinition Default => All[0];

    private static IEnumerable<BenchmarkSourceTabDefinition> AllDefinitions =>
        StandardAll.Concat(ScalableAll);

    public static BenchmarkSourceTabDefinition Get(BenchmarkSourceTab tab) =>
        AllDefinitions.First(item => item.Tab==tab);

    public static BenchmarkSourceTabDefinition GetById(string id) =>
        AllDefinitions.First(item => string.Equals(item.Id,id,StringComparison.OrdinalIgnoreCase));

    public static BenchmarkSourceTabDefinition? TryGetById(string id) =>
        AllDefinitions.FirstOrDefault(item => string.Equals(item.Id,id,StringComparison.OrdinalIgnoreCase));

    public static BenchmarkSourceTabDefinition? TryGetByProject(string project) =>
        AllDefinitions.FirstOrDefault(item =>
            item.ContainsProject(project));

    public static BenchmarkSourceTabDefinition? TryGetByProjectAndAssembly(string project,string assemblyName) {
        var candidates = AllDefinitions
            .Where(item => item.ContainsProject(project))
            .ToArray();
        if(candidates.Length==0) {
            return null;
        }

        if(!string.IsNullOrWhiteSpace(assemblyName)) {
            var assemblyMatch = candidates.FirstOrDefault(item =>
                string.Equals(item.GetAssemblyName(project),assemblyName,StringComparison.OrdinalIgnoreCase));
            if(assemblyMatch is not null) {
                return assemblyMatch;
            }
        }

        return candidates[0];
    }

    public static string ToPathPart(BenchmarkSourceTab tab) =>
        Get(tab).Id;
}
