namespace Test.Tsavorite.Linq.Enumerable.IEnumerableZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IEnumerableZLinq():global::Shared.Linq.Enumerable.Methods1(80_000);
}

namespace Shared.Linq.Enumerable {
    internal static class Route {
        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => ((IEnumerable<T>)source).AsValueEnumerable();

        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}



