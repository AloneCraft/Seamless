namespace Test.Persisted.Linq.Enumerable.IEnumerable.FLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IEnumerableFLinqInt32():global::Shared.Linq.Persisted<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IEnumerableFLinqClassInt32():global::Shared.Linq.Persisted<ClassInt32>(80_000);
}

namespace Shared.Linq {
    internal static class Route {
        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(
            this global::Seamless.Collections.Persisted.Set<T> source) { global::System.Collections.Generic.IEnumerable<T> result=source; return result; }

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) { global::System.Collections.Generic.IEnumerable<T> result=source; return result; }
    }
}
