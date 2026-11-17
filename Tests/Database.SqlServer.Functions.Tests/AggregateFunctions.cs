using F=Seamless.Database.SqlServer.Functions.AggregateFunctions;
using static Database.SqlServer.Functions.Tests.SqlServerOracle;
namespace Database.SqlServer.Functions.Tests;
public sealed class AggregateFunctions{
    private static string Over(string aggregate,string values)=>$"(SELECT {aggregate} FROM (VALUES {values}) v(x))";
    [SqlServerFact,Trait("Category","SqlServer")]public void MatchesSqlServer(){
        Check(
            (Over("SUM(x)","(1),(2),(NULL)"),()=>F.Sum(new int?[]{1,2,null})),
            (Over("SUM(x)","(CAST(NULL AS int))"),()=>F.Sum(new int?[]{null})),
            (Over("SUM(x)","(2147483647),(1)"),()=>F.Sum(new int?[]{int.MaxValue,1})),
            (Over("AVG(x)","(1),(2)"),()=>F.Avg(new int?[]{1,2})),
            (Over("AVG(x)","(-1),(-2)"),()=>F.Avg(new int?[]{-1,-2})),
            (Over("AVG(x)","(CAST(1 AS decimal(10,2))),(CAST(2 AS decimal(10,2)))"),()=>F.Avg(new decimal?[]{1m,2m},2)),
            (Over("AVG(x)","(CAST(1 AS decimal(10,2))),(CAST(1 AS decimal(10,2))),(CAST(2 AS decimal(10,2)))"),()=>F.Avg(new decimal?[]{1m,1m,2m},2)),
            (Over("STRING_AGG(x,',')","('a'),(NULL),('b')"),()=>F.StringAgg(new[]{"a",null,"b"},",")),
            (Over("STRING_AGG(x,',')","(CAST(NULL AS varchar(10)))"),()=>F.StringAgg(new string?[]{null},","))
        );
    }
}
