using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace Benchmark;

/// <summary>
/// BenchmarkConfig that compares System.Linq (no interceptor) vs Seamless (intercepted LINQ).
/// </summary>
public class SystemLinqBenchmarkConfig : BaseBenchmarkConfig
{
    internal const string SystemLinqJobId = "SystemLinq";
    internal const string SeamlessJobId = "Seamless";

    public SystemLinqBenchmarkConfig() : base()
    {
        var baseJobConfig = GetBaseJobConfig().Freeze();

        // Add job for System.Linq benchmarks (no Seamless interceptor).
        AddJob(baseJobConfig.WithToolchain(Constants.DefaultToolchain)
                            .WithCustomBuildConfiguration(Constants.CustomBuildConfigurations.SystemLinq)
                            .WithId(SystemLinqJobId)
                            .AsBaseline());

        // Add job for Seamless benchmarks (LINQ calls are intercepted and inlined).
        AddJob(baseJobConfig.WithToolchain(Constants.DefaultToolchain)
                            .WithId(SeamlessJobId));

        // Show summary with declared order (Default: execution order)
        WithOrderer(new DefaultOrderer(summaryOrderPolicy: SummaryOrderPolicy.Declared, jobOrderPolicy: JobOrderPolicy.Numeric));

        // Configure additional settings.
        AddConfigurations();
    }

    protected override void AddAnalyzers()
    {
        // Remove BaselineCustomAnalyzer to suppress warning.
        // Because baseline benchmark might be excluded by filter.
        var analyzers = DefaultConfig.Instance
                                     .GetAnalysers()
                                     .Where(x => x is not BaselineCustomAnalyzer)
                                     .ToArray();

        AddAnalyser(analyzers);
    }

    protected override void AddColumnHidingRules()
    {
        // HideColumns(Column.Toolchain);
        HideColumns(Column.Arguments);
        HideColumns(Column.BuildConfiguration);
    }

    protected override void AddFilters()
    {
        base.AddFilters();
        AddFilter(new SeamlessBenchmarkFilter());
    }

    protected override void AddLogicalGroupRules()
    {
        AddLogicalGroupRules(
        [
            BenchmarkLogicalGroupRule.ByCategory,
            BenchmarkLogicalGroupRule.ByMethod,
        ]);
    }
}
