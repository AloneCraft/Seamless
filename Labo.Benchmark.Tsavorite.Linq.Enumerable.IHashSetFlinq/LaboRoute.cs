namespace Labo.Benchmark.Tsavorite.Linq.Enumerable.IHashSetFlinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IHashSetFlinq():Labo.Shared.Linq.Enumerable.TsavoriteEnumerableMethods(3_200_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static Tsavorite.Collections.HashSet<T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => source;
    }
}
