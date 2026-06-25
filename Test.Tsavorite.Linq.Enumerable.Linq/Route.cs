namespace Test.Tsavorite.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class Linq():global::Seamless.Linq.Enumerable.Shared.Methods1(80_000);
}

namespace Seamless.Linq.Enumerable.Shared {
    internal static class Route {
        public static global::Seamless.Tsavorite.Collections.HashSet<T> AsX<T>(
            this global::Seamless.Tsavorite.Collections.HashSet<T> source) => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
