namespace Test.System.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinq():global::Shared.Linq.Enumerable.Methods1(80_000);
}

namespace Shared.Linq.Enumerable {
    internal static class Route {
        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}


