using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayNativeResources{
    [Fact,Trait("Category","Integration")]
    public async Task NativeResultSpoolSharesTheGatewayBudgetAndCancelsItsMutationWhenBothQuotasAreExhausted(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var root=Path.Combine(data,"native-resources-"+Guid.NewGuid().ToString("N"));var budget=new ResourceBudget(128L<<20,256L<<20);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")],budget:budget)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(990,"a");foreach(var row in engine.Execute("CREATE TABLE dbo.Rows(Id int PRIMARY KEY)",session).Rows){}
            var database=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database;var rowType=database.GetType().GetProperty("dbo")!.GetValue(database)!.GetType().GetProperty("Rows")!.PropertyType.GetGenericArguments()[1].FullName!.Replace('+','.');
            var code="using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+database.GetType().FullName+")context.Database;db.dbo.Rows.Add(new global::"+rowType+"(new(1)));yield return MemoryPackSerializer.Serialize(new byte[4<<20]);}}";
            await host.Transactions.InstallNativeQueryAsync(new("resources","Query",[new("Query.cs",code)],[new("write","a","","System.Int32","System.Byte[]",false)],["a"]));
            using var server=new NativeQueryServer(host.Databases,0);server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){
                using(var memory=budget.ReserveMemory(budget.MemoryLimitBytes-budget.MemoryBytes-(1<<20)))using(var storage=budget.ReserveTemporaryStorage(budget.TemporaryStorageLimitBytes-budget.TemporaryStorageBytes-65536)){
                    var error=await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var row in transaction.QueryAsync<int,byte[]>("resources","write",host.Databases.Resolve("a").SchemaFingerprint!,0,timeout.Token)){}});Assert.Contains("Resource limit",error.Message,StringComparison.OrdinalIgnoreCase);
                }
                if(!transaction.IsCompleted){try{await transaction.RollbackAsync(timeout.Token);}catch(NativeQueryException){}}
            }
            using var completed=new SessionContext(991,"a"){CancellationToken=timeout.Token};Assert.Empty(engine.Execute("SELECT Id FROM dbo.Rows",completed).Rows);
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
}
