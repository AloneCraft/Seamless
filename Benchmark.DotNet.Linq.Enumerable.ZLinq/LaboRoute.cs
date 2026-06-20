namespace Benchmark.DotNet.Linq.Enumerable.ZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinq():Labo.Shared.Linq.Enumerable.Methods1(3_200_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}

