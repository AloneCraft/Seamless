using System.Globalization;
using Seamless.Database.SqlServer.Functions;
using F=Seamless.Database.SqlServer.Functions.DateAndTimeFunctions;
using C=Seamless.Database.SqlServer.Functions.ConversionFunctions;
using static Database.SqlServer.Functions.Tests.SqlServerOracle;
namespace Database.SqlServer.Functions.Tests;
public sealed class DateAndTimeFunctions{
    private static DateTime D(string text)=>DateTime.Parse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
    private static string Dt2(string text)=>$"CAST('{text}' AS datetime2(7))";
    private static string Dt(string text)=>$"CAST('{text}' AS datetime)";
    [Fact,Trait("Category","Unit")]public void DatePart(){
        {
            //週は 1月1日を含む週が第1週で、DATEFIRST 7 なら日曜から始まる
            Assert.Equal(2,F.DatePart(Session,SqlDatePart.Week,D("2023-01-08")));
            Assert.Equal(1,F.DatePart(Session,SqlDatePart.Week,D("2023-01-07")));
        }
        {
            //DATEFIRST 1(月曜始まり)では曜日の番号が変わる
            var monday=new SqlSession{DateFirst=1};
            Assert.Equal(1,F.DatePart(monday,SqlDatePart.Weekday,D("2023-01-02")));
            Assert.Equal(2,F.DatePart(Session,SqlDatePart.Weekday,D("2023-01-02")));
        }
    }
    [Fact,Trait("Category","Unit")]public void DateAdd(){
        {
            //四半期は3か月(LinqDB は4で割っていた)。月末で止める
            Assert.Equal(D("2020-04-30"),F.DateAdd(SqlDatePart.Quarter,1,D("2020-01-31")));
        }
        {
            //ミリ秒は 10000 ティック(LinqDB はマイクロ秒を足していた)
            Assert.Equal(D("2020-01-01T00:00:00.0010000"),F.DateAdd(SqlDatePart.Millisecond,1,D("2020-01-01")));
        }
        {
            //マイクロ秒は 10 ティック、ナノ秒は 100 ナノ秒単位へ四捨五入
            Assert.Equal(D("2020-01-01T00:00:00.0000010"),F.DateAdd(SqlDatePart.Microsecond,1,D("2020-01-01")));
            Assert.Equal(D("2020-01-01"),F.DateAdd(SqlDatePart.Nanosecond,49,D("2020-01-01")));
            Assert.Equal(D("2020-01-01T00:00:00.0000001"),F.DateAdd(SqlDatePart.Nanosecond,50,D("2020-01-01")));
        }
        {
            //datetime へ丸めると -2 ミリ秒は .997 になる
            var date=C.StringToDateTime(Session,"20040701",112);
            Assert.Equal(D("2004-06-30T23:59:59.9970000"),SqlTemporal.ToDateTime(F.DateAdd(SqlDatePart.Millisecond,-2,date)));
        }
    }
    [SqlServerFact,Trait("Category","SqlServer")]public void MatchesSqlServer(){
        Check(
            ("YEAR("+Dt2("2004-07-01")+")",()=>F.Year(D("2004-07-01"))),
            ("MONTH("+Dt2("2004-07-01")+")",()=>F.Month(D("2004-07-01"))),
            ("DAY("+Dt2("2004-07-31")+")",()=>F.Day(D("2004-07-31"))),
            ("DATEPART(quarter,"+Dt2("2004-08-01")+")",()=>F.DatePart(Session,SqlDatePart.Quarter,D("2004-08-01"))),
            ("DATEPART(dayofyear,"+Dt2("2004-12-31")+")",()=>F.DatePart(Session,SqlDatePart.DayOfYear,D("2004-12-31"))),
            ("DATEPART(week,"+Dt2("2023-01-07")+")",()=>F.DatePart(Session,SqlDatePart.Week,D("2023-01-07"))),
            ("DATEPART(week,"+Dt2("2023-01-08")+")",()=>F.DatePart(Session,SqlDatePart.Week,D("2023-01-08"))),
            ("DATEPART(week,"+Dt2("2023-12-31")+")",()=>F.DatePart(Session,SqlDatePart.Week,D("2023-12-31"))),
            ("DATEPART(weekday,"+Dt2("2023-01-02")+")",()=>F.DatePart(Session,SqlDatePart.Weekday,D("2023-01-02"))),
            ("DATEPART(iso_week,"+Dt2("2021-01-03")+")",()=>F.DatePart(Session,SqlDatePart.IsoWeek,D("2021-01-03"))),
            ("DATEPART(millisecond,"+Dt2("2020-01-01T10:20:30.1234567")+")",()=>F.DatePart(Session,SqlDatePart.Millisecond,D("2020-01-01T10:20:30.1234567"))),
            ("DATEPART(microsecond,"+Dt2("2020-01-01T10:20:30.1234567")+")",()=>F.DatePart(Session,SqlDatePart.Microsecond,D("2020-01-01T10:20:30.1234567"))),
            ("DATEPART(nanosecond,"+Dt2("2020-01-01T10:20:30.1234567")+")",()=>F.DatePart(Session,SqlDatePart.Nanosecond,D("2020-01-01T10:20:30.1234567"))),
            ("DATEPART(hour,"+Dt2("2020-01-01T23:59:59")+")",()=>F.DatePart(Session,SqlDatePart.Hour,D("2020-01-01T23:59:59"))),
            ("DATENAME(month,"+Dt2("2004-07-01")+")",()=>F.DateName(Session,SqlDatePart.Month,D("2004-07-01"))),
            ("DATENAME(weekday,"+Dt2("2004-07-01")+")",()=>F.DateName(Session,SqlDatePart.Weekday,D("2004-07-01"))),
            ("DATENAME(day,"+Dt2("2004-07-01")+")",()=>F.DateName(Session,SqlDatePart.Day,D("2004-07-01"))),
            ("DATENAME(year,"+Dt2("2004-07-01")+")",()=>F.DateName(Session,SqlDatePart.Year,D("2004-07-01"))),
            ("DATEADD(quarter,1,"+Dt2("2020-01-31")+")",()=>F.DateAdd(SqlDatePart.Quarter,1,D("2020-01-31"))),
            ("DATEADD(month,1,"+Dt2("2020-01-31")+")",()=>F.DateAdd(SqlDatePart.Month,1,D("2020-01-31"))),
            ("DATEADD(m,6,"+Dt2("2003-08-31")+")",()=>F.DateAdd(SqlDatePart.Month,6,D("2003-08-31"))),
            ("DATEADD(year,1,"+Dt2("2020-02-29")+")",()=>F.DateAdd(SqlDatePart.Year,1,D("2020-02-29"))),
            ("DATEADD(week,-2,"+Dt2("2020-03-01")+")",()=>F.DateAdd(SqlDatePart.Week,-2,D("2020-03-01"))),
            ("DATEADD(millisecond,1,"+Dt2("2020-01-01")+")",()=>F.DateAdd(SqlDatePart.Millisecond,1,D("2020-01-01"))),
            ("DATEADD(microsecond,1,"+Dt2("2020-01-01")+")",()=>F.DateAdd(SqlDatePart.Microsecond,1,D("2020-01-01"))),
            ("DATEADD(nanosecond,49,"+Dt2("2020-01-01")+")",()=>F.DateAdd(SqlDatePart.Nanosecond,49,D("2020-01-01"))),
            ("DATEADD(nanosecond,50,"+Dt2("2020-01-01")+")",()=>F.DateAdd(SqlDatePart.Nanosecond,50,D("2020-01-01"))),
            ("DATEADD(nanosecond,150,"+Dt2("2020-01-01")+")",()=>F.DateAdd(SqlDatePart.Nanosecond,150,D("2020-01-01"))),
            ("DATEADD(day,1,"+Dt2("9999-12-31")+")",()=>F.DateAdd(SqlDatePart.Day,1,D("9999-12-31"))),
            ("DATEADD(millisecond,-2,CONVERT(datetime,'20040701',112))",()=>SqlTemporal.ToDateTime(F.DateAdd(SqlDatePart.Millisecond,-2,C.StringToDateTime(Session,"20040701",112)))),
            ("DATEADD(hour,5,"+Dt("2020-01-01T22:00:00")+")",()=>SqlTemporal.ToDateTime(F.DateAdd(SqlDatePart.Hour,5,D("2020-01-01T22:00:00")))),
            ("DATEDIFF(yy,"+Dt2("2003-12-31")+","+Dt2("2004-01-01")+")",()=>F.DateDiff(SqlDatePart.Year,D("2003-12-31"),D("2004-01-01"))),
            ("DATEDIFF(quarter,"+Dt2("2003-03-31")+","+Dt2("2003-04-01")+")",()=>F.DateDiff(SqlDatePart.Quarter,D("2003-03-31"),D("2003-04-01"))),
            ("DATEDIFF(month,"+Dt2("2003-01-31")+","+Dt2("2003-02-01")+")",()=>F.DateDiff(SqlDatePart.Month,D("2003-01-31"),D("2003-02-01"))),
            ("DATEDIFF(week,"+Dt2("2023-01-07")+","+Dt2("2023-01-08")+")",()=>F.DateDiff(SqlDatePart.Week,D("2023-01-07"),D("2023-01-08"))),
            ("DATEDIFF(week,"+Dt2("2023-01-08")+","+Dt2("2023-01-14")+")",()=>F.DateDiff(SqlDatePart.Week,D("2023-01-08"),D("2023-01-14"))),
            ("DATEDIFF(week,"+Dt2("2023-01-14")+","+Dt2("2022-12-31")+")",()=>F.DateDiff(SqlDatePart.Week,D("2023-01-14"),D("2022-12-31"))),
            ("DATEDIFF(day,"+Dt2("2020-01-01T23:59:59")+","+Dt2("2020-01-02T00:00:00")+")",()=>F.DateDiff(SqlDatePart.Day,D("2020-01-01T23:59:59"),D("2020-01-02T00:00:00"))),
            ("DATEDIFF(hour,"+Dt2("2020-01-01T10:59:59")+","+Dt2("2020-01-01T11:00:00")+")",()=>F.DateDiff(SqlDatePart.Hour,D("2020-01-01T10:59:59"),D("2020-01-01T11:00:00"))),
            ("DATEDIFF(second,"+Dt2("2020-01-01T10:00:00.9999999")+","+Dt2("2020-01-01T10:00:01")+")",()=>F.DateDiff(SqlDatePart.Second,D("2020-01-01T10:00:00.9999999"),D("2020-01-01T10:00:01"))),
            ("DATEDIFF(millisecond,"+Dt2("2000-01-01")+","+Dt2("2020-01-01")+")",()=>F.DateDiff(SqlDatePart.Millisecond,D("2000-01-01"),D("2020-01-01"))),
            ("DATEDIFF_BIG(millisecond,"+Dt2("2000-01-01")+","+Dt2("2020-01-01")+")",()=>F.DateDiffBig(SqlDatePart.Millisecond,D("2000-01-01"),D("2020-01-01"))),
            ("DATEDIFF(microsecond,"+Dt2("2020-01-01")+","+Dt2("2020-01-01T00:00:01.0000015")+")",()=>F.DateDiff(SqlDatePart.Microsecond,D("2020-01-01"),D("2020-01-01T00:00:01.0000015"))),
            ("DATEDIFF(nanosecond,"+Dt2("2020-01-01")+","+Dt2("2020-01-01T00:00:01")+")",()=>F.DateDiff(SqlDatePart.Nanosecond,D("2020-01-01"),D("2020-01-01T00:00:01"))),
            ("DATEDIFF(nanosecond,"+Dt2("2020-01-01")+","+Dt2("2020-01-01T00:00:03")+")",()=>F.DateDiff(SqlDatePart.Nanosecond,D("2020-01-01"),D("2020-01-01T00:00:03"))),
            ("EOMONTH("+Dt2("2020-02-10")+")",()=>F.EoMonth(D("2020-02-10"))),
            ("EOMONTH("+Dt2("2020-01-31")+",1)",()=>F.EoMonth(D("2020-01-31"),1)),
            ("DATEFROMPARTS(2020,2,30)",()=>F.DateFromParts(2020,2,30)),
            ("DATEFROMPARTS(2020,2,29)",()=>F.DateFromParts(2020,2,29)),
            ("DATETIMEFROMPARTS(2020,1,1,10,20,30,999)",()=>F.DateTimeFromParts(2020,1,1,10,20,30,999)),
            ("DATEPART(year,CAST(NULL AS datetime2))",()=>F.DatePart(Session,SqlDatePart.Year,(DateTime?)null))
        );
    }
}
