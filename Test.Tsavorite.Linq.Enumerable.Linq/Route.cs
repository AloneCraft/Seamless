namespace Test.Tsavorite.Linq.Enumerable.Linq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class LinqInt32():global::Shared.Linq.Tsavorite<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class LinqClassInt32():global::Shared.Linq.Tsavorite<global::Common.ClassInt32>(80_000);
}

namespace Shared.Linq {
    internal static class Route {
        public static global::Seamless.Collections.Tsavorite.HashSet<T> AsX<T>(
            this global::Seamless.Collections.Tsavorite.HashSet<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
