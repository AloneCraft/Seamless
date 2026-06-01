using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;

namespace Benchmark;

/// <summary>
/// Custom BenchmarkDotNet filter to exclude benchmarks based on ZLinq version category.
/// </summary>
public partial class ZLinqNuGetVersionFilter : IFilter
{
    public static readonly ZLinqNuGetVersionFilter Instance = new();

    public virtual bool Predicate(BenchmarkCase benchmarkCase)
    {
        var jobId = benchmarkCase.Job.Id;
        if (jobId == "vLocalBuild")
            return true;

        var version = Version.Parse(jobId.Substring(1)); // Gets version from JobId (Remove `v` prefix)
        var categories = benchmarkCase.Descriptor.Categories;
        foreach (var category in categories.OrderDescending())
        {
            switch (category)
            {
                case Categories.Filters.ZLINQ_1_4_0_OR_GREATER:
                    return version >= Version.Parse("1.4.0");
                case Categories.Filters.ZLINQ_1_3_1_OR_GREATER:
                    return version >= Version.Parse("1.3.1");
                case Categories.Filters.ZLINQ_1_2_0_OR_GREATER:
                    return version >= Version.Parse("1.2.0");
                default:
                    continue;
            }
        }

        return true;
    }
}
