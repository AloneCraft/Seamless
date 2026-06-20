namespace Benchmark.Tsavorite.Linq.Enumerable.IHashSetFLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IHashSetFLinq():global::Shared.Linq.Enumerable.Methods1(3_200_000);
}

namespace Shared.Linq.Enumerable {
    internal static class Route {
        public static Tsavorite.Collections.HashSet<T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}




