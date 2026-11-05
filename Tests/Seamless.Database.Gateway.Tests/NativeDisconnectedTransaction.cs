using System.Net.Sockets;
using System.Reflection;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Tests;

public sealed class NativeDisconnectedTransaction{
    private const string Fingerprint="disconnected-transaction-v1";
    [Fact,Trait("Category","Integration")]
    public async Task AbruptDisconnectRollsBackAndReleasesTheKey(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root)||!(root.StartsWith("D:\\",StringComparison.OrdinalIgnoreCase)||root.StartsWith("F:\\",StringComparison.OrdinalIgnoreCase))){throw new InvalidOperationException("An absolute D: or F: test root is required.");}
        var path=Path.Combine(root,"native-disconnect-"+Guid.NewGuid().ToString("N"));
        var options=DatabaseContainerOptions.OnStorage(path) with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"scratch")};
        using var database=new RuntimeSqlGeneratedRoot(options);var registry=new DatabaseRegistry();registry.Register("fixture",new InProcessDatabaseAdapter(database),schemaFingerprint:Fingerprint);using var module=new Module();
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var definitions=new[]{new NativeQueryDefinition("insert","fixture",Fingerprint,"int","int",false),new NativeQueryDefinition("exists","fixture",Fingerprint,"int","int"){ArgumentsContract=NativeWireContract.For<int>(),ResultContract=NativeWireContract.For<int>()}};
        using var server=new NativeQueryServer(registry,0);server.Register(new NativeModuleManifest("disconnect","","","",version,definitions),module,version);server.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using(var socket=new TcpClient()){
            await socket.ConnectAsync("127.0.0.1",server.Port,timeout.Token);var stream=socket.GetStream();var transaction=Guid.NewGuid();
            async Task Send(NativeQueryRequest request,bool row){
                await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(request),timeout.Token);
                if(row){var value=await NativeFrame.ReadAsync(stream,timeout.Token)??throw new EndOfStreamException();Assert.Equal(NativeFrame.Row,value.Kind);Assert.Equal(1,MemoryPackSerializer.Deserialize<int>(value.Payload));}
                var completed=await NativeFrame.ReadAsync(stream,timeout.Token)??throw new EndOfStreamException();Assert.Equal(NativeFrame.Completed,completed.Kind);
            }
            await Send(new("$transaction","begin","fixture","",[],transaction),false);
            await Send(new("disconnect","insert","fixture",Fingerprint,MemoryPackSerializer.Serialize(7),transaction,NativeWireContract.For<int>(),NativeWireContract.For<int>()),true);
            // COMMIT/ROLLBACKを送らずに通信だけを切る。クライアントのDisposeによる明示取消は使わない。
        }
        await module.Disconnected.Task.WaitAsync(timeout.Token);
        // 接続処理の終了を待ってから同じDBで再接続し、取引資源の回収まで確認する。
        await server.StopAsync();server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);
        Assert.Equal(0,await client.ExecuteScalarAsync<int,int>("disconnect","exists","fixture",Fingerprint,7,timeout.Token));
        await using(var transaction=await client.BeginTransactionAsync("fixture",timeout.Token)){
            var rows=new List<int>();await foreach(var value in transaction.QueryAsync<int,int>("disconnect","insert",Fingerprint,7,timeout.Token)){rows.Add(value);}Assert.Equal(1,Assert.Single(rows));await transaction.CommitAsync(timeout.Token);
        }
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("disconnect","exists","fixture",Fingerprint,7,timeout.Token));
    }
    private sealed class Module:INativeQueryModule,IDisposable{
        private CancellationTokenRegistration registration;
        private bool observing;
        public TaskCompletionSource Disconnected{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            var database=(RuntimeSqlGeneratedRoot)context.Database;var id=MemoryPackSerializer.Deserialize<int>(arguments.Span);
            if(queryId=="insert"){
                if(!database.TypedOrders.Add(new(id,"created",1))){throw new InvalidOperationException("The disconnected transaction did not release its key.");}
                if(!this.observing){this.observing=true;this.registration=context.CancellationToken.Register(()=>this.Disconnected.TrySetResult());}yield return MemoryPackSerializer.Serialize(1);yield break;
            }
            if(queryId!="exists"){throw new InvalidOperationException("Unknown test query.");}
            var exists=0;if(database.TypedOrders.TryGetValue(new(id),out _)){exists=1;}yield return MemoryPackSerializer.Serialize(exists);
        }
        public void Dispose()=>this.registration.Dispose();
    }
}
