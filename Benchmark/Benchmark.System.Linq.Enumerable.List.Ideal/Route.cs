namespace Benchmark.System.Linq.Enumerable.List.Ideal {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IdealInt32():global::Shared.Linq.IdealSources.List<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IdealClassInt32():global::Shared.Linq.IdealSources.List<global::Common.ClassInt32>(4_096);
}
