namespace Labo.Test.System.Linq.Enumerable.Linq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class Linq():Labo.Shared.Linq.Enumerable.Methods1(80_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}

