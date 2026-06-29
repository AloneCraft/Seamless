namespace Benchmark;

public enum BenchmarkSourceTab{
    SystemArray,
    SystemRange,
    SystemList,
    SeamlessPersisted,
    SeamlessTsavorite,
    ScalableSet,
    ScalableDictionary,
    ScalablePersistedList,
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
    public const string ScalablePersistedListId = "Scalable.Persisted.List";

    public static IReadOnlyList<BenchmarkSourceTabDefinition> ScalableAll { get; } = [
        new(
            BenchmarkSourceTab.ScalableSet,
            ScalableSetId,
            "Scalable.Set",
            "Scalable.Persisted.Set",
            "Persisted",
            "Benchmark.Scalable.Persisted.Collection",
            "Scalable.Tsavorite.Set",
            "Tsavorite",
            "Benchmark.Scalable.Tsavorite.Collection"),
        new(
            BenchmarkSourceTab.ScalableDictionary,
            ScalableDictionaryId,
            "Scalable.Dictionary",
            "Scalable.Persisted.Dictionary",
            "Persisted",
            "Benchmark.Scalable.Persisted.Collection",
            "Scalable.Tsavorite.Dictionary",
            "Tsavorite",
            "Benchmark.Scalable.Tsavorite.Collection"),
        new(
            BenchmarkSourceTab.ScalablePersistedList,
            ScalablePersistedListId,
            "Scalable.Persisted.List",
            "Scalable.Persisted.List",
            "Persisted",
            "Benchmark.Scalable.Persisted.Collection",
            string.Empty,
            string.Empty,
            string.Empty),
    ];

    public static IReadOnlyList<BenchmarkSourceTabDefinition> StandardAll { get; } = [
        new(
            BenchmarkSourceTab.SystemArray,
            SystemArrayId,
            "System.Array",
            "System.Array.FLinq",
            "FLinq",
            "Benchmark.DotNet.Linq.Enumerable.FLinq",
            "System.Array.ZLinq",
            "ZLinq",
            "Benchmark.DotNet.Linq.Enumerable.ZLinq"),
        new(
            BenchmarkSourceTab.SystemRange,
            SystemRangeId,
            "System.Collections.Generic.IEnumerable",
            "System.Range.FLinq",
            "FLinq",
            "Benchmark.DotNet.Linq.Enumerable.Range.FLinq",
            "System.Range.ZLinq",
            "ZLinq",
            "Benchmark.DotNet.Linq.Enumerable.Range.ZLinq"),
        new(
            BenchmarkSourceTab.SystemList,
            SystemListId,
            "System.Collections.Generic.List",
            "System.List.FLinq",
            "FLinq",
            "Benchmark.DotNet.Linq.Enumerable.List.FLinq",
            "System.List.ZLinq",
            "ZLinq",
            "Benchmark.DotNet.Linq.Enumerable.List.ZLinq"),
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

    public static BenchmarkSourceTabDefinition Get(BenchmarkSourceTab tab) =>
        All.First(item => item.Tab==tab);

    public static BenchmarkSourceTabDefinition GetById(string id) =>
        All.First(item => string.Equals(item.Id,id,StringComparison.OrdinalIgnoreCase));

    public static BenchmarkSourceTabDefinition? TryGetByProject(string project) =>
        All.FirstOrDefault(item =>
            item.ContainsProject(project));

    public static string ToPathPart(BenchmarkSourceTab tab) =>
        Get(tab).Id;
}
