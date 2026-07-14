namespace Test.Persisted.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinqInt32():global::Seamless.Linq.Shared.Persisted<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinqClassInt32():global::Seamless.Linq.Shared.Persisted<ClassInt32>(80_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static Seamless.Persisted.Collections.Set<T> AsX<T>(
            this Seamless.Persisted.Collections.Set<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
