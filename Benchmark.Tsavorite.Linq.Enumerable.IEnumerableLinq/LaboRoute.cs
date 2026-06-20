namespace Benchmark.Tsavorite.Linq.Enumerable.IEnumerableLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IEnumerableLinq():Labo.Shared.Linq.Enumerable.Methods1(3_200_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static IEnumerable<T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}



