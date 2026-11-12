using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayConstraintForms{
    [Fact,Trait("Category","Integration")]
    public async Task OmittedReferencedColumnsUseThePrimaryKeyAndExistingDefaultWithValuesDoesNotRewriteNulls(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var root=Path.Combine(data,"constraint-forms-"+Guid.NewGuid().ToString("N"));var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(970,"a");
            void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("CREATE TABLE dbo.Parent(Id int NOT NULL PRIMARY KEY); CREATE TABLE dbo.Child(Id int NOT NULL PRIMARY KEY,Pid int NULL,Value int NULL); INSERT dbo.Parent VALUES(1); INSERT dbo.Child VALUES(1,1,NULL); ALTER TABLE dbo.Child ADD CONSTRAINT FK_Child FOREIGN KEY(Pid) REFERENCES dbo.Parent");
            Execute("ALTER TABLE dbo.Child ADD CONSTRAINT DF_Child_Value DEFAULT(7) FOR Value WITH VALUES; INSERT dbo.Child(Id,Pid) VALUES(2,1)");
            Assert.Equal(new object?[]{null,7},engine.Execute("SELECT Value FROM dbo.Child ORDER BY Id",session).Rows.Select(static row=>row[0]));
            Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Child(Id,Pid) VALUES(3,99)")).Number);
            Execute("CREATE TABLE dbo.Tree(Id int PRIMARY KEY,Pid int NULL REFERENCES dbo.Tree); INSERT dbo.Tree VALUES(1,NULL),(2,1)");
            Execute("CREATE TABLE dbo.Checked(Id int PRIMARY KEY,N int CONSTRAINT CK_N CHECK(N>0),Unused int NULL); INSERT dbo.Checked VALUES(1,7,8); ALTER TABLE dbo.Checked DROP COLUMN Unused");Assert.Equal(7,Assert.Single(engine.Execute("SELECT N FROM dbo.Checked",session).Rows)[0]);Assert.Equal(5074,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Checked DROP COLUMN N")).Number);
            Execute("BEGIN TRANSACTION; SAVE TRANSACTION constraints; ALTER TABLE dbo.Child DROP CONSTRAINT FK_Child; INSERT dbo.Child(Id,Pid) VALUES(3,99); ROLLBACK TRANSACTION constraints; COMMIT TRANSACTION");
            Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Child(Id,Pid) VALUES(3,99)")).Number);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(971,"a");Assert.Equal(new object?[]{null,7},engine.Execute("SELECT Value FROM dbo.Child ORDER BY Id",session).Rows.Select(static row=>row[0]));Assert.Equal(2,engine.Execute("SELECT Id FROM dbo.Tree",session).Rows.Count());
        }
    }
}
