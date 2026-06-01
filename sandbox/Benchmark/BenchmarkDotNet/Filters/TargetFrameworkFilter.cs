using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;

namespace Benchmark;

/// <summary>
/// Custom BenchmarkDotNet filter to exclude benchmarks based on target framework specified as category.
/// This filter is required because BenchmarkDotNet don't support conditional benchmarks with `#if` directive.
/// </summary>
public partial class TargetFrameworkFilter : IFilter
{
    public static readonly TargetFrameworkFilter Instance = new();

    public virtual bool Predicate(BenchmarkCase benchmarkCase)
    {
        var toolchain = benchmarkCase.Job.Infrastructure.Toolchain;
        var name = toolchain?.Name;
        if (name == null || !name.StartsWith(".NET "))
            return true;

        var versionText = name.Substring(".NET ".Length);

        if (!Version.TryParse(versionText, out var targetVersion))
            return true; // Return true if failed to resolve target version.

        var categories = benchmarkCase.Descriptor.Categories;
        foreach (var category in categories)
        {
            switch (category)
            {
                case Categories.Filters.NET8_0_OR_GREATER:
                    return targetVersion.Major >= 8;
                case Categories.Filters.NET9_0_OR_GREATER:
                    return targetVersion.Major >= 9;
                case Categories.Filters.NET10_0_OR_GREATER:
                    return targetVersion.Major >= 10;

                default:
                    continue;
            }
        }

        return true;
    }
}
