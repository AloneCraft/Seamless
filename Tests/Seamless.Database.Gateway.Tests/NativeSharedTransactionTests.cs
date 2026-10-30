using System.Reflection;
using MemoryPack;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Tests;

public sealed class NativeSharedTransactionTests{
    private const string Fingerprint="native-shared-transaction-test";
    private sealed class Module:INativeQueryModule{
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            var database=(RuntimeSqlGeneratedRoot)context.Database;
            var id=MemoryPackSerializer.Deserialize<int>(arguments.Span);
            var key=new RuntimeTypedKey(id);
            switch(queryId){
                case "insert":
                    if(!database.TypedOrders.Add(new(id,"created",1))){throw new InvalidOperationException("Duplicate test row.");}
                    yield return MemoryPackSerializer.Serialize(1);
                    yield break;
                case "update":
                    if(!database.TypedOrders.TryGetValue(key,out var existing)){throw new InvalidOperationException("Test row is missing.");}
                    database.TypedOrders[key]=existing with{Name="updated",Version=2};
                    yield return MemoryPackSerializer.Serialize(1);
                    yield break;
                case "delete":
                    if(!database.TypedOrders.Remove(key)){throw new InvalidOperationException("Test row is missing.");}
                    yield return MemoryPackSerializer.Serialize(1);
                    yield break;
                case "read":
                    if(database.TypedOrders.TryGetValue(key,out var row)){yield return MemoryPackSerializer.Serialize(row.Name);}
                    yield break;
                default:throw new InvalidOperationException("Unexpected test query.");
            }
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task RuntimeSqlNativeCommitRollbackAndDelete(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root)||!(root.StartsWith("D:\\",StringComparison.OrdinalIgnoreCase)||root.StartsWith("F:\\",StringComparison.OrdinalIgnoreCase))){throw new InvalidOperationException("An absolute D: or F: test root is required.");}
        var path=Path.Combine(root,"native-shared-"+Guid.NewGuid().ToString("N"));
        var options=DatabaseContainerOptions.OnStorage(path) with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"scratch")};
        using var database=new RuntimeSqlGeneratedRoot(options);
        var registry=new DatabaseRegistry();registry.Register("fixture",new InProcessDatabaseAdapter(database),schemaFingerprint:Fingerprint);
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var definitions=new[]{
            new NativeQueryDefinition("insert","fixture",Fingerprint,"int","int",false),
            new NativeQueryDefinition("update","fixture",Fingerprint,"int","int",false),
            new NativeQueryDefinition("delete","fixture",Fingerprint,"int","int",false),
            new NativeQueryDefinition("read","fixture",Fingerprint,"int","string")
        };
        using var server=new NativeQueryServer(registry,35483);server.Register(new NativeModuleManifest("test","","","",version,definitions),new Module(),version);server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client=new NativeQueryClient("127.0.0.1",35483);
        async Task<int> Mutate(NativeTransaction transaction,string operation){
            var rows=new List<int>();await foreach(var value in transaction.QueryAsync<int,int>("test",operation,Fingerprint,7,timeout.Token)){rows.Add(value);}return Assert.Single(rows);
        }
        async Task<string[]> Read(){
            var rows=new List<string>();await foreach(var value in client.QueryAsync<int,string>("test","read","fixture",Fingerprint,7,timeout.Token)){rows.Add(value);}return rows.ToArray();
        }
        Assert.Empty(await Read());
        await using(var tx=await client.BeginTransactionAsync("fixture",timeout.Token)){Assert.Equal(1,await Mutate(tx,"insert"));await tx.CommitAsync(timeout.Token);}
        Assert.Equal(new[]{"created"},await Read());
        await using(var tx=await client.BeginTransactionAsync("fixture",timeout.Token)){Assert.Equal(1,await Mutate(tx,"update"));await tx.RollbackAsync(timeout.Token);}
        Assert.Equal(new[]{"created"},await Read());
        await using(var tx=await client.BeginTransactionAsync("fixture",timeout.Token)){Assert.Equal(1,await Mutate(tx,"delete"));await tx.CommitAsync(timeout.Token);}
        Assert.Empty(await Read());
    }
}
