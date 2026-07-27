namespace Test.System.Linq.Enumerable.ZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ZLinqInt32():global::Shared.Linq.Array<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ZLinqClassInt32():global::Shared.Linq.Array<global::Common.ClassInt32>(80_000);
}

namespace Shared.Linq {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>,T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source.AsValueEnumerable();
    }
}

