namespace Benchmark.DotNet.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class Linq():global::Shared.Linq.Enumerable.Methods1(3_200_000);
}

namespace Shared.Linq.Enumerable {
    internal static class Route {
        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}

