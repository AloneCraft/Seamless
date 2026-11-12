using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Fact]public void TemporalConversionsMatchMeasuredSqlServerPrecision(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());var plan=engine.Prepare("SELECT CONVERT(datetime,'2024-01-01T00:00:00.005'),CONVERT(smalldatetime,'2024-01-01T00:00:29.998'),CONVERT(smalldatetime,'2024-01-01T00:00:29.999'),CONVERT(datetime2(2),'2024-01-01T23:59:59.995'),CONVERT(time(2),'23:59:59.995'),CONVERT(datetimeoffset(2),'2024-01-01T23:59:59.995+09:00'),CONVERT(time(2),CONVERT(time(7),'00:00:00.125'))","Original DB");
        Assert.Equal(new object?[]{new DateTime(2024,1,1,0,0,0,7),new DateTime(2024,1,1),new DateTime(2024,1,1,0,1,0),new DateTime(2024,1,2),new TimeOnly(23,59,59,990),new DateTimeOffset(2024,1,2,0,0,0,TimeSpan.FromHours(9)),new TimeOnly(0,0,0,130)},Assert.Single(plan.Execute().Rows));Assert.Equal((byte)2,plan.Columns[3].Type.Scale);
        foreach(var expression in new[]{"CONVERT(datetime,'1752-12-31')","CONVERT(smalldatetime,'2079-06-06T23:59:59')","CONVERT(datetime2(2),'9999-12-31T23:59:59.999')"}){Assert.Equal(242,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT "+expression,"Original DB").Execute().Rows.Single()).Number);}
    }
}
