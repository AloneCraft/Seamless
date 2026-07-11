using Benchmark.Tsavorite.ParameterSweep;

namespace Test.Benchmark.Tsavorite.ParameterSweep;

public sealed class ParameterSweepRunnerTests {
    [Fact]
    public async Task RunAsync_HashSetInt32Smoke_CompletesAndLooksUp() {
        var options = new ParameterSweepRunOptions {
            MaxSweepSteps=1,
            MaxSuccessfulCount=64,
            LookupProbeCount=16,
            PerCaseTimeout=TimeSpan.FromSeconds(3),
        };
        var cases = ParameterSweepPlan.CreateDefaultCases(options)
            .Where(static item => item.Backend==ParameterSweepBackend.Memory
                &&item.Collection==ParameterSweepCollectionKind.HashSet
                &&item.GenericName=="Int32"
                &&item.Sweep==ParameterSweepAxis.MemorySize)
            .Take(1)
            .ToArray();

        var results = await new ParameterSweepRunner().RunAsync(cases,options);

        var result = Assert.Single(results);
        Assert.Equal(64,result.MaxSuccessfulCount);
        Assert.Equal(ParameterSweepFailureKind.Completed,result.FailureKind);
        Assert.NotNull(result.LookupMeanNanoseconds);
        Assert.Equal(result.CaseName,cases[0].CaseName);
    }

    [Fact]
    public async Task RunAsync_StorageFailure_IsCapturedAsResult() {
        var options = new ParameterSweepRunOptions {
            MaxSweepSteps=1,
            MaxSuccessfulCount=64,
            LookupProbeCount=16,
            PerCaseTimeout=TimeSpan.FromSeconds(3),
            StorageDeviceCapacity=8192,
        };
        var cases = new[] {
            new ParameterSweepCase(
                "Storage.Invalid",
                ParameterSweepCollectionKind.Dictionary,
                "Int32/Int32",
                ParameterSweepBackend.Storage,
                ParameterSweepAxis.SegmentSize,
                new(64,4096,4096,8192,1.0)),
        };

        var results = await new ParameterSweepRunner().RunAsync(cases,options);

        var result = Assert.Single(results);
        Assert.NotEqual(ParameterSweepFailureKind.Completed,result.FailureKind);
    }

    [Fact]
    public void Compare_UsesLoadedResults() {
        var result = new ParameterSweepResult(
            "run",
            DateTimeOffset.UtcNow,
            "HashSet<Int32>.Memory.IndexSize.I64_P4K_S4K_M8K_MF1",
            ParameterSweepCollectionKind.HashSet,
            "Int32",
            ParameterSweepBackend.Memory,
            ParameterSweepAxis.IndexSize,
            64,
            4096,
            4096,
            8192,
            1,
            10,
            10,
            ParameterSweepFailureKind.Completed,
            string.Empty,
            1.5,
            20);

        var compared = Assert.Single(ParameterSweepComparer.Compare([result],[result]));

        Assert.Equal(0,compared.MaxCountDelta);
        Assert.Equal(1,compared.AddElapsedRatio);
        Assert.False(compared.FailureKindChanged);
    }
}
