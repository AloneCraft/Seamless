namespace Benchmark.Tsavorite.Linq.Enumerable.IEnumerableZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IEnumerableZLinq():global::Shared.Linq.Enumerable.Methods1(3_200_000);
}

namespace Shared.Linq.Enumerable {
    internal static class Route {
        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => ((IEnumerable<T>)source).AsValueEnumerable();

        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}



