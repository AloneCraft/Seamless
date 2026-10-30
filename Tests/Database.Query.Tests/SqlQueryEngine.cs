using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Fact]public void Prepare(){
        var catalog=new Catalog();var engine=new Seamless.Database.Query.SqlQueryEngine(catalog);
        Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Unknown FROM dbo.Data","Original DB"));
        Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM dbo.Data WHERE CAST(Id AS varchar(10)) LIKE '[0-9]'","Original DB"));
        Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT [Wrong DB].dbo.Data.Id FROM dbo.Data","Original DB"));
        Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT SUM(CAST(Id AS varchar(20))) FROM dbo.Data","Original DB"));
        Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM dbo.Data OPTION (RECOMPILE)","Original DB"));
        Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM dbo.Data; DELETE dbo.Data","Original DB"));
        var plan=engine.Prepare("SELECT Id,Amount FROM [Original DB].[dbo].[Data]","Original DB");
        Assert.Equal(new SqlObjectName("Original DB","dbo","Data"),plan.Columns[0].Source);
        Assert.Equal((byte)2,plan.Columns[1].Type.Scale);
        Assert.Equal(0,catalog.Reads);
        Assert.Equal("nvarchar",engine.Prepare("SELECT 'prefix'+CAST('x' AS nvarchar(10))","Original DB").Columns[0].Type.Name);
    }
    [Fact]public void Execute(){
        var catalog=new Catalog();var engine=new Seamless.Database.Query.SqlQueryEngine(catalog);
        Assert.Equal(0.333333m,Assert.Single(engine.Prepare("SELECT CAST(1 AS decimal(12,2))/3","Original DB").Execute().Rows)[0]);
        var money=engine.Prepare("SELECT CAST(1.23456 AS money)/3","Original DB");Assert.Equal("money",money.Columns[0].Type.Name);Assert.Equal(0.4115m,Assert.Single(money.Execute().Rows)[0]);
        Assert.Equal("money",engine.Prepare("SELECT SUM(CAST(Amount AS smallmoney)) FROM dbo.Data","Original DB").Columns[0].Type.Name);
        Assert.Equal(8115,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT 10000000000000000000000000000+0.1","Original DB").Execute().Rows.Single()).Number);
        var plan=engine.Prepare("SELECT TOP(1) Id,Amount FROM dbo.Data WHERE Amount>@minimum ORDER BY Id DESC","Original DB",[new("@minimum",SqlType.Decimal)]);
        var result=plan.Execute(new Dictionary<string,object?>{{"@minimum",1m}});
        Assert.Equal(0,catalog.Reads);
        Assert.Equal(new object?[]{3,3m},Assert.Single(result.Rows));
        Assert.Equal(4,catalog.Reads);
        Assert.Equal(new object?[]{3,3m},Assert.Single(plan.Execute(new Dictionary<string,object?>{{"@minimum",2m}}).Rows));
    }
    [Fact]public void NullAndAggregates(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new Catalog());
        Assert.Equal(new object?[]{1.5m,"T1000","longer",2.5m},Assert.Single(engine.Prepare("SELECT CASE WHEN 1=0 THEN 1 ELSE 1.5 END,CASE 1 WHEN 0 THEN 'M200' ELSE 'T1000' END,COALESCE(NULL,'longer'),COALESCE(CAST(NULL AS int),2.5)","Original DB").Execute().Rows));
        Assert.All(engine.Prepare("SELECT COUNT(*),COUNT_BIG(*) FROM dbo.Data","Original DB").Columns,column=>Assert.True(column.IsNullable));
        Assert.Equal(new object?[]{4,3,6m,2m,1m,3m},Assert.Single(engine.Prepare("SELECT COUNT(*),COUNT(Amount),SUM(Amount),AVG(Amount),MIN(Amount),MAX(Amount) FROM dbo.Data","Original DB").Execute().Rows));
        Assert.Empty(engine.Prepare("SELECT Id FROM dbo.Data WHERE NOT(Amount=1 OR Amount<>1)","Original DB").Execute().Rows);
        Assert.Equal(new object?[]{0,0,null,null,null,null},Assert.Single(engine.Prepare("SELECT COUNT(*),COUNT(Amount),SUM(Amount),AVG(Amount),MIN(Amount),MAX(Amount) FROM dbo.Data WHERE Id<0","Original DB").Execute().Rows));
    }
    [Fact]public void GroupJoinAndPivot(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new Catalog());
        var grouped=engine.Prepare("SELECT Id%2 AS Parity,COUNT(*) AS C,SUM(Amount) AS S FROM dbo.Data GROUP BY Id%2 HAVING SUM(Amount)>1 ORDER BY C DESC,Parity","Original DB").Execute().Rows.ToArray();
        Assert.Equal(2,grouped.Length);Assert.Equal(new object?[]{0,2,2m},grouped[0]);Assert.Equal(new object?[]{1,2,4m},grouped[1]);
        var joined=engine.Prepare("SELECT a.Id,b.Amount FROM dbo.Data a LEFT JOIN dbo.Data b ON a.Id=b.Id AND b.Amount>1 WHERE a.Id<3 ORDER BY a.Id","Original DB").Execute().Rows.ToArray();
        Assert.Equal(new object?[]{1,null},joined[0]);Assert.Equal(new object?[]{2,2m},joined[1]);
        var right=engine.Prepare("SELECT a.Id,b.Id FROM dbo.Data a RIGHT JOIN dbo.Data b ON a.Id=b.Id AND a.Amount>1 WHERE b.Id<3 ORDER BY b.Id","Original DB").Execute().Rows.ToArray();Assert.Equal(new object?[]{null,1},right[0]);Assert.Equal(new object?[]{2,2},right[1]);
        Assert.Equal(new object?[]{1m,2m,3m},Assert.Single(engine.Prepare("SELECT [1],[2],[3] FROM (SELECT Id,Amount FROM dbo.Data) d PIVOT(SUM(Amount) FOR Id IN([1],[2],[3])) p","Original DB").Execute().Rows));
        Assert.Equal(new object?[]{3,6m},Assert.Single(engine.Prepare("SELECT COUNT(DISTINCT Amount),SUM(DISTINCT Amount) FROM dbo.Data","Original DB").Execute().Rows));
    }
    [Fact]public void StreamingAndBudget(){
        var catalog=new Catalog();var engine=new Seamless.Database.Query.SqlQueryEngine(catalog);
        Assert.Equal(new object?[]{1},Assert.Single(engine.Prepare("SELECT TOP(1) Id FROM dbo.Data","Original DB").Execute().Rows));Assert.Equal(1,catalog.Reads);
        Assert.Empty(engine.Prepare("SELECT TOP(0) Id FROM dbo.Data","Original DB").Execute().Rows);Assert.Equal(1,catalog.Reads);
        var budget=new Seamless.Execution.ResourceBudget(16384,16*1024*1024);
        var sorted=new Seamless.Database.Query.SqlQueryEngine(new LargeCatalog()).Prepare("SELECT Id,Day,Clock FROM dbo.Data ORDER BY Id DESC","Original DB").Execute(options:new(){ParentBudget=budget,MemoryLimitBytes=16384,TemporaryStorageLimitBytes=16*1024*1024}).Rows;
        Assert.Equal(Enumerable.Range(0,2000).Reverse(),sorted.Select(row=>{var id=(int)row[0]!;Assert.Equal(new DateOnly(2020,1,1).AddDays(id),row[1]);Assert.Equal(new TimeOnly(12,34,56),row[2]);return id;}));Assert.True(budget.PeakTemporaryStorageBytes>0);Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
        var aggregateBudget=new Seamless.Execution.ResourceBudget(16384,0);
        var aggregate=new Seamless.Database.Query.SqlQueryEngine(new LargeCatalog()).Prepare("SELECT COUNT(*),SUM(Id) FROM dbo.Data","Original DB").Execute(options:new(){ParentBudget=aggregateBudget,MemoryLimitBytes=16384,TemporaryStorageLimitBytes=0}).Rows;
        Assert.Equal(new object?[]{2000,1999000},Assert.Single(aggregate));Assert.Equal(0,aggregateBudget.PeakTemporaryStorageBytes);Assert.Equal(0,aggregateBudget.MemoryBytes);Assert.Equal(0,aggregateBudget.TemporaryStorageBytes);
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>engine.Prepare("SELECT Id FROM dbo.Data","Original DB").Execute(options:new(){CancellationToken=cancel.Token}).Rows.First());
    }
    [Fact]public void XmlAndRoutine(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new XmlCatalog());
        var sql="SELECT n.ref.value('declare default element namespace \"urn:test\"; (text())[1]','nvarchar(30)') AS Value FROM dbo.XmlData d CROSS APPLY d.Document.nodes('declare default element namespace \"urn:test\"; /root/item') n(ref)";
        Assert.Equal(new[]{"alpha","beta"},engine.Prepare(sql,"Original DB").Execute().Rows.Select(x=>(string)x[0]!));
        var nested="SELECT x.ref.value('string(.)[1]','nvarchar(30)') FROM dbo.XmlData d CROSS APPLY d.Document.nodes('declare default element namespace \"urn:test\"; /root') n(ref) CROSS APPLY n.ref.nodes('declare default element namespace \"urn:test\"; item') x(ref)";
        Assert.Equal(new[]{"alpha","beta"},engine.Prepare(nested,"Original DB").Execute().Rows.Select(x=>(string)x[0]!));
        Assert.Null(Assert.Single(engine.Prepare("SELECT Document.value('(/missing)[1]','nvarchar(30)') FROM dbo.XmlData","Original DB").Execute().Rows)[0]);
        Assert.Equal(new object?[]{"07","12"},Assert.Single(engine.Prepare("SELECT dbo.Fill(7),dbo.Fill(12)","Original DB").Execute().Rows));
        Assert.Equal(new object?[]{2021,2},Assert.Single(engine.Prepare("SELECT YEAR(DATEADD(m,6,CAST('2020-10-01' AS datetime))),DATEDIFF(day,CAST('2020-12-31' AS datetime),CAST('2021-01-02' AS datetime))","Original DB").Execute().Rows));
        Assert.Equal(new object?[]{new DateOnly(2020,12,31),new TimeOnly(12,34,56),2,new DateTime(2020,12,31)},Assert.Single(engine.Prepare("SELECT CAST('20201231' AS date),CAST('12:34:56' AS time),DATEDIFF(day,CAST('2020-12-31' AS date),CAST('2021-01-02' AS date)),CONVERT(datetime,'2020-12-31',101)","Original DB").Execute().Rows));
    }
    private sealed class LargeCatalog:ISqlCatalog{
        public long Generation=>1;
        public SqlTable?FindTable(SqlObjectName name)=>new(name,[new("Id",SqlType.Int,false,name),new("Day",new("date",typeof(DateOnly)),false,name),new("Clock",new("time",typeof(TimeOnly)),false,name)],_=>Rows());
        private static IEnumerable<object?[]>Rows(){for(var index=0;index<2000;index++){yield return[index,new DateOnly(2020,1,1).AddDays(index),new TimeOnly(12,34,56)];}}
    }
    private sealed class XmlCatalog:ISqlCatalog{
        public long Generation=>1;
        public SqlTable?FindTable(SqlObjectName name)=>new(name,[new("Document",new("xml",typeof(string)),false,name)],_=>new object?[][]{["<root xmlns='urn:test'><item>alpha</item><item>beta</item></root>"]});
        public SqlRoutine?FindRoutine(SqlObjectName name)=>new(name,"CREATE FUNCTION dbo.Fill(@number int) RETURNS char(2) AS BEGIN DECLARE @result char(2); IF @number>9 SET @result=CONVERT(char(2),@number); ELSE SET @result=CONVERT(char(2),'0'+CONVERT(varchar,@number)); RETURN @result; END;");
    }
    private sealed class Catalog:ISqlCatalog{
        public long Generation=>1;public int Reads{get;private set;}
        public SqlTable? FindTable(SqlObjectName name){
            if(name!=new SqlObjectName("Original DB","dbo","Data")){return null;}
            return new(name,[new("Id",SqlType.Int,false,name,0),new("Amount",new("decimal",typeof(decimal),null,12,2),true,name,1)],this.Rows);
        }
        private IEnumerable<object?[]> Rows(CancellationToken token){
            foreach(var row in new object?[][]{[1,1m],[2,2m],[3,3m],[4,null]}){token.ThrowIfCancellationRequested();this.Reads++;yield return row;}
        }
    }
}




