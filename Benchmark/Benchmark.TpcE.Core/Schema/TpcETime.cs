namespace Benchmark.TpcE.Core;
/// <summary>
/// 模擬時刻と日付の表し方。DATETIME は 0001-01-01 からのミリ秒(long)、DATE は 0001-01-01 からの日数(long)。
/// 実時刻は使わない(4 通りと再実行で同じ値にするため)。
/// </summary>
public static class TpcETime{
    public const long MillisecondsPerSecond=1000;
    public const long MillisecondsPerDay=86_400_000;
    public static long FromDateTime(DateTime value)=>value.Ticks/TimeSpan.TicksPerMillisecond;
    public static DateTime ToDateTime(long milliseconds)=>new(milliseconds*TimeSpan.TicksPerMillisecond,DateTimeKind.Unspecified);
    public static long DateOf(long milliseconds)=>milliseconds/MillisecondsPerDay;
    public static long DayNumber(int year,int month,int day)=>new DateOnly(year,month,day).DayNumber;
    public static long StartOfDay(long day)=>day*MillisecondsPerDay;
    public static DateOnly ToDate(long day)=>DateOnly.FromDayNumber(checked((int)day));
    public static string Format(long milliseconds)=>ToDateTime(milliseconds).ToString("yyyy-MM-dd HH:mm:ss.fff",System.Globalization.CultureInfo.InvariantCulture);
}
