using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Fact]public void FloatingPointCalculationProvenance(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new FloatingCatalog());
        AssertFlags("SELECT Direct,[Double],Amount,Id FROM dbo.Floating",[false,false,false,false]);
        Assert.Equal(new[]{false,false},engine.Prepare("SELECT @value,0.25E0","Original DB",[new("@value",new("real",typeof(float)))]).Columns.Select(column=>column.IsComputedFloatingPoint));
        AssertFlags("SELECT Direct,Direct+CAST(1 AS real),CAST(Amount AS real),CAST(Direct AS real),CAST(Direct AS float),-Direct,+Direct FROM dbo.Floating",[false,true,true,false,true,true,false]);
        AssertFlags("SELECT SUM(Direct),AVG(Direct),MIN(Direct),MAX(Direct+CAST(1 AS real)),COUNT(Direct) FROM dbo.Floating",[true,true,false,true,false]);
        AssertFlags("SELECT CASE WHEN Id=1 THEN Direct ELSE Direct+CAST(1 AS real) END,CASE Id WHEN 1 THEN Direct+CAST(1 AS real) ELSE Direct/CAST(3 AS real) END,COALESCE(Direct,Direct+CAST(1 AS real)),ISNULL(Direct,Direct+CAST(1 AS real)),NULLIF(Direct,Direct+CAST(1 AS real)),NULLIF(Direct+CAST(1 AS real),Direct) FROM dbo.Floating",[false,true,false,false,false,true]);
        AssertFlags("SELECT (SELECT Direct FROM dbo.Floating),(SELECT Direct+CAST(1 AS real) FROM dbo.Floating)",[false,true]);
        AssertFlags("SELECT dbo.Echo(Direct),dbo.Echo(Direct+CAST(1 AS real)),dbo.Calculate(Direct),dbo.LocalEcho(Direct) FROM dbo.Floating",[false,true,true,false]);
        var mixed=engine.Prepare("SELECT Direct,Direct+CAST(1 AS real) AS Calculated FROM dbo.Floating","Original DB");Assert.Equal(new object?[]{0.25f,1.25f},Assert.Single(mixed.Execute().Rows));Assert.False(mixed.Columns[0].IsComputedFloatingPoint);Assert.True(mixed.Columns[1].IsComputedFloatingPoint);
        void AssertFlags(string sql,bool[]expected)=>Assert.Equal(expected,engine.Prepare(sql,"Original DB").Columns.Select(column=>column.IsComputedFloatingPoint));
    }
    [Fact]public void FloatingPointProvenanceThroughSources(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new FloatingCatalog());
        AssertFlags("SELECT * FROM dbo.Mixed",[false,true]);
        AssertFlags("SELECT * FROM dbo.Nested",[false,true]);
        AssertFlags("SELECT Formula,Native FROM dbo.Nested",[true,false]);
        AssertFlags("WITH c(Native,Formula) AS(SELECT Direct,Direct+CAST(1 AS real) FROM dbo.Floating) SELECT * FROM c",[false,true]);
        AssertFlags("SELECT * FROM(SELECT Direct,Direct+CAST(1 AS real) AS Formula FROM dbo.Floating) d",[false,true]);
        AssertFlags("SELECT Direct AS Value FROM dbo.Floating UNION ALL SELECT Direct FROM dbo.Floating",[false]);
        AssertFlags("SELECT Direct AS Value FROM dbo.Floating UNION ALL SELECT Direct+CAST(1 AS real) FROM dbo.Floating",[false]);
        AssertFlags("SELECT Direct+CAST(1 AS real) AS Value FROM dbo.Floating UNION SELECT Direct/CAST(3 AS real) FROM dbo.Floating",[true]);
        AssertFlags("SELECT Direct,[1] FROM(SELECT Direct,Id,[Double] FROM dbo.Floating) d PIVOT(SUM([Double]) FOR Id IN([1])) p",[false,true]);
        AssertFlags("SELECT Formula,[1] FROM(SELECT Direct+CAST(1 AS real) AS Formula,Id,[Double] FROM dbo.Floating) d PIVOT(MIN([Double]) FOR Id IN([1])) p",[true,false]);
        var captured=new Seamless.Database.Query.SqlQueryEngine(new CapturedCatalog());foreach(var view in new[]{"Invoices","Order Details Extended"}){var plan=captured.Prepare("SELECT * FROM dbo.["+view+"]","Northwind");Assert.False(Assert.Single(plan.Columns.Where(column=>column.Name=="Discount")).IsComputedFloatingPoint);}
        void AssertFlags(string sql,bool[]expected)=>Assert.Equal(expected,engine.Prepare(sql,"Original DB").Columns.Select(column=>column.IsComputedFloatingPoint));
    }
    [Fact]public void BasicFloatingPointProvenance(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new FloatingCatalog());
        Assert.Equal(new[]{false,true},engine.PrepareBasic("SELECT Direct,Direct+CAST(1 AS real) FROM dbo.Floating","Original DB",SqlDialect.SqlServer).Columns.Select(column=>column.IsComputedFloatingPoint));
        Assert.Equal(new[]{false,true},engine.PrepareBasic("SELECT * FROM dbo.Mixed","Original DB",SqlDialect.SqlServer).Columns.Select(column=>column.IsComputedFloatingPoint));
        Assert.Equal(new[]{false,true},engine.PrepareBasic("SELECT * FROM dbo.Nested","Original DB",SqlDialect.SqlServer).Columns.Select(column=>column.IsComputedFloatingPoint));
    }
    private sealed class FloatingCatalog:ISqlCatalog{
        public long Generation=>1;
        public SqlTable?FindTable(SqlObjectName name){
            var real=new SqlType("real",typeof(float));var number=new SqlType("float",typeof(double));
            if(name.Name=="Mixed"){return new(name,[new("Native",real,false),new("Formula",real,false)],ViewDefinition:"CREATE VIEW dbo.Mixed AS SELECT Direct AS Native,Direct+CAST(1 AS real) AS Formula FROM dbo.Floating");}
            if(name.Name=="Nested"){return new(name,[new("Native",real,false),new("Formula",real,false)],ViewDefinition:"CREATE VIEW dbo.Nested AS SELECT * FROM dbo.Mixed");}
            return new(name,[new("Direct",real,false,name){IsComputedFloatingPoint=true},new("Double",number,false,name),new("Amount",new("decimal",typeof(decimal),null,12,2),false,name),new("Id",SqlType.Int,false,name)],_=>new object?[][]{[0.25f,0.5d,0.75m,1]});
        }
        public SqlRoutine?FindRoutine(SqlObjectName name){var body="RETURN @value;";if(name.Name=="Calculate"){body="RETURN @value+CAST(1 AS real);";}if(name.Name=="LocalEcho"){body="DECLARE @local real;SET @local=@value;RETURN @local;";}return new(name,"CREATE FUNCTION dbo.["+name.Name+"](@value real) RETURNS real AS BEGIN "+body+" END;");}
    }
}
