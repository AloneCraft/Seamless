using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Fact]public void BooleanNumericComparison(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new Catalog());
        var sql="SELECT CASE WHEN CAST(0 AS bit)=0 THEN 1 ELSE 0 END,CASE WHEN 1=CAST(1 AS bit) THEN 1 ELSE 0 END,CASE WHEN CAST(1 AS bit)=0 THEN 1 ELSE 0 END,CASE WHEN CAST(NULL AS bit)=0 THEN 1 ELSE 0 END";
        Assert.Equal(new object?[]{1,1,0,0},Assert.Single(engine.Prepare(sql,"Original DB").Execute().Rows));
        Assert.Equal(new object?[]{1},Assert.Single(engine.Prepare("SELECT 1 WHERE CAST(0 AS bit)=0","Original DB").Execute().Rows));
        Assert.Empty(engine.Prepare("SELECT 1 WHERE CAST(0 AS bit)=1","Original DB").Execute().Rows);
    }
}
