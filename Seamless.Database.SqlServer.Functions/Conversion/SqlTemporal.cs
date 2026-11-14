using System.Data.SqlTypes;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 日付と時刻の型ごとの精度へ丸める。関数は CLR の精度(100ナノ秒)で計算し、翻訳器が式の SQL の型に合わせてここを通す。
/// </summary>
public static class SqlTemporal{
    private static readonly DateTime smallDateTimeMinimum=new(1900,1,1);
    private static readonly DateTime smallDateTimeMaximum=new(2079,6,6,23,59,0);
    private static readonly DateTime dateTimeMinimum=new(1753,1,1);
    /// <summary>datetime の精度(1/300秒)へ丸める。範囲外は 242。</summary>
    public static DateTime? ToDateTime(DateTime? value){
        if(value is not{} date){
            return null;
        }
        if(date<dateTimeMinimum){
            throw OutOfRange();
        }
        try{
            return new SqlDateTime(date).Value;
        }catch(SqlTypeException){
            throw OutOfRange();
        }
    }
    /// <summary>smalldatetime の精度(分。29.998秒以下は切り捨て、それより上は切り上げ)へ丸める。範囲外は 242。</summary>
    public static DateTime? ToSmallDateTime(DateTime? value){
        if(value is not{} date){
            return null;
        }
        var minute=date.Ticks/TimeSpan.TicksPerMinute;
        var remainder=date.Ticks%TimeSpan.TicksPerMinute;
        if(remainder>=299_990_000){
            minute++;
        }
        var rounded=new DateTime(minute*TimeSpan.TicksPerMinute);
        if(rounded<smallDateTimeMinimum||rounded>smallDateTimeMaximum){
            throw OutOfRange();
        }
        return rounded;
    }
    /// <summary>datetime2(scale)・time(scale) の精度へ四捨五入する。</summary>
    public static DateTime? ToDateTime2(DateTime? value,byte scale){
        if(value is not{} date){
            return null;
        }
        var quantum=Quantum(scale);
        var ticks=(date.Ticks+quantum/2)/quantum*quantum;
        if(ticks>DateTime.MaxValue.Ticks){
            throw OutOfRange();
        }
        return new DateTime(ticks);
    }
    /// <summary>date へ(時刻を捨てる)。</summary>
    public static DateTime? ToDate(DateTime? value){
        if(value is not{} date){
            return null;
        }
        return date.Date;
    }
    /// <summary>datetimeoffset(scale) の精度へ四捨五入する。</summary>
    public static DateTimeOffset? ToDateTimeOffset(DateTimeOffset? value,byte scale){
        if(value is not{} offset){
            return null;
        }
        var quantum=Quantum(scale);
        return new DateTimeOffset((offset.Ticks+quantum/2)/quantum*quantum,offset.Offset);
    }
    internal static long Quantum(byte scale){
        if(scale>7){
            throw new SqlFunctionException("Specified scale is invalid.",1002);
        }
        long quantum=1;
        for(var index=scale;index<7;index++){
            quantum*=10;
        }
        return quantum;
    }
    private static SqlFunctionException OutOfRange()=>new("The conversion of a varchar data type to a datetime data type resulted in an out-of-range value.",242);
}
