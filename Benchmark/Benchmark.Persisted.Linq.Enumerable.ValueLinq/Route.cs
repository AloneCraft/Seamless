namespace Benchmark.Persisted.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ValueLinqInt32():global::Seamless.Linq.Shared.Persisted<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ValueLinqClassInt32():global::Seamless.Linq.Shared.Persisted<global::Common.ClassInt32>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.Internal.PersistedSetEnumerator<T>,T> AsX<T>(
            this Seamless.Persisted.Collections.Set<T> source) => source.AsValueEnumerable();

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
