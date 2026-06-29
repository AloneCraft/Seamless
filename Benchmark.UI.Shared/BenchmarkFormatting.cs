using System.Globalization;

namespace Benchmark;

public static class BenchmarkFormatting {
    public static string FormatNanoseconds(double? nanoseconds) {
        if(nanoseconds is not { } value||!double.IsFinite(value)||value<0) {
            return string.Empty;
        }

        if(value>=1_000_000_000) {
            return (value/1_000_000_000).ToString("0.##",CultureInfo.InvariantCulture)+" s";
        }

        if(value>=1_000_000) {
            return (value/1_000_000).ToString("0.##",CultureInfo.InvariantCulture)+" ms";
        }

        if(value>=1_000) {
            return (value/1_000).ToString("0.##",CultureInfo.InvariantCulture)+" us";
        }

        return value.ToString("0.##",CultureInfo.InvariantCulture)+" ns";
    }

    public static string FormatBytes(long? bytes) {
        if(bytes is not { } value||value<0) {
            return string.Empty;
        }

        if(value>=1024L*1024L*1024L) {
            return (value/(1024d*1024d*1024d)).ToString("0.##",CultureInfo.InvariantCulture)+" GB";
        }

        if(value>=1024L*1024L) {
            return (value/(1024d*1024d)).ToString("0.##",CultureInfo.InvariantCulture)+" MB";
        }

        if(value>=1024L) {
            return (value/1024d).ToString("0.##",CultureInfo.InvariantCulture)+" KB";
        }

        return value.ToString(CultureInfo.InvariantCulture)+" B";
    }

    public static string ShortName(string name) {
        const int maxLength = 48;
        return name.Length<=maxLength
            ? name
            : name[..(maxLength-1)]+"...";
    }
}

