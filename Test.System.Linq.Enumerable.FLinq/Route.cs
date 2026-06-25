namespace Test.System.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinq():global::Seamless.Linq.Enumerable.Shared.Methods1(80_000);
}

namespace Seamless.Linq.Enumerable.Shared {
    internal static class Route {
        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}


