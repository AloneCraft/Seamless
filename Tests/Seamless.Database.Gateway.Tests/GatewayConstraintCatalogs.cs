using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayConstraintCatalogs{
    [Fact,Trait("Category","Integration")]
    public async Task GeneratedConstraintNamesAndInlineCheckOwnersSurviveGenerationAndReopen(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"constraint-names-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        static void Verify(GatewayDatabaseHost host){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(1000,"a");
            Assert.All(engine.Execute("SELECT is_system_named FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GeneratedKeys')",session).Rows,row=>Assert.Equal(true,row[0]));
            Assert.Equal(new object?[]{3,true},Assert.Single(engine.Execute("SELECT parent_column_id,is_system_named FROM sys.check_constraints WHERE name=N'CK_GeneratedKeys_0'",session).Rows));
            Assert.Equal(new object?[]{4,false},Assert.Single(engine.Execute("SELECT parent_column_id,is_system_named FROM sys.check_constraints WHERE name=N'CK_Explicit'",session).Rows));
            Assert.Equal(true,Assert.Single(engine.Execute("SELECT is_system_named FROM sys.default_constraints WHERE name=N'DF_GeneratedKeys_N'",session).Rows)[0]);
            Assert.Equal(false,Assert.Single(engine.Execute("SELECT is_system_named FROM sys.default_constraints WHERE name=N'DF_Explicit'",session).Rows)[0]);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(1001,"a");foreach(var result in engine.ExecuteBatch("CREATE TABLE dbo.GeneratedKeys(Id int PRIMARY KEY,V int UNIQUE,N int CHECK(N>0) DEFAULT 4,Other int CONSTRAINT CK_Explicit CHECK(Other>0) CONSTRAINT DF_Explicit DEFAULT 7)",session)){foreach(var row in result.Rows){}}
            Verify(host);foreach(var result in engine.ExecuteBatch("ALTER TABLE dbo.GeneratedKeys ADD Extra int",session)){foreach(var row in result.Rows){}}Verify(host);host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){Verify(host);}
    }
    [Fact,Trait("Category","Integration")]
    public async Task ConstraintObjectsIndexesAndForeignColumnsDescribeTheSamePersistentSchema(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"constraint-catalogs-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        var parent=new TableCatalog("Parent",[new(0,"Id",number,false),new(1,"Unused",number with{IsNullable=true},false),new(2,"Code",number,false)],new("PK_Parent",[new("Id",false)],1),[new("UQ_Code",[new("Code",false)])]);
        var child=new TableCatalog("Child",[new(0,"Id",number,false),new ColumnCatalog(1,"PCode",number with{IsNullable=true},false,"(0)"){DefaultConstraintName="DF_PCode"}],new("PK_Child",[new("Id",false)],1),foreignKeys:[new("FK_Code","dbo","Parent",[("PCode","Code")],"SET_NULL","CASCADE")],checkConstraints:[new("CK_Id","Id>0",false)]);
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};int foreignId;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[parent,child])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(998,"a");
            object?[] Row(string sql)=>Assert.Single(engine.Execute(sql,session).Rows);
            Assert.Equal(new object?[]{"CK_Id","Id>0",false,false},Row("SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints"));Assert.Equal(new object?[]{"DF_PCode",2,"(0)"},Row("SELECT name,parent_column_id,definition FROM sys.default_constraints"));
            Assert.Equal(new object?[]{"FK_Code",2,3,"UQ_Code",true,"UQ_Code"},Row("SELECT f.name,c.parent_column_id,c.referenced_column_id,i.name,i.is_unique,k.name FROM sys.foreign_keys f INNER JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id INNER JOIN sys.indexes i ON i.object_id=f.referenced_object_id AND i.index_id=f.key_index_id INNER JOIN sys.key_constraints k ON k.parent_object_id=i.object_id AND k.unique_index_id=i.index_id"));
            var links=Row("SELECT f.object_id,OBJECT_ID(N'dbo.FK_Code'),o.parent_object_id,f.parent_object_id FROM sys.foreign_keys f INNER JOIN sys.objects o ON o.object_id=f.object_id");Assert.Equal(links[0],links[1]);Assert.Equal(links[2],links[3]);foreignId=Assert.IsType<int>(links[0]);
            foreach(var result in engine.ExecuteBatch("ALTER TABLE dbo.Parent DROP COLUMN Unused",session)){foreach(var row in result.Rows){}}
            Assert.Equal(3,Row("SELECT referenced_column_id FROM sys.foreign_key_columns")[0]);Assert.Equal(foreignId,Row("SELECT object_id FROM sys.foreign_keys")[0]);host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(999,"a");Assert.Equal(foreignId,Assert.Single(engine.Execute("SELECT object_id FROM sys.foreign_keys",session).Rows)[0]);}
    }
}
