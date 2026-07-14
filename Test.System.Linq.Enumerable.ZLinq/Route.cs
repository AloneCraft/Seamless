namespace Test.System.Linq.Enumerable {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ZLinqInt32():global::Seamless.Linq.Shared.Array<int>(80_000);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public sealed class ZLinqClassInt32():global::Seamless.Linq.Shared.Array<global::Common.ClassInt32>(80_000);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static ValueEnumerable<ZLinq.Linq.FromEnumerable<T>,T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source.AsValueEnumerable();
    }
}

