using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Client.Results;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Tds;
using Seamless.Execution;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tests;

public sealed class NativeTransport{
    [Fact,Trait("Category","Integration")]
    public async Task PartialEnumerationWaitsForTheCancellationBarrierAndKeepsTheConnection(){
        using var fixture=new Fixture(maximumConcurrentRequests:1);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        Assert.Equal(new byte[]{1},await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[1],timeout.Token));
        var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=field.GetValue(client);Assert.NotNull(transport);
        for(var index=0;index<20;index++){
            await using(var iterator=client.QueryAsync<byte[],byte[]>("wire","echo","fixture","wire",[(byte)index],timeout.Token).GetAsyncEnumerator()){
                Assert.True(await iterator.MoveNextAsync());Assert.Equal(new byte[]{(byte)index},iterator.Current);
            }
            Assert.Same(transport,field.GetValue(client));
            Assert.Equal(new byte[]{99},await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[99],timeout.Token));
        }
    }
    [Fact,Trait("Category","Integration")]
    public async Task CancellationOfAnExecutingQueryWaitsForCleanupAndReusesTheConnection(){
        using var fixture=new Fixture(maximumConcurrentRequests:1);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));using var cancellation=new CancellationTokenSource();
        await using var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        Assert.Equal(new byte[]{1},await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[1],timeout.Token));
        var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=field.GetValue(client);
        var pending=client.ExecuteScalarAsync<byte[],byte[]>("wire","wait","fixture","wire",[2],cancellation.Token);
        await fixture.Module.Entered.Task.WaitAsync(timeout.Token);cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending.WaitAsync(timeout.Token));Assert.True(fixture.Module.Released.Task.IsCompletedSuccessfully);
        Assert.Same(transport,field.GetValue(client));Assert.Equal(new byte[]{3},await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[3],timeout.Token));
    }
    [Fact,Trait("Category","Integration")]
    public async Task CancellationDuringALargeFrameFinishesTheFrameBeforeReusingTheConnection(){
        using var fixture=new Fixture();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        Assert.Equal(new byte[]{1},await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[1],timeout.Token));
        var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=field.GetValue(client);
        await using(var rows=client.QueryAsync<byte[],byte[]>("wire","large-valid","fixture","wire",[],timeout.Token).GetAsyncEnumerator()){
            Assert.True(await rows.MoveNextAsync());Assert.Equal(8*1024*1024,rows.Current.Length);
            while(Volatile.Read(ref fixture.Module.LargeRowsStarted)<2){await Task.Delay(1,timeout.Token);}
        }
        Assert.True(fixture.Module.Released.Task.IsCompletedSuccessfully);Assert.Same(transport,field.GetValue(client));
        Assert.Equal(new byte[]{7},await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[7],timeout.Token));
    }
    [Theory,Trait("Category","Integration"),InlineData(0,8),InlineData(2,8),InlineData(1,9)]
    public async Task InvalidCancellationCannotTargetAnUnreceivedRequest(long sequence,int length){
        using var fixture=new Fixture();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));using var socket=new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);var stream=socket.GetStream();
        await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(new NativeQueryRequest("wire","echo","fixture","wire",MemoryPackSerializer.Serialize(new byte[]{1}))),timeout.Token);
        Assert.Equal(NativeFrame.Row,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
        var payload=new byte[length];BinaryPrimitives.WriteInt64LittleEndian(payload,sequence);await NativeFrame.WriteAsync(stream,12,payload,timeout.Token);await RequireClose(stream,timeout.Token);
    }
    [Theory,Trait("Category","Integration"),InlineData(0),InlineData(1),InlineData(2)]
    public async Task InvalidOrMissingCancellationAcknowledgementDiscardsTheConnection(int mode){
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var peer=Serve();await using var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port);
        var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;object? firstTransport;
        await using(var rows=client.QueryAsync<int,int>("wire","first","fixture","wire",1,timeout.Token).GetAsyncEnumerator()){
            Assert.True(await rows.MoveNextAsync());Assert.Equal(5,rows.Current);firstTransport=field.GetValue(client);Assert.NotNull(firstTransport);
        }
        Assert.Null(field.GetValue(client));Assert.Equal(9,await client.ExecuteScalarAsync<int,int>("wire","next","fixture","wire",2,timeout.Token));Assert.NotSame(firstTransport,field.GetValue(client));await peer;
        async Task Serve(){
            using(var first=await listener.AcceptTcpClientAsync(timeout.Token)){
                var stream=first.GetStream();Assert.Equal(NativeFrame.Request,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
                await NativeFrame.WriteAsync(stream,NativeFrame.Row,MemoryPackSerializer.Serialize(5),timeout.Token);
                var cancel=(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value;Assert.Equal(NativeFrame.Cancel,cancel.Kind);Assert.Equal(1,BinaryPrimitives.ReadInt64LittleEndian(cancel.Payload));
                if(mode!=2){var payload=new byte[8];BinaryPrimitives.WriteInt64LittleEndian(payload,99);if(mode==1){payload=[1];}await NativeFrame.WriteAsync(stream,NativeFrame.Cancelled,payload,timeout.Token);}
            }
            using var second=await listener.AcceptTcpClientAsync(timeout.Token);var next=second.GetStream();Assert.Equal(NativeFrame.Request,(await NativeFrame.ReadAsync(next,timeout.Token))!.Value.Kind);
            await NativeFrame.WriteAsync(next,NativeFrame.Row,MemoryPackSerializer.Serialize(9),timeout.Token);await NativeFrame.WriteAsync(next,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,timeout.Token);
        }
    }
    [Theory,Trait("Category","Integration"),InlineData(0),InlineData(1),InlineData(2),InlineData(3)]
    public async Task CancellationDeadlineAndPartialAcknowledgementReleaseTheOwnedConnection(int mode){
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(35));using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var peer=Serve();var budget=new ResourceBudget(1<<20,0);await using var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port){RecordBudget=budget};
        await using var rows=client.QueryRecordsStream<int,int>("wire","first","fixture","wire",1,timeout.Token);
        await using var iterator=rows.GetAsyncEnumerator();Assert.True(await iterator.MoveNextAsync());Assert.Equal(5,iterator.Current);
        var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=field.GetValue(client);Assert.NotNull(transport);
        var elapsed=global::System.Diagnostics.Stopwatch.StartNew();var closing=rows.DisposeAsync().AsTask();Assert.Same(closing,rows.DisposeAsync().AsTask());await closing.WaitAsync(timeout.Token);elapsed.Stop();
        if(mode is 0 or 3){Assert.InRange(elapsed.Elapsed.TotalSeconds,14,25);}
        Assert.Null(field.GetValue(client));Assert.Equal(0,budget.MemoryBytes);Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());
        Assert.Equal(9,await client.ExecuteRecordScalarAsync<int,int>("wire","next","fixture","wire",2,timeout.Token));Assert.NotSame(transport,field.GetValue(client));Assert.Equal(0,budget.MemoryBytes);await peer;
        Console.WriteLine($"cancel-boundary mode={mode} elapsedSeconds={elapsed.Elapsed.TotalSeconds:F3} memoryBytes={budget.MemoryBytes}");
        async Task Serve(){
            using(var first=await listener.AcceptTcpClientAsync(timeout.Token)){
                var stream=first.GetStream();Assert.Equal(NativeFrame.Request,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
                await NativeFrame.WriteAsync(stream,NativeFrame.Row,NativeRecordCodec<int>.Serialize(5),timeout.Token);
                var cancel=(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value;Assert.Equal(NativeFrame.Cancel,cancel.Kind);Assert.Equal(1,BinaryPrimitives.ReadInt64LittleEndian(cancel.Payload));
                var acknowledgement=new byte[13];BinaryPrimitives.WriteInt32LittleEndian(acknowledgement,9);acknowledgement[4]=NativeFrame.Cancelled;BinaryPrimitives.WriteInt64LittleEndian(acknowledgement.AsSpan(5),1);
                if(mode==1){await stream.WriteAsync(acknowledgement.AsMemory(0,3),timeout.Token);}
                else if(mode is 2 or 3){await stream.WriteAsync(acknowledgement.AsMemory(0,8),timeout.Token);}
                if(mode is 0 or 3){await RequireClose(stream,timeout.Token);}
            }
            using var next=await listener.AcceptTcpClientAsync(timeout.Token);var connected=next.GetStream();Assert.Equal(NativeFrame.Request,(await NativeFrame.ReadAsync(connected,timeout.Token))!.Value.Kind);
            await NativeFrame.WriteAsync(connected,NativeFrame.Row,NativeRecordCodec<int>.Serialize(9),timeout.Token);await NativeFrame.WriteAsync(connected,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,timeout.Token);
        }
    }
    private sealed class Module:INativeQueryModule{
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Released=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Resume=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int LargeRowsStarted;
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            if(queryId is "large" or "large-valid"){
                var row=new byte[8*1024*1024];
                if(queryId=="large-valid"){row=MemoryPackSerializer.Serialize(row);}
                try{
                    for(var index=0;index<8;index++){Interlocked.Increment(ref this.LargeRowsStarted);yield return row;}
                }finally{this.Released.TrySetResult();}
                yield break;
            }
            if(queryId=="wait"){
                this.Entered.TrySetResult();try{this.Resume.Task.WaitAsync(context.CancellationToken).GetAwaiter().GetResult();}finally{this.Released.TrySetResult();}
            }
            yield return arguments.ToArray();
        }
    }
    private sealed class Fixture:IDisposable{
        private readonly RuntimeSqlGeneratedRoot database=new(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        public Module Module{get;}=new();
        public NativeQueryServer Server{get;}
        public Fixture(int? maximumConnections=null,int? maximumConcurrentRequests=null,RuntimeSqlGeneratedRoot? transactionDatabase=null,TimeSpan? incompleteFrameTimeout=null,TimeSpan? frameWriteTimeout=null,long? maximumReceiveBufferBytes=null,FrameBufferQuota? bufferedRequests=null,FrameBufferQuota? pendingWrites=null){
            var databases=new DatabaseRegistry();
            databases.Register("fixture",new InProcessDatabaseAdapter(this.database),schemaFingerprint:"wire");
            if(transactionDatabase is not null){databases.Register("transactions",new InProcessDatabaseAdapter(transactionDatabase));}
            FrameBufferQuota? receiveBuffers=null;
            if(maximumReceiveBufferBytes is{} maximum){receiveBuffers=new(maximum);}
            this.Server=new(databases,0,maximumConnections,maximumConcurrentRequests,incompleteFrameTimeout,frameWriteTimeout,receiveBuffers,bufferedRequests,pendingWrites);
            var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
            this.Server.Register(new("wire","","","",version,[new("echo","fixture","wire","byte[]","byte[]"),new("wait","fixture","wire","byte[]","byte[]"),new("large","fixture","wire","byte[]","byte[]"),new("large-valid","fixture","wire","byte[]","byte[]")]),this.Module,version);
            this.Server.Start();
        }
        public void Dispose(){this.Server.Dispose();this.database.Dispose();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task ConnectionPolicyDiscoveryWithoutRecommendationPreservesTheExistingQueryProtocol(){
        using var fixture=new Fixture();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client=new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
        var stream=client.GetStream();
        // 版1の問い合わせには、未設定を表す応答が一つ返る。
        await NativeFrame.WriteAsync(stream,10,new byte[]{1},timeout.Token);
        var response=await NativeFrame.ReadAsync(stream,timeout.Token);
        Assert.NotNull(response);
        Assert.Equal(11,response.Value.Kind);
        Assert.Equal(new byte[]{1,0,0,0,0,0},response.Value.Payload);
        var request=new NativeQueryRequest("wire","echo","fixture","wire",MemoryPackSerializer.Serialize(new byte[]{7}));
        await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(request),timeout.Token);
        var row=(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value;
        Assert.Equal(NativeFrame.Row,row.Kind);
        Assert.Equal(new byte[]{7},MemoryPackSerializer.Deserialize<byte[]>(row.Payload));
        Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
    }

    [Fact,Trait("Category","Integration")]
    public async Task PendingWriteBudgetClosesTheConnectionBeforeWritingAnOversizedResult(){
        using var fixture=new Fixture(pendingWrites:new(1<<20));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var oversized=new TcpClient();
        await oversized.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
        var stream=oversized.GetStream();
        var request=new NativeQueryRequest("wire","large","fixture","wire",MemoryPackSerializer.Serialize(Array.Empty<byte>()));
        await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(request),timeout.Token);
        await RequireClose(stream,timeout.Token);
        await fixture.Module.Released.Task.WaitAsync(timeout.Token);
        Assert.Equal(1,Volatile.Read(ref fixture.Module.LargeRowsStarted));
        await using var next=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        Assert.Equal(new byte[]{9},await next.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[9],timeout.Token));
    }
    [Fact,Trait("Category","Integration")]
    public async Task GatewaySharesPendingWriteBudgetAcrossNativeHttpAndTds(){
        const int limit=8*1024*1024+5;
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var databases=new DatabaseRegistry();
        databases.Register("fixture",new InProcessDatabaseAdapter(database),schemaFingerprint:"wire");
        using var gateway=new GatewayServer(databases,new(){
            NativePort=0,TdsPort=0,HttpPort=0,MaximumPendingWriteBytes=limit,NativeFrameWriteTimeout=TimeSpan.FromSeconds(8),
            Http=new(){ListenAddress=IPAddress.Loopback}
        });
        var module=new Module();
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        gateway.Native.Register(new("wire","","","",version,[
            new("echo","fixture","wire","byte[]","byte[]"),
            new("large","fixture","wire","byte[]","byte[]")
        ]),module,version);
        gateway.Start();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var httpRequest="GET /api/databases HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"u8.ToArray();
        var tdsRequest=new byte[]{TdsConstants.PacketTypePreLogin,1,0,9,0,0,1,0,0};
        await VerifyResponseAsync(gateway.HttpPort,httpRequest,"HTTP/1.1 200"u8.ToArray());
        await VerifyResponseAsync(gateway.TdsPort,tdsRequest,new byte[]{4});

        using var stalled=new TcpClient(AddressFamily.InterNetwork){ReceiveBufferSize=4096};
        await stalled.ConnectAsync(IPAddress.Loopback,gateway.NativePort,deadline.Token);
        var request=new NativeQueryRequest("wire","large","fixture","wire",MemoryPackSerializer.Serialize(Array.Empty<byte>()));
        await NativeFrame.WriteAsync(stalled.GetStream(),NativeFrame.Request,MemoryPackSerializer.Serialize(request),deadline.Token);
        while(gateway.SharedPendingWriteBytes!=limit){await Task.Delay(10,deadline.Token);}
        await Task.WhenAll(VerifyResponseAsync(gateway.HttpPort,httpRequest,null),VerifyResponseAsync(gateway.TdsPort,tdsRequest,null));
        Assert.Equal(limit,gateway.SharedPendingWriteBytes);
        await module.Released.Task.WaitAsync(deadline.Token);
        Assert.Equal(0,gateway.SharedPendingWriteBytes);
        Assert.InRange(Volatile.Read(ref module.LargeRowsStarted),1,7);

        await VerifyResponseAsync(gateway.HttpPort,httpRequest,"HTTP/1.1 200"u8.ToArray());
        await VerifyResponseAsync(gateway.TdsPort,tdsRequest,new byte[]{4});
        await using var recovered=new NativeQueryClient("127.0.0.1",gateway.NativePort);
        Assert.Equal(new byte[]{9},await recovered.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[9],deadline.Token));

        async Task VerifyResponseAsync(int port,byte[] bytes,byte[]? expected){
            using var client=new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback,port,deadline.Token);
            var stream=client.GetStream();
            await stream.WriteAsync(bytes,deadline.Token);
            if(expected is null){await RequireClose(stream,deadline.Token);return;}
            var response=new byte[expected.Length];
            await stream.ReadExactlyAsync(response,deadline.Token);
            Assert.Equal(expected,response);
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task BufferedNativeRequestRetainsItsBudgetUntilResponseCompletes(){
        var wait=new NativeQueryRequest("wire","wait","fixture","wire",MemoryPackSerializer.Serialize(new byte[]{7}));
        var payload=MemoryPackSerializer.Serialize(wait);
        using var fixture=new Fixture(bufferedRequests:new(payload.Length+1));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var active=new TcpClient();
        await active.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
        var activeStream=active.GetStream();
        await NativeFrame.WriteAsync(activeStream,NativeFrame.Request,payload,timeout.Token);
        await fixture.Module.Entered.Task.WaitAsync(timeout.Token);
        try{
            using var excess=new TcpClient();
            await excess.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
            await NativeFrame.WriteAsync(excess.GetStream(),NativeFrame.Request,payload,timeout.Token);
            await RequireClose(excess.GetStream(),timeout.Token);
        }finally{fixture.Module.Resume.TrySetResult();}
        Assert.Equal(NativeFrame.Row,(await NativeFrame.ReadAsync(activeStream,timeout.Token))!.Value.Kind);
        Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(activeStream,timeout.Token))!.Value.Kind);
        active.Client.Shutdown(SocketShutdown.Send);
        await RequireClose(activeStream,timeout.Token);
        using var next=new TcpClient();
        await next.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
        var nextStream=next.GetStream();
        await NativeFrame.WriteAsync(nextStream,NativeFrame.Request,payload,timeout.Token);
        var row=(await NativeFrame.ReadAsync(nextStream,timeout.Token))!.Value;
        Assert.Equal(NativeFrame.Row,row.Kind);
        Assert.Equal(new byte[]{7},MemoryPackSerializer.Deserialize<byte[]>(row.Payload));
        Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(nextStream,timeout.Token))!.Value.Kind);
    }

    [Fact,Trait("Category","Integration")]
    public async Task OversizedFragmentReservationClosesOnlyTheSendingConnection(){
        // 型契約を含む正常な要求は、TCPで分割されてもこの枠で復元できる。
        const int receiveQuota=256;
        using var fixture=new Fixture(maximumReceiveBufferBytes:receiveQuota);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var idle=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        Assert.Equal(new byte[]{7},await idle.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[7],timeout.Token));
        using var sending=new TcpClient();
        await sending.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
        var stream=sending.GetStream();
        var oversizedHeader=new byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(oversizedHeader,receiveQuota+1);
        oversizedHeader[4]=NativeFrame.Request;
        await stream.WriteAsync(oversizedHeader,timeout.Token);
        await RequireClose(stream,timeout.Token);
        Assert.False(fixture.Module.Entered.Task.IsCompleted);
        Assert.Equal(new byte[]{8},await idle.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[8],timeout.Token));
    }

    [Fact,Trait("Category","Integration")]
    public async Task WriteDeadlineReleasesTheEnumeratorWhenThePeerStopsReading(){
        using var fixture=new Fixture(frameWriteTimeout:TimeSpan.FromMilliseconds(300));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stalled=new TcpClient{ReceiveBufferSize=4096};
        await stalled.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
        var request=new NativeQueryRequest("wire","large","fixture","wire",MemoryPackSerializer.Serialize(Array.Empty<byte>()));
        await NativeFrame.WriteAsync(stalled.GetStream(),NativeFrame.Request,MemoryPackSerializer.Serialize(request),timeout.Token);
        await fixture.Module.Released.Task.WaitAsync(timeout.Token);
        Assert.InRange(Volatile.Read(ref fixture.Module.LargeRowsStarted),1,7);
        await using var other=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        Assert.Equal(new byte[]{9},await other.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[9],timeout.Token));
    }
    [Fact,Trait("Category","Integration")]
    public async Task IncompleteFrameDeadlineClosesStalledSendersButKeepsIdleConnections(){
        using var fixture=new Fixture(incompleteFrameTimeout:TimeSpan.FromMilliseconds(300));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var idle=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        Assert.Equal(new byte[]{7},await idle.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[7],timeout.Token));
        foreach(var incomplete in new byte[][]{[16],[16,0,0,0,NativeFrame.Request,1]}){
            using var stalled=new TcpClient();
            await stalled.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
            var stream=stalled.GetStream();
            await stream.WriteAsync(incomplete,timeout.Token);
            await RequireClose(stream,timeout.Token);
        }
        Assert.Equal(new byte[]{8},await idle.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[8],timeout.Token));
    }
    [Fact,Trait("Category","Integration")]
    public async Task RequestLimitRejectsBeforeExecutionAndReleasesTheSlot(){
        using var transactionDatabase=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        using var fixture=new Fixture(maximumConcurrentRequests:1,transactionDatabase:transactionDatabase);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var first=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        await using var second=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        var active=first.ExecuteScalarAsync<byte[],byte[]>("wire","wait","fixture","wire",[7],timeout.Token);
        try{
            await fixture.Module.Entered.Task.WaitAsync(timeout.Token);
            var error=await Assert.ThrowsAsync<NativeQueryException>(()=>second.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[9],timeout.Token));
            Assert.True(error.IsRecoverable);


            await using(var committed=await second.BeginTransactionAsync("transactions",timeout.Token)){
                await committed.CommitAsync(timeout.Token);
            }
            await using(var cancelled=await second.BeginTransactionAsync("transactions",timeout.Token)){
                await cancelled.RollbackAsync(timeout.Token);
            }
            Assert.False(active.IsCompleted);
        }finally{fixture.Module.Resume.TrySetResult();}
        Assert.Equal(new byte[]{7},await active);
        Assert.Equal(new byte[]{9},await first.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",[9],timeout.Token));
    }
    [Fact,Trait("Category","Integration")]
    public async Task ConnectionLimitRejectsAnotherSocketWithoutInterruptingAnAdmittedConnection(){
        using var fixture=new Fixture(maximumConnections:1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var first=new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
        var stream=first.GetStream();
        var body=MemoryPackSerializer.Serialize(new NativeQueryRequest("wire","echo","fixture","wire",MemoryPackSerializer.Serialize(new byte[]{7})));
        var frame=new byte[body.Length+5];BinaryPrimitives.WriteInt32LittleEndian(frame,body.Length+1);frame[4]=NativeFrame.Request;body.CopyTo(frame,5);
        await ReadEcho();
        using(var rejected=new TcpClient()){
            await rejected.ConnectAsync(IPAddress.Loopback,fixture.Server.Port,timeout.Token);
            await RequireClose(rejected.GetStream(),timeout.Token);
        }
        await ReadEcho();

        async Task ReadEcho(){
            await stream.WriteAsync(frame,timeout.Token);
            var row=(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value;
            Assert.Equal(NativeFrame.Row,row.Kind);
            Assert.Equal(new byte[]{7},MemoryPackSerializer.Deserialize<byte[]>(row.Payload));
            Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
        }
    }
    [Fact,Trait("Category","Integration")]
    public async Task LargePayloadAndCoalescedRequestsUseTheExistingWireFormat(){
        using var fixture=new Fixture();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var expected=Enumerable.Range(0,200000).Select(i=>(byte)i).ToArray();
        await using(var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port)){Assert.Equal(expected,await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",expected,timeout.Token));}
        using var socket=new TcpClient();await socket.ConnectAsync("127.0.0.1",fixture.Server.Port,timeout.Token);var stream=socket.GetStream();var body=MemoryPackSerializer.Serialize(new NativeQueryRequest("wire","echo","fixture","wire",MemoryPackSerializer.Serialize(new byte[]{7})));var frame=new byte[body.Length+5];BinaryPrimitives.WriteInt32LittleEndian(frame,body.Length+1);frame[4]=NativeFrame.Request;body.CopyTo(frame,5);
        // ヘッダーを1バイトずつ送り、続くフレームは連結して送る。
        for(var index=0;index<5;index++){await stream.WriteAsync(frame.AsMemory(index,1),timeout.Token);}var joined=new byte[body.Length+frame.Length];body.CopyTo(joined,0);frame.CopyTo(joined,body.Length);await stream.WriteAsync(joined,timeout.Token);
        for(var index=0;index<2;index++){var row=await NativeFrame.ReadAsync(stream,timeout.Token);Assert.Equal(NativeFrame.Row,row!.Value.Kind);Assert.Equal(new byte[]{7},MemoryPackSerializer.Deserialize<byte[]>(row.Value.Payload));Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);}
    }
    [Fact,Trait("Category","Integration")]
    public async Task InvalidAndIncompleteFramesCloseOnlyTheirOwnConnection(){
        using var fixture=new Fixture();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        foreach(var length in new[]{-1,0,NativeFrame.MaximumBytes+1,1024}){
            using var socket=new TcpClient();await socket.ConnectAsync("127.0.0.1",fixture.Server.Port,timeout.Token);var stream=socket.GetStream();var header=new byte[5];BinaryPrimitives.WriteInt32LittleEndian(header,length);header[4]=NativeFrame.Request;await stream.WriteAsync(header,timeout.Token);if(length==1024){socket.Client.Shutdown(SocketShutdown.Send);}await RequireClose(stream,timeout.Token);
        }
        {using var socket=new TcpClient();await socket.ConnectAsync("127.0.0.1",fixture.Server.Port,timeout.Token);var stream=socket.GetStream();await stream.WriteAsync(new byte[]{1,0,0,0,255},timeout.Token);await RequireClose(stream,timeout.Token);}
        await using var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port);Assert.Equal(new byte[]{9},await client.ExecuteScalarAsync<byte[],byte[]>("wire","echo","fixture","wire",new byte[]{9},timeout.Token));
    }
    private static async Task RequireClose(NetworkStream stream,CancellationToken cancellation){try{Assert.Equal(0,await stream.ReadAsync(new byte[1],cancellation));}catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}}
    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BufferedScalarResultsReplayAfterDisconnect(bool spill){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");
        var root=Path.Combine(data,"native-replay-"+Guid.NewGuid().ToString("N"));
        var budget=new ResourceBudget(65600,256<<10);
        var options=new NativeResultOptions(root,1<<20,1<<20,spill?0:32<<10){ParentBudget=budget};
        await Verify(new[]{int.MinValue,0,int.MaxValue});
        await Verify(new[]{long.MinValue,0L,long.MaxValue});
        await Verify(new string?[]{null,"","界"});
        await Verify(new string?[]{null,"","界"},MemoryPackSerializerOptions.Utf16);

        async Task Verify<T>(T[] rows,MemoryPackSerializerOptions? encoding=null){
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener=new TcpListener(IPAddress.Loopback,0);
            listener.Start();
            var send=Send();
            NativeResult<T> result;
            await using(var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port)){
                result=await client.QueryBufferedAsync<int,T>("wire","rows","fixture","wire",0,options,deadline.Token);
            }
            await send;
            using(result){
                Assert.Equal(spill,result.IsFileBacked);
                Assert.Equal(rows,result.ReadRows().ToArray());
                Assert.Equal(rows,result.ReadRows().ToArray());
            }
            Assert.Equal(0,budget.MemoryBytes);
            Assert.Equal(0,budget.TemporaryStorageBytes);
            Assert.False(Directory.Exists(root)&&Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Any());

            async Task Send(){
                using var peer=await listener.AcceptTcpClientAsync(deadline.Token);
                var stream=peer.GetStream();
                var frame=await NativeFrame.ReadAsync(stream,deadline.Token);
                Assert.Equal(NativeFrame.Request,frame!.Value.Kind);
                foreach(var row in rows){
                    await NativeFrame.WriteAsync(stream,NativeFrame.Row,MemoryPackSerializer.Serialize(row,encoding),deadline.Token);
                }
                await NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,deadline.Token);
            }
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task ClientRejectsMalformedCompletionAndReconnects(){
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        await using var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port);

        async Task Respond(bool valid){
            using var peer=await listener.AcceptTcpClientAsync(timeout.Token);
            var stream=peer.GetStream();
            var request=await NativeFrame.ReadAsync(stream,timeout.Token);
            Assert.Equal(NativeFrame.Request,request!.Value.Kind);
            await NativeFrame.WriteAsync(stream,NativeFrame.Row,MemoryPackSerializer.Serialize(19),timeout.Token);
            if(valid){
                await NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,timeout.Token);
            }else{
                // 本体は送らない。ヘッダーだけで不正が判明する。
                await stream.WriteAsync(new byte[]{2,0,0,0,NativeFrame.Completed},timeout.Token);
                await RequireClose(stream,timeout.Token);
            }
        }

        var malformed=Respond(false);
        await Assert.ThrowsAsync<InvalidDataException>(()=>client.ExecuteScalarAsync<int,int>("wire","rows","fixture","wire",0,timeout.Token));
        await malformed;
        var valid=Respond(true);
        Assert.Equal(19,await client.ExecuteScalarAsync<int,int>("wire","rows","fixture","wire",0,timeout.Token));
        await valid;
    }
    [Fact,Trait("Category","Integration")]
    public async Task ServerStopCancelsAnExecutingQueryAndReleasesItsEnumerator(){
        using var fixture=new Fixture();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await using var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port);var operation=client.ExecuteScalarAsync<byte[],byte[]>("wire","wait","fixture","wire",[],timeout.Token);await fixture.Module.Entered.Task.WaitAsync(timeout.Token);await fixture.Server.StopAsync().WaitAsync(timeout.Token);await fixture.Module.Released.Task.WaitAsync(timeout.Token);await Assert.ThrowsAnyAsync<IOException>(async()=>await operation);
    }
}
