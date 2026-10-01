namespace Benchmark.System.Linq.Enumerable.Ideal {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IdealInt32():global::Shared.Linq.IdealSources.Array<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IdealClassInt32():global::Shared.Linq.IdealSources.Array<global::Common.ClassInt32>(4_096);
}
