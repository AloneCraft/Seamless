namespace Benchmark.System.Linq.Enumerable.List {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqInt32():global::Seamless.Linq.Shared.List<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqClassInt32():global::Seamless.Linq.Shared.List<global::Common.ClassInt32>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromList<T>, T> AsX<T>(this global::System.Collections.Generic.List<T> source) => source.AsValueEnumerable();

        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source.AsValueEnumerable();
    }
}
