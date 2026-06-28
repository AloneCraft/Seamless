namespace Benchmark.DotNet.Linq.Enumerable.List {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinq():global::Seamless.Linq.Shared.Methods1(3_200_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this List<T> source) => ((IEnumerable<T>)source).AsValueEnumerable();

        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}
