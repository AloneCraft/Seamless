using System.Globalization;
using MemoryPack;
using Seamless.Collections;

namespace Tsavorite.ParameterSweep;

public enum ParameterSweepCollectionKind {
    HashSet,
    Set,
    Dictionary,
}

public enum ParameterSweepBackend {
    Memory,
    Storage,
}

public enum ParameterSweepAxis {
    IndexSize,
    MemorySize,
    PageSize,
    SegmentSize,
}

public enum ParameterSweepFailureKind {
    Completed,
    LogFull,
    OutOfMemory,
    Timeout,
    InvalidConfiguration,
    Exception,
}

public readonly record struct ParameterSweepSettings(
    long IndexSize,
    long PageSize,
    long SegmentSize,
    long MemorySize,
    double MutableFraction);

public sealed record ParameterSweepCase(
    string CaseName,
    ParameterSweepCollectionKind Collection,
    string GenericName,
    ParameterSweepBackend Backend,
    ParameterSweepAxis Sweep,
    ParameterSweepSettings Settings);

public sealed record ParameterSweepResult(
    string RunId,
    DateTimeOffset MeasuredAt,
    string CaseName,
    ParameterSweepCollectionKind Collection,
    string GenericName,
    ParameterSweepBackend Backend,
    ParameterSweepAxis Sweep,
    long IndexSize,
    long PageSize,
    long SegmentSize,
    long MemorySize,
    double MutableFraction,
    long MaxSuccessfulCount,
    long EntryCount,
    ParameterSweepFailureKind FailureKind,
    string FailureMessage,
    double AddElapsedMilliseconds,
    double? LookupMeanNanoseconds);

public sealed record ParameterSweepComparisonRow(
    ParameterSweepResult Current,
    ParameterSweepResult? Baseline,
    long? MaxCountDelta,
    double? AddElapsedRatio,
    double? LookupRatio,
    bool FailureKindChanged);

[MemoryPackable]
public readonly partial record struct ParameterSweepUnmanagedSetRecord(int Key,long Value):IRecord<int>;

[MemoryPackable]
public readonly partial record struct ParameterSweepManagedSetRecord(int Key,string Name):IRecord<int>;

public static class ParameterSweepFormatting {
    public static string FormatSize(long value) {
        if(value%(1L<<30)==0&&value>=1L<<30) {
            return (value>>30).ToString(CultureInfo.InvariantCulture)+"G";
        }
        if(value%(1L<<20)==0&&value>=1L<<20) {
            return (value>>20).ToString(CultureInfo.InvariantCulture)+"M";
        }
        if(value%(1L<<10)==0&&value>=1L<<10) {
            return (value>>10).ToString(CultureInfo.InvariantCulture)+"K";
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string CreateCaseName(
        ParameterSweepCollectionKind collection,
        string genericName,
        ParameterSweepBackend backend,
        ParameterSweepAxis axis,
        ParameterSweepSettings settings) =>
        collection+"<"+genericName+">."
        +backend+"."
        +axis+"."
        +"I"+FormatSize(settings.IndexSize)
        +"_P"+FormatSize(settings.PageSize)
        +"_S"+FormatSize(settings.SegmentSize)
        +"_M"+FormatSize(settings.MemorySize)
        +"_MF"+settings.MutableFraction.ToString("0.###",CultureInfo.InvariantCulture);
}
