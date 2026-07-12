using Seamless.Tsavorite;

namespace Benchmark.Tsavorite.ParameterSweep;

public sealed class ParameterSweepRunOptions {
    public ParameterSweepSettings Baseline { get; init; } = new(
        IndexSize: 131072,
        PageSize: Const.PageSize,
        SegmentSize: Const.SegmentSize,
        MemorySize: 64L*1024*1024,
        MutableFraction: 1.0);

    public int MaxSweepSteps { get; init; } = 8;
    public int MaxSuccessfulCount { get; init; } = 65536;
    public int LookupProbeCount { get; init; } = 4096;
    public TimeSpan PerCaseTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public long StorageDeviceCapacity { get; init; } = 1L<<30;
    public string? WorkDirectory { get; init; }
    public IReadOnlyList<int> LogicalCpus { get; init; } = [];
}

public static class ParameterSweepPlan {
    private const long MinimumIndexSize = 64;
    private const long MinimumPageSize = 1L<<12;
    private const long MinimumSegmentSize = 1L<<12;
    private const long MinimumMemorySize = 1L<<13;

    public static IReadOnlyList<ParameterSweepCase> CreateDefaultCases(ParameterSweepRunOptions? options = null) {
        var actualOptions = options??new();
        ValidateOptions(actualOptions);
        var cases = new List<ParameterSweepCase>();
        foreach(var backend in new[] { ParameterSweepBackend.Memory, ParameterSweepBackend.Storage }) {
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.HashSet,"Int32",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.HashSet,"String",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.HashSet,"UnmanagedStruct",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.HashSet,"ManagedStruct",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.HashSet,"ManagedClass",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Set,"UnmanagedSetRecord",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Set,"ManagedSetRecord",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Dictionary,"String/String",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Dictionary,"String/Int32",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Dictionary,"Int32/String",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Dictionary,"Int32/Int32",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Dictionary,"UnmanagedStruct/UnmanagedStruct",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Dictionary,"ManagedStruct/ManagedStruct",actualOptions);
            AddCollectionCases(cases,backend,ParameterSweepCollectionKind.Dictionary,"ManagedClass/ManagedClass",actualOptions);
        }
        return cases;
    }

    public static IReadOnlyList<ParameterSweepCase> CreateSmokeCases(ParameterSweepRunOptions? options = null) {
        var actualOptions = options??new ParameterSweepRunOptions {
            MaxSweepSteps=2,
            MaxSuccessfulCount=128,
            LookupProbeCount=32,
            PerCaseTimeout=TimeSpan.FromSeconds(2),
            StorageDeviceCapacity=16L*1024*1024,
        };
        return [
            ..CreateAxisCases(ParameterSweepBackend.Memory,ParameterSweepCollectionKind.HashSet,"Int32",ParameterSweepAxis.IndexSize,actualOptions),
            ..CreateAxisCases(ParameterSweepBackend.Memory,ParameterSweepCollectionKind.Set,"UnmanagedSetRecord",ParameterSweepAxis.MemorySize,actualOptions),
            ..CreateAxisCases(ParameterSweepBackend.Storage,ParameterSweepCollectionKind.Dictionary,"Int32/Int32",ParameterSweepAxis.SegmentSize,actualOptions),
        ];
    }

    private static void AddCollectionCases(
        List<ParameterSweepCase> cases,
        ParameterSweepBackend backend,
        ParameterSweepCollectionKind collection,
        string genericName,
        ParameterSweepRunOptions options) {
        foreach(var axis in Enum.GetValues<ParameterSweepAxis>()) {
            cases.AddRange(CreateAxisCases(backend,collection,genericName,axis,options));
        }
    }

    private static IReadOnlyList<ParameterSweepCase> CreateAxisCases(
        ParameterSweepBackend backend,
        ParameterSweepCollectionKind collection,
        string genericName,
        ParameterSweepAxis axis,
        ParameterSweepRunOptions options) {
        var cases = new List<ParameterSweepCase>(options.MaxSweepSteps);
        for(var step = 0;step<options.MaxSweepSteps;step++) {
            var settings = CreateSettings(axis,step,options.Baseline);
            var caseName = ParameterSweepFormatting.CreateCaseName(collection,genericName,backend,axis,settings);
            cases.Add(new(caseName,collection,genericName,backend,axis,settings));
        }
        return cases;
    }

    private static ParameterSweepSettings CreateSettings(ParameterSweepAxis axis,int step,ParameterSweepSettings baseline) {
        var value = axis switch {
            ParameterSweepAxis.IndexSize => MinimumIndexSize<<step,
            ParameterSweepAxis.MemorySize => MinimumMemorySize<<step,
            ParameterSweepAxis.PageSize => MinimumPageSize<<step,
            ParameterSweepAxis.SegmentSize => MinimumSegmentSize<<step,
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };
        var settings = axis switch {
            ParameterSweepAxis.IndexSize => baseline with { IndexSize=value },
            ParameterSweepAxis.MemorySize => baseline with { MemorySize=value },
            ParameterSweepAxis.PageSize => baseline with {
                PageSize=value,
                SegmentSize=Math.Max(baseline.SegmentSize,value),
                MemorySize=Math.Max(baseline.MemorySize,2*value),
            },
            ParameterSweepAxis.SegmentSize => baseline with { SegmentSize=value },
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };
        return settings;
    }

    public static void ValidateOptions(ParameterSweepRunOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        if(options.MaxSweepSteps<=0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaxSweepSteps));
        }
        if(options.MaxSuccessfulCount<=0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaxSuccessfulCount));
        }
        if(options.LookupProbeCount<=0) {
            throw new ArgumentOutOfRangeException(nameof(options.LookupProbeCount));
        }
        if(options.PerCaseTimeout<=TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(options.PerCaseTimeout));
        }
        if(options.StorageDeviceCapacity<MinimumSegmentSize*2) {
            throw new ArgumentOutOfRangeException(nameof(options.StorageDeviceCapacity));
        }
        if(options.LogicalCpus.Any(static cpu => cpu<0)) {
            throw new ArgumentOutOfRangeException(nameof(options.LogicalCpus));
        }
    }
}
