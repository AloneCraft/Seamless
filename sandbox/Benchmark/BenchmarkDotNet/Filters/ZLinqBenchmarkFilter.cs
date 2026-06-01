using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;

namespace Benchmark;

/// <summary>
/// Custom filter that shows benchmarks in the <c>Benchmark.Seamless.*</c> namespace.
/// For the SystemLinq job, <c>SeamlessOnly</c> category benchmarks are excluded.
/// </summary>
public partial class SeamlessBenchmarkFilter : IFilter
{
    public virtual bool Predicate(BenchmarkCase benchmarkCase)
    {
        var ns = benchmarkCase.Descriptor.Type.Namespace!;
        if (!ns.StartsWith("Benchmark.Seamless"))
            return false;

        if (benchmarkCase.Job.Id == SystemLinqBenchmarkConfig.SystemLinqJobId)
        {
            var categories = benchmarkCase.Descriptor.Categories;
            return !categories.Contains(Categories.Filters.SeamlessOnly);
        }

        return true;
    }
}
