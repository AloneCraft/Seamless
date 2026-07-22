namespace Test.Persisted.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IEnumerableFLinqInt32():global::Seamless.Linq.Shared.Persisted<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IEnumerableFLinqClassInt32():global::Seamless.Linq.Shared.Persisted<ClassInt32>(80_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(
            this global::Seamless.Persisted.Collections.Set<T> source) { global::System.Collections.Generic.IEnumerable<T> result=source; return result; }

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) { global::System.Collections.Generic.IEnumerable<T> result=source; return result; }
    }
}
