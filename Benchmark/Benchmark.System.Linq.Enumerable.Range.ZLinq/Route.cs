namespace Benchmark.System.Linq.Enumerable.Range.ZLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqInt32():global::Shared.Linq.Range_start_count(4_096);
}

namespace Shared.Linq {
    internal static class RangeX {
        public static ValueEnumerable<ZLinq.Linq.FromRange,int> Create(int start,int count) => ValueEnumerable.Range(start,count);
    }
}
