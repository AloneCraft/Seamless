namespace Benchmark.DotNet.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinq():global::Seamless.Linq.Shared.Methods1(3_200_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static T[] AsX<T>(this T[] source) => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
