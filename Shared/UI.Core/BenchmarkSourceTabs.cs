namespace UI;

public enum BenchmarkSourceTab{
    SystemArray,
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
    string RightAssemblyName){
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

    public bool ShouldWarnForProject(string project) =>
        this.WarningProjectOrder.Contains(project,StringComparer.OrdinalIgnoreCase);
}

public static class BenchmarkSourceTabs{
    public const string SystemArrayId = "System.Array";
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
            "Scalable.Persisted.Collections",
            "Scalable.Persisted.Set.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalableTsavoriteSet,
            ScalableTsavoriteSetId,
            "Tsavorite.Set<T> IndexSize_PageSize_SegmentSize_MemorySize_MutableFraction",
            "Scalable.Tsavorite.Set",
            "Tsavorite",
            "Scalable.Tsavorite.Collections",
            "Scalable.Tsavorite.Set.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalablePersistedDictionary,
            ScalablePersistedDictionaryId,
            "Persisted.Dictionary<TKey,TValue> InitialCapacity_InitialDataCapacity",
            "Scalable.Persisted.Dictionary",
            "Persisted",
            "Scalable.Persisted.Collections",
            "Scalable.Persisted.Dictionary.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalableTsavoriteDictionary,
            ScalableTsavoriteDictionaryId,
            "Tsavorite.Dictionary<TKey,TValue> IndexSize_PageSize_SegmentSize_MemorySize_MutableFraction",
            "Scalable.Tsavorite.Dictionary",
            "Tsavorite",
            "Scalable.Tsavorite.Collections",
            "Scalable.Tsavorite.Dictionary.Previous",
            "Previous",
            string.Empty),
        new(
            BenchmarkSourceTab.ScalablePersistedList,
            ScalablePersistedListId,
            "Persisted.List<T> InitialCapacity_InitialDataCapacity",
            "Scalable.Persisted.List",
            "Persisted",
            "Scalable.Persisted.Collections",
            "Scalable.Persisted.List.Previous",
            "前回",
            string.Empty),
    ];

    public static IReadOnlyList<BenchmarkSourceTabDefinition> StandardAll { get; } = [
        new(
            BenchmarkSourceTab.SystemArray,
            SystemArrayId,
            "System.Array",
            "System.Array.FLinq",
            "FLinq",
            "DotNet.Linq.Enumerable.FLinq",
            "System.Array.ZLinq",
            "ZLinq",
            "DotNet.Linq.Enumerable.ZLinq"),
        new(
            BenchmarkSourceTab.SystemRange,
            SystemRangeId,
            "System.Collections.Generic.IEnumerable",
            "System.Range.FLinq",
            "FLinq",
            "DotNet.Linq.Enumerable.Range.FLinq",
            "System.Range.ZLinq",
            "ZLinq",
            "DotNet.Linq.Enumerable.Range.ZLinq"),
        new(
            BenchmarkSourceTab.SystemList,
            SystemListId,
            "System.Collections.Generic.List",
            "System.List.FLinq",
            "FLinq",
            "DotNet.Linq.Enumerable.List.FLinq",
            "System.List.ZLinq",
            "ZLinq",
            "DotNet.Linq.Enumerable.List.ZLinq"),
        new(
            BenchmarkSourceTab.SeamlessPersisted,
            SeamlessPersistedId,
            "Seamless.Persisted",
            "Seamless.Persisted.FLinq",
            "FLinq",
            "Persisted.Linq.Enumerable.FLinq",
            "Seamless.Linq",
            "Linq",
            "Persisted.Linq.Enumerable.Linq"),
        new(
            BenchmarkSourceTab.SeamlessTsavorite,
            SeamlessTsavoriteId,
            "Seamless.Tsavorite",
            "Seamless.Tsavorite.FLinq",
            "FLinq",
            "Tsavorite.Linq.Enumerable.FLinq",
            "Seamless.Linq",
            "Linq",
            "Tsavorite.Linq.Enumerable.Linq"),
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

    public static BenchmarkSourceTabDefinition? TryGetByProject(string project) =>
        AllDefinitions.FirstOrDefault(item =>
            item.ContainsProject(project));

    public static string ToPathPart(BenchmarkSourceTab tab) =>
        Get(tab).Id;
}
