namespace Benchmark.Tsavorite.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class Linq():global::Seamless.Linq.Shared.Methods1(3_200_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::Seamless.Tsavorite.Collections.HashSet<T> AsX<T>(
            this global::Seamless.Tsavorite.Collections.HashSet<T> source) => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
