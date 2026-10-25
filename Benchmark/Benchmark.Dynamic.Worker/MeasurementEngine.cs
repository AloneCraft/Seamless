using Benchmark.Dynamic.Core;
using System.Diagnostics;

namespace Benchmark.Dynamic.Worker;

public sealed record MeasurementProfile(
    string ID,
    int WarmupCount,
    int SampleCount,
    TimeSpan MinimumCalibrationDuration,
    int MaximumOperationCount) {
    public static readonly MeasurementProfile BalancedV1=new(
        "Balanced-v1",
        2,
        7,
        TimeSpan.FromMilliseconds(25),
        1_048_576);
}

public sealed record RawMeasurementSample(
    DynamicImplementationKind Implementation,
    int SampleIndex,
    int ExecutionOrder,
    long ElapsedNanoseconds,
    int OperationCount,
    long AllocatedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections) {
    public double NanosecondsPerOperation=>(double)this.ElapsedNanoseconds/this.OperationCount;
    public double AllocatedBytesPerOperation=>(double)this.AllocatedBytes/this.OperationCount;
}

public sealed record MeasurementSummary(
    DynamicImplementationKind Implementation,
    int OperationCount,
    int WarmupCount,
    int SampleCount,
    double MedianNanoseconds,
    double MeanNanoseconds,
    double MinimumNanoseconds,
    double MaximumNanoseconds,
    double StandardDeviationNanoseconds,
    double StandardErrorNanoseconds,
    double AllocatedBytesPerOperation,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    IReadOnlyList<RawMeasurementSample> Samples);

public sealed record BalancedMeasurementResult(
    string ProfileID,
    IReadOnlyList<MeasurementSummary> Measurements,
    IReadOnlyList<RawMeasurementSample> ExecutionOrderedSamples);

public sealed class MeasurementEngine {
    private static readonly DynamicImplementationKind[] MeasuredImplementations=[
        DynamicImplementationKind.System,
        DynamicImplementationKind.FLinqUnoptimized,
        DynamicImplementationKind.FLinq,
        DynamicImplementationKind.ZLinq,
        DynamicImplementationKind.Ideal,
    ];
    private readonly MeasurementProfile Profile;

    public MeasurementEngine(MeasurementProfile? Profile=null) {
        this.Profile=Profile??MeasurementProfile.BalancedV1;
    }

    public BalancedMeasurementResult Measure(
        LoadedDynamicQuery Query,
        object? Primary,
        object? Secondary,
        int QueryIndex,
        int SourceOrdinal,
        CancellationToken CancellationToken=default) {
        ArgumentNullException.ThrowIfNull(Query);
        var Implementations=MeasuredImplementations.Where(Query.Has).ToArray();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        foreach(var Implementation in Implementations) {
            for(var WarmupIndex=0;WarmupIndex<this.Profile.WarmupCount;WarmupIndex++) {
                CancellationToken.ThrowIfCancellationRequested();
                GC.KeepAlive(Query.Run(Implementation,Primary,Secondary));
            }
        }
        var OperationCounts=Implementations.ToDictionary(
            Implementation=>Implementation,
            Implementation=>this.Calibrate(Query.GetDelegate(Implementation),Primary,Secondary,CancellationToken));

        var OrderedSamples=new List<RawMeasurementSample>(this.Profile.SampleCount*Implementations.Length);
        for(var SampleIndex=0;SampleIndex<this.Profile.SampleCount;SampleIndex++) {
            var Rotation=(QueryIndex+SourceOrdinal+SampleIndex)%Implementations.Length;
            for(var Position=0;Position<Implementations.Length;Position++) {
                CancellationToken.ThrowIfCancellationRequested();
                var Implementation=Implementations[(Rotation+Position)%Implementations.Length];
                OrderedSamples.Add(MeasureSample(
                    Query.GetDelegate(Implementation),
                    Implementation,
                    Primary,
                    Secondary,
                    SampleIndex,
                    Position,
                    OperationCounts[Implementation]));
            }
        }

        var Summaries=Implementations.Select(Implementation=>集計(
            Implementation,
            OperationCounts[Implementation],
            OrderedSamples.Where(Sample=>Sample.Implementation==Implementation).OrderBy(Sample=>Sample.SampleIndex).ToArray())).ToArray();
        return new(this.Profile.ID,Summaries,OrderedSamples);
    }

    private int Calibrate(
        Func<object?,object?,DynamicQueryValue> Function,
        object? Primary,
        object? Secondary,
        CancellationToken CancellationToken) {
        var Operations=1;
        while(true) {
            CancellationToken.ThrowIfCancellationRequested();
            var Start=Stopwatch.GetTimestamp();
            DynamicQueryValue? Result=null;
            for(var Index=0;Index<Operations;Index++) {
                Result=Function(Primary,Secondary);
            }
            var Elapsed=Stopwatch.GetElapsedTime(Start);
            GC.KeepAlive(Result);
            if(Elapsed>=this.Profile.MinimumCalibrationDuration||Operations>=this.Profile.MaximumOperationCount) {
                return Operations;
            }
            Operations=Math.Min(checked(Operations*2),this.Profile.MaximumOperationCount);
        }
    }

    private static RawMeasurementSample MeasureSample(
        Func<object?,object?,DynamicQueryValue> Function,
        DynamicImplementationKind Implementation,
        object? Primary,
        object? Secondary,
        int SampleIndex,
        int ExecutionOrder,
        int OperationCount) {
        var Gen0Before=GC.CollectionCount(0);
        var Gen1Before=GC.CollectionCount(1);
        var Gen2Before=GC.CollectionCount(2);
        var AllocationBefore=GC.GetAllocatedBytesForCurrentThread();
        var Start=Stopwatch.GetTimestamp();
        DynamicQueryValue? Result=null;
        for(var Index=0;Index<OperationCount;Index++) {
            Result=Function(Primary,Secondary);
        }
        var ElapsedNanoseconds=(long)(Stopwatch.GetElapsedTime(Start).TotalNanoseconds);
        var AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-AllocationBefore;
        var Gen0=GC.CollectionCount(0)-Gen0Before;
        var Gen1=GC.CollectionCount(1)-Gen1Before;
        var Gen2=GC.CollectionCount(2)-Gen2Before;
        GC.KeepAlive(Result);
        return new(
            Implementation,
            SampleIndex,
            ExecutionOrder,
            ElapsedNanoseconds,
            OperationCount,
            AllocatedBytes,
            Gen0,
            Gen1,
            Gen2);
    }

    private MeasurementSummary 集計(
        DynamicImplementationKind Implementation,
        int OperationCount,
        IReadOnlyList<RawMeasurementSample> Samples) {
        var Values=Samples.Select(Sample=>Sample.NanosecondsPerOperation).Order().ToArray();
        var Median=Values.Length%2==1
            ?Values[Values.Length/2]
            :(Values[Values.Length/2-1]+Values[Values.Length/2])/2d;
        var Mean=Values.Average();
        var Variance=Values.Length>1
            ?Values.Sum(Value=>(Value-Mean)*(Value-Mean))/(Values.Length-1)
            :0d;
        var StandardDeviation=Math.Sqrt(Variance);
        return new(
            Implementation,
            OperationCount,
            this.Profile.WarmupCount,
            Samples.Count,
            Median,
            Mean,
            Values[0],
            Values[^1],
            StandardDeviation,
            StandardDeviation/Math.Sqrt(Values.Length),
            Samples.Average(Sample=>Sample.AllocatedBytesPerOperation),
            Samples.Sum(Sample=>Sample.Gen0Collections),
            Samples.Sum(Sample=>Sample.Gen1Collections),
            Samples.Sum(Sample=>Sample.Gen2Collections),
            Samples);
    }
}
