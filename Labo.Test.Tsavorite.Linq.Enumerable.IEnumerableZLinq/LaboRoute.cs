namespace Labo.Test.Tsavorite.Linq.Enumerable.IEnumerableZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IEnumerableZLinq():Labo.Shared.Linq.Enumerable.TsavoriteEnumerableMethods(80_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => ((IEnumerable<T>)source).AsValueEnumerable();
    }
}
