namespace Benchmark.Tsavorite.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class LinqInt32():global::Seamless.Linq.Shared.Tsavorite<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class LinqClassInt32():global::Seamless.Linq.Shared.Tsavorite<global::Common.ClassInt32>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::Seamless.Tsavorite.Collections.HashSet<T> AsX<T>(
            this global::Seamless.Tsavorite.Collections.HashSet<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
