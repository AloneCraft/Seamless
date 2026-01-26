using System.Diagnostics;
namespace Seamless.Reasoning;
public static class PersonExtensions {
    public static double second(this Stopwatch Stopwatch) {
        var ElapsedTicks = (double)Stopwatch.ElapsedTicks;
        var s = ElapsedTicks / Stopwatch.Frequency;
        return s;
    }
    public static double ms(this Stopwatch Stopwatch) {
        var s=Stopwatch.second();
        var ms = s * 1000;
        return ms;
    }
    public static double us(this Stopwatch Stopwatch) {
        var ms=Stopwatch.ms();
        var us = ms * 1000;
        return us;
    }
    public static double ns(this Stopwatch Stopwatch) {
        var us=Stopwatch.us();
        var ns = us * 1000;
        return ns;
    }
    public static long BytesSecond(this Stopwatch Stopwatch,long Bytes) {
        var ElapsedTicks = (double)Stopwatch.ElapsedTicks;
        var second = ElapsedTicks / Stopwatch.Frequency;
        return (long)(Bytes / second);
    }
    public static long KiBSecond(this Stopwatch Stopwatch,long Bytes) => Stopwatch.BytesSecond(Bytes) / Const.Ki;
    public static long MiBSecond(this Stopwatch Stopwatch,long Bytes) => Stopwatch.BytesSecond(Bytes) / Const.Mi;
    public static long GiBSecond(this Stopwatch Stopwatch,long Bytes) => Stopwatch.BytesSecond(Bytes) / Const.Gi;
}
