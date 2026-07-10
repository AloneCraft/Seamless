namespace Tsavorite.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class LinqInt32():global::Seamless.Linq.Shared.Methods1<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class LinqString():global::Seamless.Linq.Shared.Methods1<string>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::Seamless.Tsavorite.Collections.HashSet<T> AsX<T>(
            this global::Seamless.Tsavorite.Collections.HashSet<T> source) => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
