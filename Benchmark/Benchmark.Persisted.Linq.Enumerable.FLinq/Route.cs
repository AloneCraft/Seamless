namespace Benchmark.Persisted.Linq.Enumerable.FLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqInt32():global::Shared.Linq.Persisted<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqClassInt32():global::Shared.Linq.Persisted<global::Common.ClassInt32>(4_096);
}

namespace Shared.Linq {
    internal static class Route {
        public static Seamless.Collections.Persisted.Set<T> AsX<T>(
            this Seamless.Collections.Persisted.Set<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
