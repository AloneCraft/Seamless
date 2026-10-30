using Seamless.Database.Query;

namespace Database.Query.Tests;

public sealed partial class SqlQueryEngine{
    [Fact]public void BasicOwnedSyntaxExecutesExpectedSqlSemantics(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new BasicCatalog());
        object?[][]Run(string sql)=>engine.PrepareBasic(sql,"fixture",SqlDialect.SqlServer).Execute().Rows.ToArray();
        Assert.Equal(new object?[]{4,3,6m,2m,1m,3m},Assert.Single(Run("SELECT COUNT(*),COUNT(Amount),SUM(Amount),AVG(Amount),MIN(Amount),MAX(Amount) FROM dbo.Data")));
        Assert.Equal(new object?[]{0,null},Assert.Single(Run("SELECT COUNT(*),SUM(Amount) FROM dbo.Data WHERE Id<0")));
        Assert.Equal(new object?[]{4},Assert.Single(Run("SELECT Id FROM dbo.Data WHERE Amount IS NULL")));
        var joined=Run("SELECT a.Id,b.Amount FROM dbo.Data a LEFT JOIN dbo.Data b ON a.Id=b.Id AND b.Amount>1 WHERE a.Id<3 ORDER BY a.Id");Assert.Equal(2,joined.Length);Assert.Equal(new object?[]{1,null},joined[0]);Assert.Equal(new object?[]{2,2m},joined[1]);
        var grouped=Run("SELECT Id%2 AS Parity,COUNT(*) AS C,SUM(Amount) AS S FROM dbo.Data GROUP BY Id%2 HAVING SUM(Amount)>1 ORDER BY C DESC,Parity");Assert.Equal(new object?[]{0,2,2m},grouped[0]);Assert.Equal(new object?[]{1,2,4m},grouped[1]);
        Assert.Equal(new object?[]{0.333333m},Assert.Single(Run("SELECT CAST(1 AS decimal(12,2))/3")));
        var parameter=engine.PrepareBasic("SELECT TOP(1) Id FROM dbo.Data WHERE Amount>@minimum ORDER BY Id DESC","fixture",SqlDialect.SqlServer,[new("@minimum",SqlType.Decimal)]);Assert.Equal(new object?[]{3},Assert.Single(parameter.Execute(new Dictionary<string,object?>{{"@minimum",1m}}).Rows));
        Assert.NotSame(engine.Prepare("SELECT 1","fixture"),engine.PrepareBasic("SELECT 1","fixture",SqlDialect.SqlServer));
        Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>engine.PrepareBasic("SELECT Id FROM dbo.Data UNION SELECT Id FROM dbo.Data","fixture",SqlDialect.SqlServer)).Number);
        Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>engine.PrepareBasic("SELECT 1","fixture",SqlDialect.MySql)).Number);
    }
    [Fact]public void BasicViewsExecuteDefinitionsAndRejectInvalidDependencies(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new BasicCatalog());
        var plan=engine.PrepareBasic("SELECT OtherId,Amount FROM dbo.Nested ORDER BY OtherId","fixture",SqlDialect.SqlServer);Assert.Equal("OtherId",plan.Columns[0].Name);var rows=plan.Execute().Rows.ToArray();Assert.Equal(2,rows.Length);Assert.Equal(new object?[]{2,2m},rows[0]);Assert.Equal(new object?[]{3,3m},rows[1]);
        Assert.Equal(4429,Assert.Throws<SqlQueryException>(()=>engine.PrepareBasic("SELECT * FROM dbo.Loop","fixture",SqlDialect.SqlServer)).Number);
        Assert.Equal(4502,Assert.Throws<SqlQueryException>(()=>engine.PrepareBasic("SELECT * FROM dbo.Mismatch","fixture",SqlDialect.SqlServer)).Number);
        Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>engine.PrepareBasic("SELECT * FROM dbo.WrongName","fixture",SqlDialect.SqlServer)).Number);
    }
    private sealed class BasicCatalog:ISqlCatalog{
        public long Generation=>1;
        public SqlTable?FindTable(SqlObjectName name){
            if(name.Database!="fixture"||name.Schema!="dbo"){return null;}
            var columns=new SqlColumn[]{new("Id",SqlType.Int,false,name),new("Amount",new("decimal",typeof(decimal),null,12,2),true,name,1)};
            return name.Name switch{
                "Data"=>new(name,columns,_=>Rows()),
                "V"=>new(name,[columns[0] with{Name="OtherId"},columns[1]],ViewDefinition:"CREATE VIEW dbo.V(OtherId,Amount) AS SELECT Id,Amount FROM dbo.Data WHERE Amount>1"),
                "Nested"=>new(name,[columns[0] with{Name="OtherId"},columns[1]],ViewDefinition:"CREATE VIEW dbo.Nested AS SELECT OtherId,Amount FROM dbo.V"),
                "Loop"=>new(name,columns,ViewDefinition:"CREATE VIEW dbo.Loop AS SELECT Id,Amount FROM dbo.Loop"),
                "Mismatch"=>new(name,[columns[0]],ViewDefinition:"CREATE VIEW dbo.Mismatch AS SELECT Id,Amount FROM dbo.Data"),
                "WrongName"=>new(name,columns,ViewDefinition:"CREATE VIEW dbo.Other AS SELECT Id,Amount FROM dbo.Data"),
                _=>null
            };
        }
        private static IEnumerable<object?[]>Rows(){yield return [1,1m];yield return [2,2m];yield return [3,3m];yield return [4,null];}
    }
}
