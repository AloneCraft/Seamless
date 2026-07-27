namespace Benchmark.Persisted.Linq.Enumerable.ValueLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ValueLinqInt32():global::Shared.Linq.Persisted<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ValueLinqClassInt32():global::Shared.Linq.Persisted<global::Common.ClassInt32>(4_096);
}

namespace Shared.Linq {
    using global::Seamless.Linq;

    internal static class Route {
        public static global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> AsX<T>(
            this Seamless.Collections.Persisted.Set<T> source) => source.AsValueEnumerable();

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
