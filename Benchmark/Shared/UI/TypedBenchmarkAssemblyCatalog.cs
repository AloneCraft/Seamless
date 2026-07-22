#if BENCHMARK_UI_TYPED_CATALOG
using System.Reflection;

namespace UI;

internal static class 型付きベンチマークアセンブリカタログ {
    public static IReadOnlyDictionary<string,Assembly> アセンブリ一覧 { get; } = アセンブリ一覧を作成();

    private static IReadOnlyDictionary<string,Assembly> アセンブリ一覧を作成() {
        var アンカー型一覧 = アンカー型一覧を取得();
        return アンカー型一覧
            .Select(static 型 => 型.Assembly)
            .ToDictionary(
                static アセンブリ => アセンブリ.GetName().Name
                    ??throw new InvalidOperationException("ベンチマークアセンブリ名が空です。"),
                static アセンブリ => アセンブリ,
                StringComparer.OrdinalIgnoreCase);
    }

    private static Type[] アンカー型一覧を取得() =>
#if SCALABLE_BENCHMARK
    [
        typeof(global::Benchmark.Scalable.Persisted.Collections.Set.SetInt32),
        typeof(global::Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32),
    ];
#else
    [
        typeof(global::Benchmark.System.Linq.Enumerable.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.LinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.ZLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.LinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.ZLinqInt32),
        typeof(global::Benchmark.Optimization.All.Anchor),
        typeof(global::Benchmark.Optimization.NoRewrite.Anchor),
        typeof(global::Benchmark.Optimization.NoPipelineFusion.Anchor),
        typeof(global::Benchmark.Optimization.NoPredicatePushdown.Anchor),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.LinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.ZLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.LinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.ZLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.FLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.IEnumerableFLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.LinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.ValueLinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.FLinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.IEnumerableFLinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.LinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.ValueLinqInt32),
        typeof(global::Benchmark.Persisted.Collections.Set),
        typeof(global::Benchmark.Tsavorite.Collections.Set),
    ];
#endif
}
#endif
