using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewaySqlServerTypeComparisonTests{
    private sealed record TypeCase(string Type,string Minimum,string Maximum);
    [SqlServerPrintFact,Trait("Category","Integration")]
    public async Task ImportedConstraintNamesAndInlineOwnersMatchSqlServer(){
        var source=SqlServerPrintFactAttribute.SourceConnection();using var connection=new SqlConnection(source.ConnectionString);await connection.OpenAsync();var name="GatewayCatalog_"+Guid.NewGuid().ToString("N");using var command=connection.CreateCommand();
        try{
            command.CommandText="CREATE TABLE dbo.["+name+"](Id int PRIMARY KEY,V int UNIQUE,N int CHECK(N>0) DEFAULT 4,Parent int NULL REFERENCES dbo.["+name+"](Id))";await command.ExecuteNonQueryAsync();
            var catalog=await new SqlServerCatalogReader(source.ConnectionString,false).ReadAsync();var table=catalog.Schemas.Single(schema=>schema.Name=="dbo").Tables.Single(table=>table.Name==name);
            Assert.True(table.PrimaryKey!.IsSystemNamed);Assert.True(Assert.Single(table.UniqueKeys).IsSystemNamed);Assert.True(Assert.Single(table.ForeignKeys).IsSystemNamed);var check=Assert.Single(table.CheckConstraints);Assert.True(check.IsSystemNamed);Assert.Equal("N",check.ParentColumn);Assert.True(table.Columns.Single(column=>column.Name=="N").IsDefaultSystemNamed);
        }finally{command.CommandText="DROP TABLE IF EXISTS dbo.["+name+"]";await command.ExecuteNonQueryAsync();}
    }
    [SqlServerPrintFact,Trait("Category","Integration")]
    public async Task StoredBoundaryValuesNullsAndWireMetadataMatchSqlServer(){
        TypeCase[] cases=[
            new("bit","0","1"),new("tinyint","0","255"),new("smallint","-32768","32767"),new("int","-2147483648","2147483647"),new("bigint","-9223372036854775808","9223372036854775807"),
            new("decimal(28,0)","-9999999999999999999999999999","9999999999999999999999999999"),new("numeric(28,12)","-9999999999999999.999999999999","9999999999999999.999999999999"),
            new("money","-922337203685477.5808","922337203685477.5807"),new("smallmoney","-214748.3648","214748.3647"),
            new("real","CAST('-3.4028234663852886E+38' AS real)","CAST('3.4028234663852886E+38' AS real)"),new("float","CAST('-1.7976931348623157E+308' AS float)","CAST('1.7976931348623157E+308' AS float)"),
            new("char(8) COLLATE Latin1_General_100_CI_AS","'a'","'12345678'"),new("varchar(8) COLLATE Latin1_General_100_CI_AS","''","N'éééééééé'"),new("varchar(8) COLLATE Japanese_CI_AS","''","N'日本日本'"),
            new("nchar(4) COLLATE Japanese_CI_AS","N'a'","N'日本語文'"),new("nvarchar(4) COLLATE Japanese_CI_AS","N''","N'日本語文'"),new("varchar(max)","''","'variable'"),new("nvarchar(max)","N''","N'日本語'"),new("text","''","'legacy'"),new("ntext","N''","N'日本語'"),
            new("binary(4)","0x00","0xFFFFFFFF"),new("varbinary(4)","0x","0xFFFFFFFF"),new("varbinary(max)","0x","0x01020304"),new("image","0x","0x01020304"),
            new("uniqueidentifier","'00000000-0000-0000-0000-000000000000'","'ffffffff-ffff-ffff-ffff-ffffffffffff'"),new("xml","N'<a />'","N'<root xml:space=\"preserve\"> </root><b><![CDATA[x<y]]></b>'"),
            new("date","'0001-01-01'","'9999-12-31'"),new("time(7)","'00:00:00.0000000'","'23:59:59.9999999'"),new("time(0)","'00:00:00.0000000'","CAST('23:59:59.9999999' AS time(0))"),
            new("datetime","'1753-01-01T00:00:00.000'","'9999-12-31T23:59:59.997'"),new("smalldatetime","'1900-01-01T00:00:00'","'2079-06-06T23:59:00'"),new("datetime2(7)","'0001-01-01T00:00:00.0000000'","'9999-12-31T23:59:59.9999999'"),new("datetime2(0)","'0001-01-01T00:00:00'","'9999-12-31T23:59:59'"),new("datetimeoffset(7)","'0001-01-01T00:00:00.0000000+00:00'","'9999-12-31T23:59:59.9999999+00:00'")];
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"sql-type-comparison-"+Guid.NewGuid().ToString("N")),compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);
        using var server=new TdsServer(0,host.Databases.Resolve("a").Adapter,new SqlExecutionEngine(host.Databases));server.Start();
        var target=new SqlConnectionStringBuilder{DataSource="127.0.0.1,"+server.Port,InitialCatalog="a",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
        var source=SqlServerPrintFactAttribute.SourceConnection();
        using var expectedConnection=new SqlConnection(source.ConnectionString);using var actualConnection=new SqlConnection(target.ConnectionString);await expectedConnection.OpenAsync();await actualConnection.OpenAsync();
        static async Task Execute(SqlConnection connection,string sql){using var command=connection.CreateCommand();command.CommandText=sql;command.CommandTimeout=120;await command.ExecuteNonQueryAsync();}
        var columns="Id int NOT NULL PRIMARY KEY,"+string.Join(',',cases.Select((item,index)=>"C"+index+" "+item.Type+" NULL"));
        var rows="(0,"+string.Join(',',cases.Select(static item=>item.Minimum))+"),(1,"+string.Join(',',cases.Select(static item=>item.Maximum))+"),(2,"+string.Join(',',cases.Select(static _=>"NULL"))+")";
        await Execute(expectedConnection,"CREATE TABLE #GatewayTypeComparison("+columns+"); INSERT #GatewayTypeComparison VALUES"+rows);
        await Execute(actualConnection,"CREATE TABLE dbo.TypeComparison("+columns+"); INSERT dbo.TypeComparison VALUES"+rows);
        async Task Verify(){
            using var expectedCommand=expectedConnection.CreateCommand();using var actualCommand=actualConnection.CreateCommand();expectedCommand.CommandText="SELECT * FROM #GatewayTypeComparison ORDER BY Id";actualCommand.CommandText="SELECT * FROM dbo.TypeComparison ORDER BY Id";
            using var expected=await expectedCommand.ExecuteReaderAsync();using var actual=await actualCommand.ExecuteReaderAsync();var expectedColumns=expected.GetColumnSchema();var actualColumns=actual.GetColumnSchema();Assert.Equal(expected.FieldCount,actual.FieldCount);
            for(var index=0;index<expected.FieldCount;index++){var left=expectedColumns[index];var right=actualColumns[index];Assert.Equal((left.ColumnName,left.DataTypeName,left.ColumnSize,left.NumericPrecision,left.NumericScale,left.AllowDBNull),(right.ColumnName,right.DataTypeName,right.ColumnSize,right.NumericPrecision,right.NumericScale,right.AllowDBNull));}
            var count=0;while(await expected.ReadAsync()){Assert.True(await actual.ReadAsync());for(var index=0;index<expected.FieldCount;index++){var left=expected.GetValue(index);var right=actual.GetValue(index);if(left is byte[] bytes){Assert.Equal(bytes,Assert.IsType<byte[]>(right));}else{Assert.Equal(left,right);}}count++;}Assert.Equal(3,count);Assert.False(await actual.ReadAsync());
        }
        await Verify();await Execute(actualConnection,"ALTER TABLE dbo.TypeComparison ADD Generation int NULL");await Execute(actualConnection,"ALTER TABLE dbo.TypeComparison DROP COLUMN Generation");await Verify();
        await Execute(expectedConnection,"UPDATE #GatewayTypeComparison SET C15=N'case' WHERE C11='A'");await Execute(actualConnection,"UPDATE dbo.TypeComparison SET C15=N'case' WHERE C11='A'");await Verify();
    }
}
