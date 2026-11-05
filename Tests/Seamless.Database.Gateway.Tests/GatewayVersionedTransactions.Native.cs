using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Fact,Trait("Category","Integration")]
    public async Task NativePublicationAndDdlUseTheSameCatalogueSnapshotAndRecoverTogether(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var path=Path.Combine(data,"mvcc-native-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options(),[Definition("a"),Definition("b")])){
            var engine=new SqlExecutionEngine(host.Databases);using var seed=new SessionContext(40,"b");Execute(engine,seed,"INSERT a.dbo.Items VALUES(1,100); INSERT b.dbo.Items VALUES(1,200)");var type=((InProcessDatabaseAdapter)host.Databases.Resolve("b").Adapter).Database.GetType().FullName!;
            NativeQuerySourceProject Source(int offset)=>new("snapshot-module","Query",[new("Query.cs","using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+type+")context.Database;var sum="+offset+";foreach(var row in db.dbo.Items.GetGatewayTransactionRows()){sum=checked(sum+row.N);}yield return MemoryPackSerializer.Serialize(sum);}}")],[new("n","b","","System.Int32","System.Int32")],["b"]);
            await host.Transactions.InstallNativeQueryAsync(Source(0));using var old=new SessionContext(41,"a");Execute(engine,old,"BEGIN TRANSACTION");Assert.Equal(100,Assert.Single(engine.Execute("SELECT N FROM dbo.Items",old).Rows)[0]);await host.Transactions.InstallNativeQueryAsync(Source(10));Assert.Equal(200,ReadNative(host,old));using var current=new SessionContext(42,"b");Assert.Equal(210,ReadNative(host,current));Execute(engine,old,"ROLLBACK TRANSACTION");Execute(engine,current,"ALTER TABLE dbo.Items ADD Extra int NULL");Assert.Equal(210,ReadNative(host,current));host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options())){using var session=new SessionContext(43,"b");Assert.Equal(210,ReadNative(host,session));}
    }
    private static int ReadNative(GatewayDatabaseHost host,SessionContext session)=>session.Run(()=>{
        using var binding=DatabaseRegistry.EnterSqlSession(session);using var reads=host.Transactions.AcquireNativeQueryReads(session,"snapshot-module",true);Assert.True(host.Transactions.TryResolveNative(session,"snapshot-module",out _,out var module));var database=host.Transactions.NativeDatabase(session,"snapshot-module","b",true);using var iterator=module.Execute("n",ReadOnlyMemory<byte>.Empty,new NativeContext(database)).GetEnumerator();Assert.True(host.Transactions.ExecuteNativeOperation(session,iterator.MoveNext));var value=MemoryPackSerializer.Deserialize<int>(iterator.Current);Assert.False(host.Transactions.ExecuteNativeOperation(session,iterator.MoveNext));return value;
    });
    private sealed class NativeContext(object database):INativeQueryContext{public object Database=>database;public string DatabaseName=>"b";public CancellationToken CancellationToken=>default;}
}
