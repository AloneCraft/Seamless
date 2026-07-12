namespace Benchmark.System.Linq.Enumerable.Range {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ZLinqInt32():global::Seamless.Linq.Shared.Range_start_count(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class RangeX {
        public static ValueEnumerable<ZLinq.Linq.FromRange,int>Create(int start,int count)=>ValueEnumerable.Range(start,count);
    }
}
