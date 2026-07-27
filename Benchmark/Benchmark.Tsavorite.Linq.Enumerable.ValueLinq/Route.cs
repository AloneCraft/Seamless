namespace Benchmark.Tsavorite.Linq.Enumerable.ValueLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ValueLinqInt32():global::Shared.Linq.Tsavorite<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ValueLinqClassInt32():global::Shared.Linq.Tsavorite<global::Common.ClassInt32>(4_096);
}

namespace Shared.Linq {
    using global::Seamless.Linq;

    internal static class Route {
        public static global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> AsX<T>(
            this global::Seamless.Collections.Tsavorite.HashSet<T> source) => source.AsValueEnumerable();

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
