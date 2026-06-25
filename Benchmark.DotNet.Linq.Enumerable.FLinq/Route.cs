namespace Benchmark.DotNet.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinq():global::Seamless.Linq.Enumerable.Shared.Methods1(3_200_000);
}

namespace Seamless.Linq.Enumerable.Shared {
    internal static class Route {
        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
