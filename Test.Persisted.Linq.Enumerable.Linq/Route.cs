namespace Test.Persisted.Linq.Enumerable.Linq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class LinqInt32():global::Shared.Linq.Persisted<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class LinqClassInt32():global::Shared.Linq.Persisted<global::Common.ClassInt32>(80_000);
}

namespace Shared.Linq {
    internal static class Route {
        public static Seamless.Collections.Persisted.Set<T> AsX<T>(
            this Seamless.Collections.Persisted.Set<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
