namespace Test.Persisted.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinq():global::Seamless.Linq.Enumerable.Shared.Methods1(80_000);
}

namespace Seamless.Linq.Enumerable.Shared {
    internal static class Route {
        public static Seamless.Persisted.Collections.Set<T> AsX<T>(
            this Seamless.Persisted.Collections.Set<T> source) => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
