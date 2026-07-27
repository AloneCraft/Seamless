namespace Test.Tsavorite.Linq.Enumerable.FLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinqInt32():global::Shared.Linq.Tsavorite<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinqClassInt32():global::Shared.Linq.Tsavorite<global::Common.ClassInt32>(80_000);
}

namespace Shared.Linq {
    internal static class Route {
        public static global::Seamless.Collections.Tsavorite.HashSet<T> AsX<T>(
            this global::Seamless.Collections.Tsavorite.HashSet<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
