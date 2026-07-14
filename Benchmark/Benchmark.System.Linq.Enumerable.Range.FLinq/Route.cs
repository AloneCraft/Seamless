namespace Benchmark.System.Linq.Enumerable.Range {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqInt32():global::Seamless.Linq.Shared.Range_start_count(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class RangeX {
        public static global::System.Collections.Generic.IEnumerable<int> Create(int start,int count) => global::System.Linq.Enumerable.Range(start,count);
    }
}
