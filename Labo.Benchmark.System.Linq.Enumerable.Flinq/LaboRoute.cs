namespace Labo.Benchmark.System.Linq.Enumerable.Flinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class Flinq():Labo.Shared.Linq.Enumerable.SystemEnumerableMethods(3_200_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}
