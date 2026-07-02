namespace DotNet.Linq.Enumerable.List {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqInt32():global::Seamless.Linq.Shared.Methods1<int>(3_200_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqString():global::Seamless.Linq.Shared.Methods1<string>(3_200_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static IEnumerable<T> AsX<T>(this List<T> source) => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
