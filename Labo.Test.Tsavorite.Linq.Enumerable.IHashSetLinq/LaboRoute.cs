namespace Labo.Test.Tsavorite.Linq.Enumerable.IHashSetLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IHashSetLinq():Labo.Shared.Linq.Enumerable.TsavoriteEnumerableMethods(80_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static Tsavorite.Collections.HashSet<T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => source;
    }
}
