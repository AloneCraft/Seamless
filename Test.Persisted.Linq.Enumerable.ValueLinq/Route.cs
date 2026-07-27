namespace Test.Persisted.Linq.Enumerable.ValueLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ValueLinqInt32():global::Shared.Linq.Persisted<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ValueLinqClassInt32():global::Shared.Linq.Persisted<ClassInt32>(80_000);
}

namespace Shared.Linq {
    internal static class Route {
        public static global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> AsX<T>(
            this Seamless.Collections.Persisted.Set<T> source) => source.AsValueEnumerable();

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
