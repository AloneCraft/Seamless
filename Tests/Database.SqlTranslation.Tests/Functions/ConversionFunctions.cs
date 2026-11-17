using System.Globalization;
using Seamless.Database.SqlServer.Functions;
using F=Seamless.Database.SqlServer.Functions.ConversionFunctions;
using static Database.SqlServer.Functions.Tests.SqlServerOracle;
namespace Database.SqlServer.Functions.Tests;
public sealed class ConversionFunctions{
    private static DateTime D(string text)=>DateTime.Parse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
    private static (string,Func<object?>) Style(int style,string text,SqlTemporalType type,string sqlType,byte scale=7)=>
        ($"CONVERT(varchar(40),CAST('{text}' AS {sqlType}),{style})",()=>F.DateTimeToString(D(text),type,style,scale));
    [SqlServerFact,Trait("Category","SqlServer")]public void MatchesSqlServer(){
        var styles=new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,20,21,22,23,24,25,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,120,121,126,127};
        var cases=new List<(string,Func<object?>)>();
        foreach(var style in styles){
            cases.Add(Style(style,"2004-07-01T15:04:05.123",SqlTemporalType.DateTime,"datetime"));
            cases.Add(Style(style,"2004-07-01T00:00:00",SqlTemporalType.DateTime,"datetime"));
        }
        foreach(var style in new[]{0,100,9,109,13,113,20,21,23,120,121,126,127}){
            cases.Add(Style(style,"2004-07-01T15:04:05.1234567",SqlTemporalType.DateTime2,"datetime2(7)"));
            cases.Add(Style(style,"2004-07-01T15:04:05.12",SqlTemporalType.DateTime2,"datetime2(3)",3));
        }
        cases.AddRange([
            ("CONVERT(datetime,'99991231',112)",()=>SqlTemporal.ToDateTime(F.StringToDateTime(Session,"99991231",112))),
            ("CONVERT(datetime,'20030701',112)",()=>SqlTemporal.ToDateTime(F.StringToDateTime(Session,"20030701",112))),
            ("CONVERT(datetime,'2004-07-01')",()=>SqlTemporal.ToDateTime(F.StringToDateTime(Session,"2004-07-01"))),
            ("CONVERT(datetime,'07/01/2004')",()=>SqlTemporal.ToDateTime(F.StringToDateTime(Session,"07/01/2004"))),
            ("CONVERT(datetime,'Jul 1 2004 3:04PM')",()=>SqlTemporal.ToDateTime(F.StringToDateTime(Session,"Jul 1 2004 3:04PM"))),
            ("CONVERT(datetime,'not a date')",()=>SqlTemporal.ToDateTime(F.StringToDateTime(Session,"not a date"))),
            ("CONVERT(datetime,'01/07/2004',103)",()=>SqlTemporal.ToDateTime(F.StringToDateTime(Session,"01/07/2004",103))),
            ("CONVERT(varchar(30),CAST(1234567.0 AS float))",()=>F.FloatToString(1234567.0)),
            ("CONVERT(varchar(30),CAST(123.456 AS float))",()=>F.FloatToString(123.456)),
            ("CONVERT(varchar(30),CAST(0.0001234 AS float))",()=>F.FloatToString(0.0001234)),
            ("CONVERT(varchar(30),CAST(0.00001234 AS float))",()=>F.FloatToString(0.00001234)),
            ("CONVERT(varchar(30),CAST(123456.7 AS float))",()=>F.FloatToString(123456.7)),
            ("CONVERT(varchar(30),CAST(1234.5678 AS float),1)",()=>F.FloatToString(1234.5678,1)),
            ("CONVERT(varchar(30),CAST(1234.5678 AS float),2)",()=>F.FloatToString(1234.5678,2)),
            ("CONVERT(varchar(30),CAST(1234.5678 AS money))",()=>F.MoneyToString(1234.5678m)),
            ("CONVERT(varchar(30),CAST(1234567.5 AS money),1)",()=>F.MoneyToString(1234567.5m,1)),
            ("CONVERT(varchar(30),CAST(1234.5 AS money),2)",()=>F.MoneyToString(1234.5m,2)),
            ("CONVERT(varchar(30),CAST(1.5 AS decimal(10,3)))",()=>F.DecimalToString(1.5m,3)),
            ("CONVERT(varchar(30),CAST(-0.5 AS decimal(10,2)))",()=>F.DecimalToString(-0.5m,2)),
            ("CONVERT(varchar(30),CAST(1 AS bit))",()=>F.BitToString(true)),
            ("CONVERT(int,' 42 ')",()=>F.StringToInt32(" 42 ")),
            ("CONVERT(int,'4.2')",()=>F.StringToInt32("4.2")),
            ("CONVERT(int,'')",()=>F.StringToInt32("")),
            ("CONVERT(int,'3000000000')",()=>F.StringToInt32("3000000000")),
            ("CONVERT(decimal(10,2),'1.235')",()=>F.StringToDecimal("1.235",2)),
            ("CONVERT(bit,'true')",()=>F.StringToBit("true")),
            ("CONVERT(bit,'5')",()=>F.StringToBit("5")),
            ("CONVERT(uniqueidentifier,'x')",()=>F.StringToGuid("x"))
        ]);
        Check([..cases]);
    }
}
