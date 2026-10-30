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
        typeof(global::Shared.Scalable.Persisted.Collections.Set.SetInt32),
        typeof(global::Shared.Scalable.Tsavorite.Collections.Set.SetInt32),
    ];
#else
    [
        typeof(global::Benchmark.System.Linq.Enumerable.FLinq.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.ZLinq.ZLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.FLinq.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.ZLinq.ZLinqInt32),
        typeof(global::Benchmark.Optimization.All.Anchor),
        typeof(global::Benchmark.Optimization.NoRewrite.Anchor),
        typeof(global::Benchmark.Optimization.NoPipelineFusion.Anchor),
        typeof(global::Benchmark.Optimization.NoPredicatePushdown.Anchor),
        typeof(global::Benchmark.Optimization.NoProjectionMerge.Anchor),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.FLinq.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.ZLinq.ZLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.FLinq.FLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.ZLinq.ZLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.FLinq.FLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.IEnumerable.FLinq.IEnumerableFLinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.Linq.LinqInt32),
        typeof(global::Benchmark.Persisted.Linq.Enumerable.ValueLinq.ValueLinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.FLinq.FLinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.IEnumerable.FLinq.IEnumerableFLinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.Linq.LinqInt32),
        typeof(global::Benchmark.Tsavorite.Linq.Enumerable.ValueLinq.ValueLinqInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.Ideal.IdealInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.IEnumerable.Ideal.IdealInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.Range.Ideal.IdealInt32),
        typeof(global::Benchmark.System.Linq.Enumerable.List.Ideal.IdealInt32),
        typeof(global::Benchmark.Optimization.Ideal.Anchor),
    ];
#endif
}
#endif
