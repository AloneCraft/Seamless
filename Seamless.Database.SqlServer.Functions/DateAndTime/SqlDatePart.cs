namespace Seamless.Database.SqlServer.Functions;
/// <summary>DATEPART・DATENAME・DATEADD・DATEDIFF の1つ目の引数。</summary>
public enum SqlDatePart{
    Year,Quarter,Month,DayOfYear,Day,Week,Weekday,Hour,Minute,Second,Millisecond,Microsecond,Nanosecond,TzOffset,IsoWeek
}
/// <summary>日付の単位の名前と略称を <see cref="SqlDatePart"/> へ直す。</summary>
public static class SqlDateParts{
    /// <summary>yy・mm・dw などの略称も受け付ける。知らない名前は 155 のエラー。</summary>
    public static SqlDatePart Parse(string name)=>name.Trim().Trim('[',']').ToUpperInvariant() switch{
        "YEAR" or "YY" or "YYYY"=>SqlDatePart.Year,
        "QUARTER" or "QQ" or "Q"=>SqlDatePart.Quarter,
        "MONTH" or "MM" or "M"=>SqlDatePart.Month,
        "DAYOFYEAR" or "DY" or "Y"=>SqlDatePart.DayOfYear,
        "DAY" or "DD" or "D"=>SqlDatePart.Day,
        "WEEK" or "WK" or "WW"=>SqlDatePart.Week,
        "WEEKDAY" or "DW" or "W"=>SqlDatePart.Weekday,
        "HOUR" or "HH"=>SqlDatePart.Hour,
        "MINUTE" or "MI" or "N"=>SqlDatePart.Minute,
        "SECOND" or "SS" or "S"=>SqlDatePart.Second,
        "MILLISECOND" or "MS"=>SqlDatePart.Millisecond,
        "MICROSECOND" or "MCS"=>SqlDatePart.Microsecond,
        "NANOSECOND" or "NS"=>SqlDatePart.Nanosecond,
        "TZOFFSET" or "TZ"=>SqlDatePart.TzOffset,
        "ISO_WEEK" or "ISOWK" or "ISOWW"=>SqlDatePart.IsoWeek,
        _=>throw new SqlFunctionException($"'{name}' is not a recognized datepart option.",155)
    };
}
