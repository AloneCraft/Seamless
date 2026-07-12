namespace Benchmark.Persisted.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class LinqInt32():global::Seamless.Linq.Shared.Persisted<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class LinqClassInt32():global::Seamless.Linq.Shared.Persisted<global::Common.ClassInt32>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static Seamless.Persisted.Collections.Set<T> AsX<T>(
            this Seamless.Persisted.Collections.Set<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
