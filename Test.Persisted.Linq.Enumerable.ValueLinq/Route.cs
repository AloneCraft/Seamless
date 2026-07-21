namespace Test.Persisted.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ValueLinqInt32():global::Seamless.Linq.Shared.Persisted<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ValueLinqClassInt32():global::Seamless.Linq.Shared.Persisted<ClassInt32>(80_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.Internal.PersistedSetEnumerator<T>,T> AsX<T>(
            this Seamless.Persisted.Collections.Set<T> source) => source.AsValueEnumerable();

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
