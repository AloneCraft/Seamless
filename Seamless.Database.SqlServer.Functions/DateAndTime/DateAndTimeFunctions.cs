using System.Globalization;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 日付と時刻の関数。値は CLR の精度で計算する。datetime・smalldatetime などの型の精度へは、翻訳器が <see cref="SqlTemporal"/> で丸める。
/// datetimeoffset を受け取る版は、DATEDIFF では UTC に直して比べ、ほかはその地点の時刻で扱う(本物と同じ)。
/// </summary>
public static class DateAndTimeFunctions{
    private static readonly string[] englishMonths=["January","February","March","April","May","June","July","August","September","October","November","December"];
    private static readonly string[] englishWeekdays=["Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"];
    // ---- 現在時刻 ----
    /// <summary>GETDATE・CURRENT_TIMESTAMP。datetime。同じ文の中では同じ値。</summary>
    [SqlFunction("GETDATE")][SqlFunction("CURRENT_TIMESTAMP")]
    public static DateTime GetDate(SqlSession session)=>SqlTemporal.ToDateTime(session.StatementTime)!.Value;
    /// <summary>GETUTCDATE。datetime。</summary>
    [SqlFunction("GETUTCDATE")]
    public static DateTime GetUtcDate(SqlSession session)=>SqlTemporal.ToDateTime(session.StatementUtcTime)!.Value;
    /// <summary>SYSDATETIME。datetime2(7)。</summary>
    [SqlFunction("SYSDATETIME")]
    public static DateTime SysDateTime(SqlSession session)=>session.StatementTime;
    /// <summary>SYSUTCDATETIME。datetime2(7)。</summary>
    [SqlFunction("SYSUTCDATETIME")]
    public static DateTime SysUtcDateTime(SqlSession session)=>session.StatementUtcTime;
    /// <summary>SYSDATETIMEOFFSET。datetimeoffset(7)。</summary>
    [SqlFunction("SYSDATETIMEOFFSET")]
    public static DateTimeOffset SysDateTimeOffset(SqlSession session)=>new(session.StatementTime,session.StatementTime-session.StatementUtcTime);
    // ---- 部分を取り出す ----
    /// <summary>YEAR。</summary>
    [SqlFunction("YEAR")]
    public static int? Year(DateTime? date)=>date?.Year;
    /// <summary>MONTH。</summary>
    [SqlFunction("MONTH")]
    public static int? Month(DateTime? date)=>date?.Month;
    /// <summary>DAY。</summary>
    [SqlFunction("DAY")]
    public static int? Day(DateTime? date)=>date?.Day;
    /// <summary>DATEPART。週(week)と曜日(weekday)は SET DATEFIRST に従う。</summary>
    [SqlFunction("DATEPART")]
    public static int? DatePart(SqlSession session,SqlDatePart part,DateTime? date){
        if(date is not{} value){
            return null;
        }
        return part switch{
            SqlDatePart.Year=>value.Year,
            SqlDatePart.Quarter=>(value.Month-1)/3+1,
            SqlDatePart.Month=>value.Month,
            SqlDatePart.DayOfYear=>value.DayOfYear,
            SqlDatePart.Day=>value.Day,
            SqlDatePart.Week=>Week(session,value),
            SqlDatePart.Weekday=>Weekday(session,value),
            SqlDatePart.Hour=>value.Hour,
            SqlDatePart.Minute=>value.Minute,
            SqlDatePart.Second=>value.Second,
            SqlDatePart.Millisecond=>(int)(value.Ticks%TimeSpan.TicksPerSecond/TimeSpan.TicksPerMillisecond),
            SqlDatePart.Microsecond=>(int)(value.Ticks%TimeSpan.TicksPerSecond/10),
            SqlDatePart.Nanosecond=>(int)(value.Ticks%TimeSpan.TicksPerSecond*100),
            SqlDatePart.IsoWeek=>ISOWeek.GetWeekOfYear(value),
            SqlDatePart.TzOffset=>0,
            _=>throw new SqlFunctionException($"The datepart {part} is not supported by date function datepart.",9810)
        };
    }
    /// <summary>DATEPART の datetimeoffset 版。tzoffset は時差(分)。</summary>
    [SqlFunction("DATEPART")]
    public static int? DatePart(SqlSession session,SqlDatePart part,DateTimeOffset? date){
        if(date is not{} value){
            return null;
        }
        if(part==SqlDatePart.TzOffset){
            return (int)value.Offset.TotalMinutes;
        }
        return DatePart(session,part,value.DateTime);
    }
    /// <summary>DATENAME。月と曜日は SET LANGUAGE の名前、ほかは DATEPART の数字の文字列。</summary>
    [SqlFunction("DATENAME")]
    public static string? DateName(SqlSession session,SqlDatePart part,DateTime? date){
        if(date is not{} value){
            return null;
        }
        if(part==SqlDatePart.Month){
            return MonthName(session,value.Month);
        }
        if(part==SqlDatePart.Weekday){
            return WeekdayName(session,value.DayOfWeek);
        }
        return DatePart(session,part,value)!.Value.ToString(CultureInfo.InvariantCulture);
    }
    /// <summary>DATENAME の datetimeoffset 版。tzoffset は "+09:00" の形。</summary>
    [SqlFunction("DATENAME")]
    public static string? DateName(SqlSession session,SqlDatePart part,DateTimeOffset? date){
        if(date is not{} value){
            return null;
        }
        if(part==SqlDatePart.TzOffset){
            var sign="+";
            if(value.Offset<TimeSpan.Zero){
                sign="-";
            }
            return sign+value.Offset.Duration().ToString("hh\\:mm",CultureInfo.InvariantCulture);
        }
        return DateName(session,part,value.DateTime);
    }
    // ---- 足し引き ----
    /// <summary>
    /// DATEADD。年・四半期・月は月末で止める(1月31日に1か月足すと2月の末日)。
    /// ナノ秒は100ナノ秒単位へ四捨五入する(49以下は変わらない)。範囲外は 517。
    /// </summary>
    [SqlFunction("DATEADD")]
    public static DateTime? DateAdd(SqlDatePart part,long? number,DateTime? date){
        if(number is not{} count||date is not{} value){
            return null;
        }
        try{
            return part switch{
                SqlDatePart.Year=>value.AddYears(checked((int)count)),
                SqlDatePart.Quarter=>value.AddMonths(checked((int)count*3)),
                SqlDatePart.Month=>value.AddMonths(checked((int)count)),
                SqlDatePart.DayOfYear or SqlDatePart.Day or SqlDatePart.Weekday=>value.AddDays(count),
                SqlDatePart.Week=>value.AddDays(checked(count*7)),
                SqlDatePart.Hour=>value.AddTicks(checked(count*TimeSpan.TicksPerHour)),
                SqlDatePart.Minute=>value.AddTicks(checked(count*TimeSpan.TicksPerMinute)),
                SqlDatePart.Second=>value.AddTicks(checked(count*TimeSpan.TicksPerSecond)),
                SqlDatePart.Millisecond=>value.AddTicks(checked(count*TimeSpan.TicksPerMillisecond)),
                SqlDatePart.Microsecond=>value.AddTicks(checked(count*10)),
                SqlDatePart.Nanosecond=>value.AddTicks((long)Math.Round(count/100m,MidpointRounding.AwayFromZero)),
                _=>throw new SqlFunctionException($"The datepart {part} is not supported by date function dateadd.",9810)
            };
        }catch(Exception error)when(error is ArgumentOutOfRangeException or OverflowException){
            throw new SqlFunctionException("Adding a value to a 'datetime' column caused an overflow.",517);
        }
    }
    /// <summary>DATEADD の datetimeoffset 版。時差は変えない。</summary>
    [SqlFunction("DATEADD")]
    public static DateTimeOffset? DateAdd(SqlDatePart part,long? number,DateTimeOffset? date){
        if(date is not{} value){
            return null;
        }
        if(DateAdd(part,number,value.DateTime) is not{} added){
            return null;
        }
        return new DateTimeOffset(added,value.Offset);
    }
    /// <summary>
    /// DATEDIFF。2つの日時の間で、その単位の境目をいくつ越えたかを数える(1年たったかではない)。
    /// 週の境目は SET DATEFIRST によらず日曜日。int に収まらなければ 535。
    /// </summary>
    [SqlFunction("DATEDIFF")]
    public static int? DateDiff(SqlDatePart part,DateTime? startDate,DateTime? endDate){
        if(DateDiffBig(part,startDate,endDate) is not{} difference){
            return null;
        }
        if(difference<int.MinValue||difference>int.MaxValue){
            throw new SqlFunctionException("The datediff function resulted in an overflow. The number of dateparts separating two date/time instances is too large. Try to use datediff with a less precise datepart.",535);
        }
        return (int)difference;
    }
    /// <summary>DATEDIFF の datetimeoffset 版。UTC に直して比べる。</summary>
    [SqlFunction("DATEDIFF")]
    public static int? DateDiff(SqlDatePart part,DateTimeOffset? startDate,DateTimeOffset? endDate)=>DateDiff(part,startDate?.UtcDateTime,endDate?.UtcDateTime);
    /// <summary>DATEDIFF_BIG。bigint を返す。</summary>
    [SqlFunction("DATEDIFF_BIG")]
    public static long? DateDiffBig(SqlDatePart part,DateTime? startDate,DateTime? endDate){
        if(startDate is not{} start||endDate is not{} end){
            return null;
        }
        return part switch{
            SqlDatePart.Year=>end.Year-start.Year,
            SqlDatePart.Quarter=>(end.Year*4L+(end.Month-1)/3)-(start.Year*4L+(start.Month-1)/3),
            SqlDatePart.Month=>(end.Year*12L+end.Month)-(start.Year*12L+start.Month),
            SqlDatePart.DayOfYear or SqlDatePart.Day or SqlDatePart.Weekday=>(end.Date.Ticks-start.Date.Ticks)/TimeSpan.TicksPerDay,
            // 0001-01-01 は月曜日。1日ずらして7で割ると、日曜日ごとに1つ増える番号になる。
            SqlDatePart.Week=>(end.Date.Ticks/TimeSpan.TicksPerDay+1)/7-(start.Date.Ticks/TimeSpan.TicksPerDay+1)/7,
            SqlDatePart.Hour=>end.Ticks/TimeSpan.TicksPerHour-start.Ticks/TimeSpan.TicksPerHour,
            SqlDatePart.Minute=>end.Ticks/TimeSpan.TicksPerMinute-start.Ticks/TimeSpan.TicksPerMinute,
            SqlDatePart.Second=>end.Ticks/TimeSpan.TicksPerSecond-start.Ticks/TimeSpan.TicksPerSecond,
            SqlDatePart.Millisecond=>end.Ticks/TimeSpan.TicksPerMillisecond-start.Ticks/TimeSpan.TicksPerMillisecond,
            SqlDatePart.Microsecond=>end.Ticks/10-start.Ticks/10,
            SqlDatePart.Nanosecond=>checked((end.Ticks-start.Ticks)*100),
            _=>throw new SqlFunctionException($"The datepart {part} is not supported by date function datediff.",9810)
        };
    }
    // ---- 作る ----
    /// <summary>EOMONTH。その月の末日(date)。monthsToAdd を足してから求める。</summary>
    [SqlFunction("EOMONTH")]
    public static DateTime? EoMonth(DateTime? startDate,int? monthsToAdd=0){
        if(startDate is not{} value||monthsToAdd is not{} months){
            return null;
        }
        DateTime shifted;
        try{
            shifted=value.Date.AddMonths(months);
        }catch(ArgumentOutOfRangeException){
            throw new SqlFunctionException("Adding a value to a 'date' column caused an overflow.",517);
        }
        return new DateTime(shifted.Year,shifted.Month,DateTime.DaysInMonth(shifted.Year,shifted.Month));
    }
    /// <summary>DATEFROMPARTS。正しくない日付は 289。</summary>
    [SqlFunction("DATEFROMPARTS")]
    public static DateTime? DateFromParts(int? year,int? month,int? day){
        if(year is not{} y||month is not{} m||day is not{} d){
            return null;
        }
        try{
            return new DateTime(y,m,d);
        }catch(ArgumentOutOfRangeException){
            throw new SqlFunctionException("Cannot construct data type date, some of the arguments have values which are not valid.",289);
        }
    }
    /// <summary>DATETIMEFROMPARTS。datetime。ミリ秒は datetime の精度へ丸める。</summary>
    [SqlFunction("DATETIMEFROMPARTS")]
    public static DateTime? DateTimeFromParts(int? year,int? month,int? day,int? hour,int? minute,int? seconds,int? milliseconds){
        if(year is not{} y||month is not{} mo||day is not{} d||hour is not{} h||minute is not{} mi||seconds is not{} s||milliseconds is not{} ms){
            return null;
        }
        try{
            return SqlTemporal.ToDateTime(new DateTime(y,mo,d,h,mi,s,ms));
        }catch(ArgumentOutOfRangeException){
            throw new SqlFunctionException("Cannot construct data type datetime, some of the arguments have values which are not valid.",289);
        }
    }
    // ---- 補助 ----
    // SQL Server の曜日の番号(1=月曜 … 7=日曜)。
    private static int SqlDayNumber(DayOfWeek day){
        if(day==DayOfWeek.Sunday){
            return 7;
        }
        return (int)day;
    }
    private static int Weekday(SqlSession session,DateTime value)=>(SqlDayNumber(value.DayOfWeek)-session.DateFirst+7)%7+1;
    // 1月1日を含む週が第1週。週は DATEFIRST の曜日から始まる。
    private static int Week(SqlSession session,DateTime value){
        var januaryFirst=new DateTime(value.Year,1,1);
        var offset=Weekday(session,januaryFirst)-1;
        return (value.DayOfYear-1+offset)/7+1;
    }
    private static string MonthName(SqlSession session,int month){
        if(IsEnglish(session)){
            return englishMonths[month-1];
        }
        throw new NotSupportedException($"DATENAME の月名は言語 {session.Language} に対応していない。");
    }
    private static string WeekdayName(SqlSession session,DayOfWeek day){
        if(IsEnglish(session)){
            return englishWeekdays[(int)day];
        }
        throw new NotSupportedException($"DATENAME の曜日名は言語 {session.Language} に対応していない。");
    }
    private static bool IsEnglish(SqlSession session)=>session.Language.Equals("us_english",StringComparison.OrdinalIgnoreCase)||session.Language.Equals("English",StringComparison.OrdinalIgnoreCase);
}
