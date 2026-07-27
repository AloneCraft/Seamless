namespace Benchmark.System.Linq.Enumerable.List.ZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqInt32():global::Shared.Linq.List<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqClassInt32():global::Shared.Linq.List<global::Common.ClassInt32>(4_096);
}

namespace Shared.Linq {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromList<T>,T> AsX<T>(this global::System.Collections.Generic.List<T> source) => source.AsValueEnumerable();

        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>,T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source.AsValueEnumerable();
    }
}
