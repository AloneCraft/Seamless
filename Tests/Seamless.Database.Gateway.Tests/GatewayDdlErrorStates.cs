using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayDdlErrorStates(Xunit.Abstractions.ITestOutputHelper output){
    [Theory,Trait("Category","Integration")]
    [InlineData("DROP TABLE dbo.BoundTable",3729,3729,false)]
    [InlineData("ALTER TABLE dbo.BoundTable ALTER COLUMN N bigint NOT NULL",5074,4922,true)]
    [InlineData("ALTER TABLE dbo.Items ALTER COLUMN N int NOT NULL",515,515,true)]
    [InlineData("ALTER TABLE dbo.Items ADD CONSTRAINT CK_Positive CHECK(Id>10)",547,547,true)]
    [InlineData("ALTER TABLE dbo.Items ADD Required int NOT NULL",4901,4901,true)]
    [InlineData("ALTER TABLE dbo.Items ALTER COLUMN N tinyint NULL",220,220,true)]
    public async Task DdlErrorsFollowSqlServerTransactionStateInsideAndOutsideCatch(string ddl,int outsideNumber,int caughtNumber,bool doomed){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"ddl-error-state-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var id=new ColumnCatalog(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false);var number=new ColumnCatalog(1,"N",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false);var view=new ViewCatalog("Bound",[number with{Ordinal=0}],null,"CREATE VIEW dbo.Bound WITH SCHEMABINDING AS SELECT N FROM dbo.BoundTable");
        using var host=await GatewayDatabaseHost.OpenAsync(root,compiler,new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},[new("a",[new SchemaCatalog("dbo",[new TableCatalog("BoundTable",[id,number],new("PK_Bound",[new("Id",false)])),new TableCatalog("Items",[id,number with{Type=number.Type with{IsNullable=true}}],new("PK_Items",[new("Id",false)]))],[view])],"Japanese_CI_AS")]);var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(997,"a");
        void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
        Execute("INSERT dbo.Items VALUES(1,300),(2,NULL); INSERT dbo.BoundTable VALUES(1,7); BEGIN TRANSACTION; UPDATE dbo.Items SET N=301 WHERE Id=1");Assert.Equal(outsideNumber,Assert.Throws<SqlQueryException>(()=>Execute(ddl)).Number);Assert.Equal(0,session.TransactionCount);Assert.Equal(0,session.TransactionState);Assert.Equal(300,Assert.Single(engine.Execute("SELECT N FROM dbo.Items WHERE Id=1",session).Rows)[0]);
        foreach(var xactAbort in new[]{false,true}){
            if(xactAbort){Execute("SET XACT_ABORT ON");}else{Execute("SET XACT_ABORT OFF");}
            var results=engine.ExecuteBatch("BEGIN TRANSACTION; UPDATE dbo.Items SET N=301 WHERE Id=1; BEGIN TRY "+ddl+"; END TRY BEGIN CATCH SELECT ERROR_NUMBER(),@@TRANCOUNT,XACT_STATE(); ROLLBACK TRANSACTION; END CATCH",session);
            var captured=results.Select(result=>new{Columns=result.Columns.Select(static column=>column.Name).ToArray(),Rows=result.Rows.ToArray()}).ToArray();output.WriteLine(global::System.Text.Json.JsonSerializer.Serialize(new{XactAbort=xactAbort,Results=captured,Count=session.TransactionCount,State=session.TransactionState}));
            var rows=captured.SelectMany(static result=>result.Rows).ToArray();short expectedState=1;if(doomed||xactAbort){expectedState=-1;}
            Assert.Equal(new object?[]{caughtNumber,1,expectedState},Assert.Single(rows));Assert.Equal(0,session.TransactionCount);
            Assert.Equal(300,Assert.Single(engine.Execute("SELECT N FROM dbo.Items WHERE Id=1",session).Rows)[0]);Assert.Equal(0,Assert.Single(engine.Execute("SELECT COUNT(*) FROM sys.check_constraints WHERE name=N'CK_Positive'",session).Rows)[0]);
        }
    }
}
