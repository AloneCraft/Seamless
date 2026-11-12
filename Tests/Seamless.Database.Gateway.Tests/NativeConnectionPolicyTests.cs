using System.Net;
using System.Net.Sockets;
using MemoryPack;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Tests;

public sealed class NativeConnectionPolicyTests{
    [Fact]
    public void InvalidConnectionPoliciesAreRejected(){
        Assert.Throws<ArgumentOutOfRangeException>(()=>new NativeConnectionPolicy((NativeConnectionLifetime)100));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseWhenIdle));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseWhenIdle,TimeSpan.Zero));
        Assert.Throws<ArgumentException>(()=>new NativeConnectionPolicy(NativeConnectionLifetime.KeepAlive,TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RecommendationWireValidatesValuesAndExactSize(){
        Assert.Null(NativeConnectionPolicyWire.ReadRecommendation(NativeConnectionPolicyWire.WriteRecommendation(null)));
        foreach(var lifetime in Enum.GetValues<NativeConnectionLifetime>()){
            var policy=Policy(lifetime);
            Assert.Equal(policy,NativeConnectionPolicyWire.ReadRecommendation(NativeConnectionPolicyWire.WriteRecommendation(policy)));
        }
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadRecommendation([1,0]));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadRecommendation([2,1,0,0,0,0]));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadRecommendation([1,1,1,0,0,0]));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadRecommendation([1,2,0,0,0,0]));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadRecommendation([1,2,255,255,255,255]));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadRecommendation([1,4,0,0,0,0]));
    }

    [Fact]
    public async Task MissingRecommendationResponseDoesNotSendAnApplicationRequest(){
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        await using var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port){UseServerConnectionPolicy=true};
        var opening=client.ExecuteScalarAsync<int,int>("wire","update","fixture","wire",1,deadline.Token);
        using(var accepted=await listener.AcceptTcpClientAsync(deadline.Token)){
            var stream=accepted.GetStream();
            var request=await NativeFrame.ReadAsync(stream,deadline.Token);
            Assert.NotNull(request);
            Assert.Equal(NativeConnectionPolicyWire.Discover,request.Value.Kind);
            Assert.Equal(new byte[]{NativeConnectionPolicyWire.Version},request.Value.Payload);
            accepted.Client.Shutdown(SocketShutdown.Send);
            await RequireClose(stream,deadline.Token);
        }
        await Assert.ThrowsAsync<EndOfStreamException>(()=>opening);
        Assert.False(listener.Pending());
    }

    [Fact]
    public async Task IdlePolicyReusesTheSocketBetweenConsecutiveRequests(){
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        var policy=new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseWhenIdle,TimeSpan.FromSeconds(2));
        await using var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,policy);
        var peer=Observe();
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("wire","first","fixture","wire",0,deadline.Token));
        Assert.Equal(2,await client.ExecuteScalarAsync<int,int>("wire","second","fixture","wire",0,deadline.Token));
        await peer;
        Assert.False(listener.Pending());

        async Task Observe(){
            using var accepted=await listener.AcceptTcpClientAsync(deadline.Token);
            var stream=accepted.GetStream();
            Assert.Equal("first",(await ReadRequest(stream,deadline.Token)).QueryId);
            await WriteRow(stream,1,deadline.Token);
            await Complete(stream,deadline.Token);
            Assert.Equal("second",(await ReadRequest(stream,deadline.Token)).QueryId);
            await WriteRow(stream,2,deadline.Token);
            await Complete(stream,deadline.Token);
            await RequireClose(stream,deadline.Token);
        }
    }

    [Theory]
    [InlineData(NativeConnectionLifetime.KeepAlive)]
    [InlineData(NativeConnectionLifetime.ReleaseWhenIdle)]
    [InlineData(NativeConnectionLifetime.ReleaseAfterOperation)]
    public async Task ConnectionPolicyKeepsTheSocketUntilTheResultIsConsumed(NativeConnectionLifetime lifetime){
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;
        var policy=Policy(lifetime);
        var client=new NativeQueryClient("127.0.0.1",port,policy);
        var readyToFinish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstClosed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer=Observe();
        try{
            await client.ConnectAsync(deadline.Token);
            await using(var rows=client.QueryAsync<int,int>("wire","first","fixture","wire",1,deadline.Token).GetAsyncEnumerator()){
                Assert.True(await rows.MoveNextAsync());
                Assert.Equal(1,rows.Current);
                await Task.Delay(300,deadline.Token);
                Assert.False(firstClosed.Task.IsCompleted);
                readyToFinish.SetResult();
                Assert.False(await rows.MoveNextAsync());
            }
            if(lifetime!=NativeConnectionLifetime.KeepAlive){await firstClosed.Task.WaitAsync(deadline.Token);}
            Assert.Equal(2,await client.ExecuteScalarAsync<int,int>("wire","second","fixture","wire",2,deadline.Token));
        }finally{await client.DisposeAsync();}
        await peer;

        async Task Observe(){
            using var first=await listener.AcceptTcpClientAsync(deadline.Token);
            var stream=first.GetStream();
            Assert.Equal("first",(await ReadRequest(stream,deadline.Token)).QueryId);
            await WriteRow(stream,1,deadline.Token);
            await readyToFinish.Task.WaitAsync(deadline.Token);
            await Complete(stream,deadline.Token);
            if(lifetime==NativeConnectionLifetime.KeepAlive){
                await Second(stream);
            }else{
                await RequireClose(stream,deadline.Token);
                firstClosed.SetResult();
                using var next=await listener.AcceptTcpClientAsync(deadline.Token);
                await Second(next.GetStream());
            }
        }
        async Task Second(NetworkStream stream){
            Assert.Equal("second",(await ReadRequest(stream,deadline.Token)).QueryId);
            await WriteRow(stream,2,deadline.Token);
            await Complete(stream,deadline.Token);
            await RequireClose(stream,deadline.Token);
        }
    }

    [Theory]
    [InlineData(NativeConnectionLifetime.ReleaseWhenIdle,true)]
    [InlineData(NativeConnectionLifetime.ReleaseWhenIdle,false)]
    [InlineData(NativeConnectionLifetime.ReleaseAfterOperation,true)]
    [InlineData(NativeConnectionLifetime.ReleaseAfterOperation,false)]
    public async Task ConnectionPolicyRetainsTheSameSocketUntilTheOuterTransactionEnds(NativeConnectionLifetime lifetime,bool commit){
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        await using var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,Policy(lifetime));
        var peer=Observe();
        await client.ConnectAsync(deadline.Token);
        await using(var transaction=await client.BeginTransactionAsync("fixture",deadline.Token)){
            await Task.Delay(300,deadline.Token);
            await transaction.BeginNestedAsync(deadline.Token);
            await transaction.CommitAsync(deadline.Token);
            await Task.Delay(300,deadline.Token);
            Assert.Equal(1,transaction.TransactionCount);
            if(commit){await transaction.CommitAsync(deadline.Token);}else{await transaction.RollbackAsync(deadline.Token);}
        }
        await peer;

        async Task Observe(){
            using var accepted=await listener.AcceptTcpClientAsync(deadline.Token);
            var stream=accepted.GetStream();
            var expected=Guid.Empty;
            foreach(var query in new[]{"begin","begin","commit",commit?"commit":"rollback"}){
                var request=await ReadRequest(stream,deadline.Token);
                Assert.Equal("$transaction",request.ModuleId);
                Assert.Equal(query,request.QueryId);
                Assert.NotEqual(Guid.Empty,request.TransactionId);
                if(expected==Guid.Empty){expected=request.TransactionId;}
                Assert.Equal(expected,request.TransactionId);
                await Complete(stream,deadline.Token);
            }
            await RequireClose(stream,deadline.Token);
        }
    }

    [Fact]
    public async Task ConnectionPolicyDoesNotResendAnOperationAfterItsResponseIsLost(){
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        await using var client=new NativeQueryClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,Policy(NativeConnectionLifetime.ReleaseAfterOperation));
        var failed=client.ExecuteScalarAsync<int,int>("wire","update","fixture","wire",1,deadline.Token);
        using(var accepted=await listener.AcceptTcpClientAsync(deadline.Token)){
            Assert.Equal("update",(await ReadRequest(accepted.GetStream(),deadline.Token)).QueryId);
        }
        await Assert.ThrowsAsync<EndOfStreamException>(()=>failed);
        Assert.False(listener.Pending());

        var next=client.ExecuteScalarAsync<int,int>("wire","read","fixture","wire",0,deadline.Token);
        using(var accepted=await listener.AcceptTcpClientAsync(deadline.Token)){
            var stream=accepted.GetStream();
            Assert.Equal("read",(await ReadRequest(stream,deadline.Token)).QueryId);
            await WriteRow(stream,7,deadline.Token);
            await Complete(stream,deadline.Token);
            Assert.Equal(7,await next);
            await RequireClose(stream,deadline.Token);
        }
    }

    private static NativeConnectionPolicy Policy(NativeConnectionLifetime lifetime){
        if(lifetime==NativeConnectionLifetime.ReleaseWhenIdle){return new(lifetime,TimeSpan.FromMilliseconds(180));}
        return new(lifetime);
    }
    private static async Task<NativeQueryRequest> ReadRequest(NetworkStream stream,CancellationToken cancellationToken){
        var frame=await NativeFrame.ReadAsync(stream,cancellationToken);
        Assert.NotNull(frame);
        Assert.Equal(NativeFrame.Request,frame.Value.Kind);
        return MemoryPackSerializer.Deserialize<NativeQueryRequest>(frame.Value.Payload)!;
    }
    private static ValueTask WriteRow(NetworkStream stream,int row,CancellationToken cancellationToken)=>NativeFrame.WriteAsync(stream,NativeFrame.Row,MemoryPackSerializer.Serialize(row),cancellationToken);
    private static ValueTask Complete(NetworkStream stream,CancellationToken cancellationToken)=>NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,cancellationToken);
    private static async Task RequireClose(NetworkStream stream,CancellationToken cancellationToken){
        try{Assert.Equal(0,await stream.ReadAsync(new byte[1],cancellationToken));}
        catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}
    }
}
