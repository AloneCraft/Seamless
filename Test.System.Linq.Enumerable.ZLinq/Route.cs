namespace Test.System.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ZLinq():global::Seamless.Linq.Shared.Methods1(80_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}

