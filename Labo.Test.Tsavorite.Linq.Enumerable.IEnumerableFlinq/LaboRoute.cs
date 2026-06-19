namespace Labo.Test.Tsavorite.Linq.Enumerable.IEnumerableFLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class IEnumerableFLinq():Labo.Shared.Linq.Enumerable.Methods1(80_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static IEnumerable<T> AsX<T>(this Tsavorite.Collections.HashSet<T> source)
            where T : unmanaged, IEquatable<T> => source;

        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}




