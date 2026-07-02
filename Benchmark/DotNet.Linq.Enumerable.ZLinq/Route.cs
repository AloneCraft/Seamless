namespace DotNet.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqInt32():global::Seamless.Linq.Shared.Methods1<int>(3_200_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqString():global::Seamless.Linq.Shared.Methods1<string>(3_200_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromArray<T>, T> AsX<T>(this T[] source) => source.AsValueEnumerable();

        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}

