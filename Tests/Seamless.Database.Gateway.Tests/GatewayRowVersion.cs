using System.Buffers.Binary;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayRowVersion{
    [Fact,Trait("Category","Integration")]
    public async Task ImportedWatermarksSurviveColumnRemovalAndNativeUpdatesUseTheSameCounter(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var root=Path.Combine(data,"rowversion-import-"+Guid.NewGuid().ToString("N"));var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        var definition=new DatabaseCatalog("a",[new SchemaCatalog("dbo",[new TableCatalog("Imported",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"Rv",new(DatabaseTypeKind.Binary,false,MaximumLength:8,SqlTypeName:"timestamp"),false)],new("PK_Imported",[new("Id",false)]))])],"Japanese_CI_AS");
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[definition],initialDataSource:(_,generated)=>new RowVersionSource())){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(982,"a");
            void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}ulong Version(string sql)=>BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(Assert.Single(engine.Execute(sql,session).Rows)[0]));
            Assert.Equal(65UL,Version("SELECT @@DBTS"));Execute("ALTER TABLE dbo.Imported DROP COLUMN Rv");Assert.Equal(65UL,Version("SELECT @@DBTS"));Execute("CREATE TABLE dbo.NativeRows(Id int PRIMARY KEY,Rv rowversion); INSERT dbo.NativeRows(Id)VALUES(1)");Assert.Equal(66UL,Version("SELECT Rv FROM dbo.NativeRows"));
            var database=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database;var types=database.GetType().GetProperty("dbo")!.GetValue(database)!.GetType().GetProperty("NativeRows")!.PropertyType.GetGenericArguments();
            var code="using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+database.GetType().FullName+")context.Database;var value=MemoryPackSerializer.Deserialize<int>(args.Span);if(id==\"insert\"){db.dbo.NativeRows.Add(new global::"+types[1].FullName!.Replace('+','.')+"(value,null!));}else{db.dbo.NativeRows.Replace(new global::"+types[0].FullName!.Replace('+','.')+"(value),new global::"+types[1].FullName!.Replace('+','.')+"(value,null!));}yield return MemoryPackSerializer.Serialize(value);}}";
            await host.Transactions.InstallNativeQueryAsync(new("rowversion","Query",[new("Query.cs",code)],[new("insert","a","","System.Int32","System.Int32",false),new("update","a","","System.Int32","System.Int32",false)],["a"]));
            using var server=new NativeQueryServer(host.Databases,0);server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var fingerprint=host.Databases.Resolve("a").SchemaFingerprint!;
            await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){await foreach(var value in transaction.QueryAsync<int,int>("rowversion","update",fingerprint,1,timeout.Token)){}await transaction.RollbackAsync(timeout.Token);}Assert.Equal(66UL,Version("SELECT Rv FROM dbo.NativeRows"));Assert.Equal(67UL,Version("SELECT @@DBTS"));
            await foreach(var value in client.QueryAsync<int,int>("rowversion","insert","a",fingerprint,2,timeout.Token)){}Assert.Equal(68UL,Version("SELECT @@DBTS"));host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(983,"a");Assert.Equal(68UL,BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(Assert.Single(engine.Execute("SELECT @@DBTS",session).Rows)[0])));}
    }
    private sealed class RowVersionSource:IDatabaseImportSource{
        public IEnumerable<TRow> ReadTable<TRow>(global::Seamless.Database.System.Keys.DatabaseObject key){var constructor=typeof(TRow).GetConstructors().Single(static constructor=>constructor.GetParameters().Length==2);for(var id=1;id<=2;id++){var bytes=new byte[8];BinaryPrimitives.WriteUInt64BigEndian(bytes,(ulong)(63+id));yield return(TRow)constructor.Invoke([id,bytes]);}}
    }
    [Fact,Trait("Category","Integration")]
    public async Task RowVersionsAreDatabaseWideDurableAndRestoredWithRowsAtSavepoints(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var root=Path.Combine(data,"rowversion-"+Guid.NewGuid().ToString("N"));var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};ulong watermark;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(980,"a");
            void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            ulong Version(string sql)=>BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(Assert.Single(engine.Execute(sql,session).Rows)[0]));
            Execute("CREATE TABLE dbo.T(Id int NOT NULL PRIMARY KEY,N int NOT NULL,Rv rowversion); CREATE TABLE dbo.Other(Id int NOT NULL PRIMARY KEY,Rv timestamp NULL); INSERT dbo.T VALUES(1,7); INSERT dbo.Other(Id) VALUES(1)");var first=Version("SELECT Rv FROM dbo.T");var other=Version("SELECT Rv FROM dbo.Other");Assert.True(other>first);Assert.Equal(other,Version("SELECT @@DBTS"));
            Assert.Equal(new object?[]{(byte)189,(short)8,false},Assert.Single(engine.Execute("SELECT system_type_id,max_length,is_nullable FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.T') AND name=N'Rv'",session).Rows));
            Execute("BEGIN TRANSACTION; SAVE TRANSACTION before_change; UPDATE dbo.T SET N=N WHERE Id=1");var changed=Version("SELECT Rv FROM dbo.T");Assert.True(changed>other);Execute("ROLLBACK TRANSACTION before_change");Assert.Equal(first,Version("SELECT Rv FROM dbo.T"));Assert.Equal(changed,Version("SELECT @@DBTS"));Execute("COMMIT TRANSACTION");
            Assert.Equal(272,Assert.Throws<SqlQueryException>(()=>Execute("UPDATE dbo.T SET Rv=DEFAULT WHERE Id=1")).Number);Assert.Equal(273,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.T(Id,N,Rv) VALUES(3,9,0x0000000000000001)")).Number);Assert.Equal(2738,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.T ADD Another rowversion")).Number);
            Execute("INSERT dbo.T(Id,N,Rv) VALUES(2,8,DEFAULT); CREATE TABLE dbo.Added(Id int NOT NULL PRIMARY KEY); INSERT dbo.Added VALUES(1),(2); ALTER TABLE dbo.Added ADD Rv rowversion");var versions=engine.Execute("SELECT Rv FROM dbo.Added",session).Rows.Select(row=>BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(row[0]))).ToArray();Assert.Equal(2,versions.Distinct().Count());Assert.All(versions,value=>Assert.True(value>changed));
            Execute("ALTER TABLE dbo.T ADD Extra int NULL");Assert.Equal(first,Version("SELECT Rv FROM dbo.T WHERE Id=1"));watermark=Version("SELECT @@DBTS");host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(981,"a");Assert.Equal(watermark,BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(Assert.Single(engine.Execute("SELECT @@DBTS",session).Rows)[0])));foreach(var row in engine.Execute("UPDATE dbo.T SET N=N WHERE Id=1",session).Rows){}Assert.True(BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(Assert.Single(engine.Execute("SELECT Rv FROM dbo.T WHERE Id=1",session).Rows)[0]))>watermark);
        }
    }
}
