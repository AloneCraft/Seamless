using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Fact]public void ComparisonTypeAndCollation(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());
        foreach(var condition in new[]{"'a'=Ci","@text=Ci","N'あ'=Japanese","'a' IN(Ci)","'a' IN(SELECT Ci FROM dbo.Strings)","'a' BETWEEN Ci AND Ci","'2'=Id","CAST(Ci AS nvarchar(10))='a'","CASE 'a' WHEN Ci THEN 1 ELSE 0 END=1","NULLIF('a',Ci) IS NULL"}){
            var plan=engine.Prepare("SELECT Id FROM dbo.Strings WHERE "+condition,"Original DB",[new("@text",SqlType.String)]);
            Assert.Equal(new object?[]{2},Assert.Single(plan.Execute(new Dictionary<string,object?>{{"@text","a"}}).Rows));
        }
        Assert.Equal(245,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM dbo.Strings WHERE 'bad'=Id","Original DB").Execute().Rows.Single()).Number);
        Assert.Empty(engine.Prepare("SELECT Id FROM dbo.Strings WHERE 'a'=Binary","Original DB").Execute().Rows);
        Assert.Empty(engine.Prepare("SELECT Id FROM dbo.Strings WHERE 'a'='A'","Original DB").Execute().Rows);
        Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT 'a' COLLATE Unknown_CI_AS","Original DB")).Number);
        Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT CAST('a' AS varchar(10)) COLLATE Unknown_CI_AS","Original DB")).Number);
        var conditional=engine.Prepare("SELECT CASE WHEN Id=2 THEN Ci ELSE 'fallback' END,CAST(Ci AS nvarchar(10)),CAST(Id AS varchar(10)) FROM dbo.Strings","Original DB");
        Assert.Equal("SQL_Latin1_General_CP1_CI_AS",conditional.Columns[0].Type.Collation);
        Assert.Equal("SQL_Latin1_General_CP1_CI_AS",conditional.Columns[1].Type.Collation);
        Assert.Equal("Latin1_General_100_BIN2",conditional.Columns[2].Type.Collation);
        Assert.Equal(new object?[]{"A","A","2"},Assert.Single(conditional.Execute().Rows));
        Assert.Equal(468,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM dbo.Strings WHERE Ci=Binary","Original DB")).Number);
        Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM dbo.Strings WHERE Unknown='a'","Original DB").Execute().Rows.Single()).Number);
    }
    [Fact]public void DecimalLiteralPrecision(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());
        var literal=engine.Prepare("SELECT CAST(1 AS decimal(12,2))/3","Original DB");Assert.Equal((byte)16,literal.Columns[0].Type.Precision);Assert.Equal((byte)6,literal.Columns[0].Type.Scale);Assert.Equal(new object?[]{0.333333m},Assert.Single(literal.Execute().Rows));
        var cast=engine.Prepare("SELECT CAST(1 AS decimal(12,2))/CAST(3 AS int)","Original DB");Assert.Equal((byte)23,cast.Columns[0].Type.Precision);Assert.Equal((byte)13,cast.Columns[0].Type.Scale);Assert.Equal(new object?[]{0.3333333333333m},Assert.Single(cast.Execute().Rows));
        var column=engine.Prepare("SELECT CAST(1 AS decimal(12,2))/Id FROM dbo.Strings","Original DB");Assert.Equal((byte)23,column.Columns[0].Type.Precision);Assert.Equal((byte)13,column.Columns[0].Type.Scale);Assert.Equal(new object?[]{0.5m},Assert.Single(column.Execute().Rows));
        Assert.Equal(new object?[]{0},Assert.Single(engine.Prepare("SELECT 1/3","Original DB").Execute().Rows));
    }
    [Fact]public void UnionCommonTypesAndCollation(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());
        var literal=engine.Prepare("SELECT 1 AS Value UNION ALL SELECT 2.5","Original DB");Assert.Equal("decimal",literal.Columns[0].Type.Name);Assert.Equal((byte)2,literal.Columns[0].Type.Precision);Assert.Equal((byte)1,literal.Columns[0].Type.Scale);Assert.Equal(new[]{1m,2.5m},literal.Execute().Rows.Select(row=>Assert.IsType<decimal>(row[0])));
        var literalCases=engine.Prepare("SELECT CASE WHEN 1=1 THEN 1 ELSE 2.5 END,COALESCE(1,2.5),CASE WHEN 1=1 THEN CAST(1 AS int) ELSE 2.5 END","Original DB");Assert.Equal(new byte?[]{2,2,11},literalCases.Columns.Select(column=>column.Type.Precision));Assert.Equal(new byte?[]{1,1,1},literalCases.Columns.Select(column=>column.Type.Scale));Assert.Equal(new object?[]{1m,1m,1m},Assert.Single(literalCases.Execute().Rows));
        var column=engine.Prepare("SELECT Id AS Value FROM dbo.Strings UNION ALL SELECT 2.5","Original DB");Assert.Equal((byte)11,column.Columns[0].Type.Precision);Assert.Equal(new[]{2m,2.5m},column.Execute().Rows.Select(row=>Assert.IsType<decimal>(row[0])));
        var derived=engine.Prepare("SELECT Value FROM (SELECT 1 AS Value) d UNION ALL SELECT 2.5","Original DB");Assert.Equal((byte)2,derived.Columns[0].Type.Precision);
        var computed=engine.Prepare("SELECT ABS(1) AS Value UNION ALL SELECT 2.5","Original DB");Assert.Equal((byte)11,computed.Columns[0].Type.Precision);
        var numeric=engine.Prepare("SELECT CAST(1 AS decimal(12,0)) AS Value UNION ALL SELECT 2.5","Original DB");
        Assert.Equal("decimal",numeric.Columns[0].Type.Name);Assert.Equal((byte)13,numeric.Columns[0].Type.Precision);Assert.Equal((byte)1,numeric.Columns[0].Type.Scale);
        Assert.Equal(new[]{1m,2.5m},numeric.Execute().Rows.Select(row=>Assert.IsType<decimal>(row[0])));
        var text=engine.Prepare("SELECT CAST('a' AS varchar(1)) AS Value UNION ALL SELECT CAST('long' AS varchar(10))","Original DB");
        Assert.Equal(10,text.Columns[0].Type.Length);Assert.Equal(new[]{"a","long"},text.Execute().Rows.Select(row=>(string)row[0]!));
        var distinct=engine.Prepare("SELECT Ci AS Value FROM dbo.Strings UNION SELECT 'a'","Original DB");
        Assert.Equal("SQL_Latin1_General_CP1_CI_AS",distinct.Columns[0].Type.Collation);Assert.Single(distinct.Execute().Rows);
        var nullable=engine.Prepare("SELECT Id AS Value FROM dbo.Strings UNION ALL SELECT CAST(NULL AS int)","Original DB");Assert.True(nullable.Columns[0].IsNullable);
    }
    [Fact]public void PivotAggregateTypesAndCollation(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());
        var pivot=engine.Prepare("SELECT [a] FROM (SELECT Ci,Amount FROM dbo.Strings) d PIVOT(SUM(Amount) FOR Ci IN([a])) p","Original DB");
        Assert.Equal((byte)38,pivot.Columns[0].Type.Precision);Assert.Equal((byte)2,pivot.Columns[0].Type.Scale);Assert.Equal(new object?[]{1.25m},Assert.Single(pivot.Execute().Rows));
        var count=engine.Prepare("SELECT [a] FROM (SELECT Ci,Amount FROM dbo.Strings) d PIVOT(COUNT(Amount) FOR Ci IN([a])) p","Original DB");Assert.Equal("int",count.Columns[0].Type.Name);Assert.Equal(new object?[]{1},Assert.Single(count.Execute().Rows));
        var grouped=engine.Prepare("SELECT GroupName,[1] FROM dbo.PivotRows PIVOT(SUM(Amount) FOR Id IN([1])) p","Original DB");Assert.Equal(new object?[]{"A",3},Assert.Single(grouped.Execute().Rows));
        var empty=engine.Prepare("SELECT [a] FROM (SELECT Ci,Amount FROM dbo.Strings WHERE Id<0) d PIVOT(COUNT(Amount) FOR Ci IN([a])) p","Original DB");Assert.Empty(empty.Execute().Rows);
        var budget=new Seamless.Execution.ResourceBudget(16384,0);var bounded=new Seamless.Database.Query.SqlQueryEngine(new LargeCatalog()).Prepare("SELECT [0],[1] FROM (SELECT Id,Id%2 AS Parity FROM dbo.Data) d PIVOT(COUNT(Id) FOR Parity IN([0],[1])) p","Original DB");Assert.Equal(new object?[]{1000,1000},Assert.Single(bounded.Execute(options:new(){ParentBudget=budget,MemoryLimitBytes=16384,TemporaryStorageLimitBytes=0}).Rows));Assert.Equal(0,budget.PeakTemporaryStorageBytes);Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public void UnionCommonTypeBoundaries(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());
        var query=engine.Prepare("SELECT CAST(0.00000000000000000001 AS decimal(38,30)) AS Value UNION ALL SELECT CAST(10000000000000000000 AS decimal(38,10))","Original DB");Assert.Equal((byte)38,query.Columns[0].Type.Precision);Assert.Equal((byte)10,query.Columns[0].Type.Scale);Assert.Equal(new[]{0m,10000000000000000000m},query.Execute().Rows.Select(row=>Assert.IsType<decimal>(row[0])));
        var nulls=engine.Prepare("SELECT NULL AS Value UNION ALL SELECT 'long'","Original DB");Assert.Equal("varchar",nulls.Columns[0].Type.Name);Assert.Equal(4,nulls.Columns[0].Type.Length);Assert.Equal(new object?[]{null,"long"},nulls.Execute().Rows.Select(row=>row[0]));
        Assert.Equal(new object?[]{2},Assert.Single(engine.Prepare("SELECT Id FROM dbo.Strings WHERE 'a' IN(Ci,NULL)","Original DB").Execute().Rows));
        var binary=engine.Prepare("SELECT CAST(0x01 AS varbinary(1)) AS Value UNION ALL SELECT CAST(0x0203 AS varbinary(2))","Original DB");Assert.Equal(2,binary.Columns[0].Type.Length);var rows=binary.Execute().Rows.ToArray();Assert.Equal(new byte[]{1},rows[0][0]);Assert.Equal(new byte[]{2,3},rows[1][0]);
    }
    [Fact]public void CharacterCasts(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());
        foreach(var type in new[]{"char(8001)","nchar(4001)","binary(2147483647)","char(MAX)","varchar(0)","decimal(39,1)","decimal(5,6)"}){Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT CAST('x' AS "+type+")","Original DB"));}
        Assert.Equal(new object?[]{"1","0","1","0"},Assert.Single(engine.Prepare("SELECT CAST(CAST(1 AS bit) AS varchar(5)),CAST(CAST(0 AS bit) AS varchar(5)),CAST(CAST(1 AS bit) AS nvarchar(5)),CAST(CAST(0 AS bit) AS nvarchar(5))","Original DB").Execute().Rows));
        Assert.Equal(8000,Assert.IsType<string>(Assert.Single(engine.Prepare("SELECT CAST('x' AS char(8000))","Original DB").Execute().Rows)[0]).Length);
        Assert.Equal(new object?[]{"x   ","x   ","long","é"},Assert.Single(engine.Prepare("SELECT CAST('x' AS char(4)),CAST(N'x' AS nchar(4)),CAST('longer' AS varchar(4)),CAST(N'é' AS varchar(1))","Original DB").Execute().Rows));
        Assert.Equal(new object?[]{"x","x",1,2},Assert.Single(engine.Prepare("SELECT LEFT(CAST('x' AS char(4)),1),RTRIM(CAST('x' AS char(4))),DATALENGTH(CAST('x' AS varchar(4))),DATALENGTH(CAST(N'x' AS nvarchar(4)))","Original DB").Execute().Rows));
        var xml=new Seamless.Database.Query.SqlQueryEngine(new XmlCatalog());Assert.Equal(40515,Assert.Throws<SqlQueryException>(()=>xml.Prepare("SELECT DATALENGTH(Document) FROM dbo.XmlData","Original DB").Execute().Rows.Single()).Number);
        var captured=new Seamless.Database.Query.SqlQueryEngine(new CapturedCatalog());Assert.Equal(new object?[]{"00000007"},Assert.Single(captured.Prepare("SELECT dbo.ufnLeadingZeros(7)","AdventureWorks2016").Execute().Rows));
        var binary=Assert.Single(engine.Prepare("SELECT CAST(0x01 AS binary(2)),CAST(0x010203 AS varbinary(2))","Original DB").Execute().Rows);Assert.Equal(new byte[]{1,0},binary[0]);Assert.Equal(new byte[]{1,2},binary[1]);
        var japanese=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog("Japanese_CI_AS"));
        var parameter=japanese.Prepare("SELECT @text","Original DB",[new("@text",new("varchar",typeof(string),3))]);
        Assert.Equal(8152,Assert.Throws<SqlQueryException>(()=>parameter.Execute(new Dictionary<string,object?>{{"@text","あい"}})).Number);
        Assert.Equal(new object?[]{"あ","あ ","あ"},Assert.Single(japanese.Prepare("SELECT CAST(N'あい' AS varchar(3)),CAST(N'あ' AS char(3)),CAST(N'あ' AS nvarchar(1))","Original DB").Execute().Rows));
    }
    private sealed class SemanticCatalog(string collation="Latin1_General_100_BIN2"):ISqlCatalog{
        public long Generation=>1;
        public string?GetDatabaseCollation(string database)=>collation;
        public SqlTable?FindTable(SqlObjectName name){
            if(name.Name=="PivotRows"){return new(name,[new("GroupName",new("varchar",typeof(string),10,null,null,"SQL_Latin1_General_CP1_CI_AS"),false,name),new("Id",SqlType.Int,false,name),new("Amount",SqlType.Int,false,name)],_=>new object?[][]{["A",1,1],["a",1,2]});}
            return new(name,[new("Id",SqlType.Int,false,name),new("Ci",new("varchar",typeof(string),10,null,null,"SQL_Latin1_General_CP1_CI_AS"),false,name),new("Japanese",new("nvarchar",typeof(string),10,null,null,"Japanese_CI_AS"),false,name),new("Binary",new("varchar",typeof(string),10,null,null,"Latin1_General_100_BIN2"),false,name),new("Amount",new("decimal",typeof(decimal),null,12,2),false,name),new("Unknown",new("varchar",typeof(string),10,null,null,"Unknown_BIN2"),false,name)],_=>new object?[][]{[2,"A","ア","A",1.25m,"A"]});
        }
    }
}
