using Benchmark.Tsavorite.ParameterSweep;

namespace Test.Benchmark.Tsavorite.ParameterSweep;

public sealed class ParameterSweepPlanTests {
    [Fact]
    public void CreateDefaultCases_UsesOneFactorSweepOrder() {
        var options = new ParameterSweepRunOptions {
            MaxSweepSteps=3,
            MaxSuccessfulCount=16,
        };
        var cases = ParameterSweepPlan.CreateDefaultCases(options)
            .Where(static item => item.Backend==ParameterSweepBackend.Memory
                &&item.Collection==ParameterSweepCollectionKind.HashSet
                &&item.GenericName=="Int32"
                &&item.Sweep==ParameterSweepAxis.IndexSize)
            .ToArray();

        Assert.Equal(3,cases.Length);
        Assert.Equal(64,cases[0].Settings.IndexSize);
        Assert.Equal(128,cases[1].Settings.IndexSize);
        Assert.Equal(256,cases[2].Settings.IndexSize);
        Assert.All(cases,item => Assert.Contains("HashSet<Int32>.Memory.IndexSize",item.CaseName));
    }

    [Fact]
    public void CreateDefaultCases_PageSweepKeepsSettingsValid() {
        var options = new ParameterSweepRunOptions {
            MaxSweepSteps=4,
            MaxSuccessfulCount=16,
        };
        var cases = ParameterSweepPlan.CreateDefaultCases(options)
            .Where(static item => item.Sweep==ParameterSweepAxis.PageSize)
            .Take(4)
            .ToArray();

        Assert.All(cases,item => {
            Assert.True(item.Settings.PageSize<=item.Settings.SegmentSize);
            Assert.True(item.Settings.MemorySize>=2*item.Settings.PageSize);
        });
    }

    [Fact]
    public void CreateDefaultCases_InvalidOptions_Throws() {
        var options = new ParameterSweepRunOptions {
            MaxSweepSteps=0,
        };

        Assert.Throws<ArgumentOutOfRangeException>(()=>ParameterSweepPlan.CreateDefaultCases(options));
    }
}
