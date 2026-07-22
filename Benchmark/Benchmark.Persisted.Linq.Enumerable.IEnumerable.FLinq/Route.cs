namespace Benchmark.Persisted.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IEnumerableFLinqInt32():global::Seamless.Linq.Shared.Persisted<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IEnumerableFLinqClassInt32():global::Seamless.Linq.Shared.Persisted<global::Common.ClassInt32>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(
            this global::Seamless.Persisted.Collections.Set<T> source) { global::System.Collections.Generic.IEnumerable<T> result=source; return result; }

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) { global::System.Collections.Generic.IEnumerable<T> result=source; return result; }
    }
}
