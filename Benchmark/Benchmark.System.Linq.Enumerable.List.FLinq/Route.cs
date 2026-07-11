namespace Benchmark.System.Linq.Enumerable.List {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqInt32():global::Seamless.Linq.Shared.Methods1<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqString():global::Seamless.Linq.Shared.Methods1<string>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static IEnumerable<T> AsX<T>(this List<T> source) => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
