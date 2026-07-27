namespace Benchmark.System.Linq.Enumerable.Range.FLinq {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqInt32():global::Shared.Linq.Range_start_count(4_096);
}

namespace Shared.Linq {
    internal static class RangeX {
        public static global::System.Collections.Generic.IEnumerable<int> Create(int start,int count) => global::System.Linq.Enumerable.Range(start,count);
    }
}
