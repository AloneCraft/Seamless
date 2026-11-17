using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayViewDependencies{
    [Fact,Trait("Category","Integration")]
    public async Task ImportedSchemaBoundViewsProtectTheirTablesAndColumnsAcrossGenerations(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var root=Path.Combine(data,"view-dependencies-"+Guid.NewGuid().ToString("N"));var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        var id=new ColumnCatalog(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false);var number=new ColumnCatalog(1,"N",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false);var table=new TableCatalog("T",[id,number],new("PK_T",[new("Id",false)]));var view=new ViewCatalog("Bound",[number with{Ordinal=0}],null,"CREATE VIEW dbo.Bound WITH SCHEMABINDING AS SELECT N FROM dbo.T");
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[table],[view])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(995,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("INSERT dbo.T(Id,N) VALUES(1,7)");Assert.Equal(3729,Assert.Throws<SqlQueryException>(()=>Execute("DROP TABLE dbo.T")).Number);Assert.Equal(5074,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.T ALTER COLUMN N bigint NOT NULL")).Number);Assert.Equal(5074,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.T DROP COLUMN N")).Number);
            Execute("ALTER TABLE dbo.T ADD Extra int NULL");Assert.Equal(7,Assert.Single(engine.Execute("SELECT N FROM dbo.Bound",session).Rows)[0]);host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(996,"a");Assert.Equal(7,Assert.Single(engine.Execute("SELECT N FROM dbo.Bound",session).Rows)[0]);}
    }
}
