namespace Labo.Test.System.Linq.Enumerable.ZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ZLinq():Labo.Shared.Linq.Enumerable.SystemEnumerableMethods(80_000);
}

namespace Labo.Shared.Linq.Enumerable {
    internal static class LaboRoute {
        public static ZLinq.ValueEnumerable<ZLinq.Linq.FromEnumerable<T>, T> AsX<T>(this IEnumerable<T> source) => source.AsValueEnumerable();
    }
}
