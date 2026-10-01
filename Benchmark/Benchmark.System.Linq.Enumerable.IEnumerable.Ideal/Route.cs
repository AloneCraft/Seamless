namespace Benchmark.System.Linq.Enumerable.IEnumerable.Ideal {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IdealInt32():global::Shared.Linq.IdealSources.IEnumerable<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IdealClassInt32():global::Shared.Linq.IdealSources.IEnumerable<global::Common.ClassInt32>(4_096);
}
