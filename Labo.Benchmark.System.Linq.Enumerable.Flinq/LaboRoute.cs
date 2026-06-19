namespace Labo.Benchmark.System.Linq.Enumerable.FLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class FLinq():Labo.Shared.Linq.Enumerable.Methods1(3_200_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
    }
}


