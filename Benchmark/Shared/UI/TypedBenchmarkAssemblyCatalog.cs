#if BENCHMARK_UI_TYPED_CATALOG
using System.Reflection;

namespace UI;

internal static class TypedBenchmarkAssemblyCatalog {
    public static IReadOnlyDictionary<string,Assembly> Assemblies { get; } = CreateAssemblies();

    private static IReadOnlyDictionary<string,Assembly> CreateAssemblies() {
        var anchorTypes = GetAnchorTypes();
        return anchorTypes
            .Select(static type => type.Assembly)
            .ToDictionary(
                static assembly => assembly.GetName().Name
                    ??throw new InvalidOperationException("Benchmark assembly name is empty."),
                static assembly => assembly,
                StringComparer.OrdinalIgnoreCase);
    }

    private static Type[] GetAnchorTypes() =>
#if SCALABLE_BENCHMARK
    [
        typeof(global::Benchmark.Scalable.Persisted.Collections.Set.SetInt32),
        typeof(global::Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32),
    ];
#else
    [
        typeof(global::Benchmark.System.Linq.Enumerable.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.ZLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.ZLinqInt32),
        typeof(global::Benchmark.Optimization.All.Anchor),
        typeof(global::Benchmark.Optimization.NoRewrite.Anchor),
        typeof(global::Benchmark.Optimization.NoPipelineFusion.Anchor),
        typeof(global::Benchmark.Optimization.NoPredicatePushdown.Anchor),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.ZLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.ZLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.FLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.LinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.FLinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.LinqInt32),
        typeof(global::Benchmark.Persisted.Collections.Set),
        typeof(global::Benchmark.Tsavorite.Collections.Set),
    ];
#endif
}
#endif
