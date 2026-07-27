namespace Benchmark.Tsavorite.Linq.Enumerable.FLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqInt32():global::Shared.Linq.Tsavorite<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqClassInt32():global::Shared.Linq.Tsavorite<global::Common.ClassInt32>(4_096);
}

namespace Shared.Linq {
    internal static class Route {
        public static global::Seamless.Collections.Tsavorite.HashSet<T> AsX<T>(
            this global::Seamless.Collections.Tsavorite.HashSet<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
