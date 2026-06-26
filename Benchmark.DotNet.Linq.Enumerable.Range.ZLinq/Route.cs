namespace Benchmark.DotNet.Linq.Enumerable.Range {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinq():global::Seamless.Linq.Enumerable.Shared.Methods1(3_200_000);
}

namespace Seamless.Linq.Enumerable.Shared {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}
