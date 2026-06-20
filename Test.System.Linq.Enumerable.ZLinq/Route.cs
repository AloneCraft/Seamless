namespace Test.System.Linq.Enumerable.ZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ZLinq():global::Shared.Linq.Enumerable.Methods1(80_000);
}

namespace Shared.Linq.Enumerable {
    internal static class Route {
        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}

