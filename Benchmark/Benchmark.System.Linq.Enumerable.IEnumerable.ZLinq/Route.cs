namespace Benchmark.System.Linq.Enumerable.IEnumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqInt32():global::Seamless.Linq.Shared.IEnumerable<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqClassInt32():global::Seamless.Linq.Shared.IEnumerable<global::Common.ClassInt32>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>,T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source.AsValueEnumerable();
    }
}
