using System.Net;
using System.Net.Sockets;
using MemoryPack;
using ClientPool=Seamless.Database.Client.NativeConnectionPool;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Tests;

public sealed class NativeConnectionPool{
    [Fact]
    public async Task RentAsyncReusesOnlyTheTransportAndKeepsItDuringTransactionsAndResults(){
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var listener=Listen();
        using var pool=new ClientPool("127.0.0.1",Port(listener),new(){MaximumConnections=1,MaximumWaiters=0});
        var first=await pool.RentAsync(timeout.Token);
        Assert.False(listener.Pending());
        var exchange=Read(first,"first",timeout.Token);
        using var peer=await listener.AcceptTcpClientAsync(timeout.Token);
        var stream=peer.GetStream();
        Assert.Equal("first",(await Request(stream,timeout.Token)).QueryId);
        await Reply(stream,1,timeout.Token);
        Assert.Equal(1,await exchange);
        await first.DisposeAsync();
        await first.DisposeAsync();

        await using var second=await pool.RentAsync(timeout.Token);
        Assert.NotSame(first,second);
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>Read(first,"stale",timeout.Token));
        await using(var rows=second.QueryAsync<int,int>("wire","stream","fixture","wire",1,timeout.Token).GetAsyncEnumerator()){
            var next=rows.MoveNextAsync().AsTask();
            Assert.Equal("stream",(await Request(stream,timeout.Token)).QueryId);
            await NativeFrame.WriteAsync(stream,NativeFrame.Row,MemoryPackSerializer.Serialize(7),timeout.Token);
            Assert.True(await next);
            Assert.Equal(7,rows.Current);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>second.DisposeAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(()=>pool.RentAsync(timeout.Token).AsTask());
            await Complete(stream,timeout.Token);
            Assert.False(await rows.MoveNextAsync());
        }
        var opening=second.BeginTransactionAsync("fixture",timeout.Token);
        var begin=await Request(stream,timeout.Token);
        Assert.Equal("begin",begin.QueryId);
        await Complete(stream,timeout.Token);
        await using var transaction=await opening;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>second.DisposeAsync().AsTask());
        pool.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>pool.RentAsync(timeout.Token).AsTask());
        var rollback=transaction.RollbackAsync(timeout.Token);
        var finish=await Request(stream,timeout.Token);
        Assert.Equal("rollback",finish.QueryId);
        Assert.Equal(begin.TransactionId,finish.TransactionId);
        await Complete(stream,timeout.Token);
        await rollback;
        await second.DisposeAsync();
        Assert.Equal(0,await stream.ReadAsync(new byte[1],timeout.Token));
        Assert.False(listener.Pending());
    }

    [Fact]
    public async Task RentAsyncBoundsWaitingAndReleasesWaitersOnCancellationTimeoutAndClose(){
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var pool=new ClientPool("127.0.0.1",14335,new(){MaximumConnections=1,MaximumWaiters=1,WaitTimeout=TimeSpan.FromMilliseconds(500)});
        await using var leased=await pool.RentAsync(deadline.Token);
        using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token)){
            var pending=pool.RentAsync(cancel.Token).AsTask();
            Assert.False(pending.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>pool.RentAsync(deadline.Token).AsTask());
            await cancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
        }
        await Assert.ThrowsAsync<TimeoutException>(()=>pool.RentAsync(deadline.Token).AsTask());
        var unblocked=pool.RentAsync(deadline.Token).AsTask();
        await leased.DisposeAsync();
        await using var next=await unblocked;
        var closing=pool.RentAsync(deadline.Token).AsTask();
        pool.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>closing);
    }

    [Theory]
    [InlineData(NativeConnectionLifetime.ReleaseWhenIdle)]
    [InlineData(NativeConnectionLifetime.ReleaseAfterOperation)]
    public async Task ReturnedTransportsFollowTheConnectionPolicy(NativeConnectionLifetime lifetime){
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=Listen();
        NativeConnectionPolicy policy;
        if(lifetime==NativeConnectionLifetime.ReleaseWhenIdle){policy=new(lifetime,TimeSpan.FromMilliseconds(350));}
        else{policy=new(lifetime);}
        using var pool=new ClientPool("127.0.0.1",Port(listener),new(){MaximumConnections=1,ConnectionPolicy=policy});
        await using var client=await pool.RentAsync(timeout.Token);
        var exchange=Read(client,"first",timeout.Token);
        using var peer=await listener.AcceptTcpClientAsync(timeout.Token);
        var stream=peer.GetStream();
        await Request(stream,timeout.Token);
        await Reply(stream,1,timeout.Token);
        Assert.Equal(1,await exchange);
        await client.DisposeAsync();
        Assert.Equal(0,await stream.ReadAsync(new byte[1],timeout.Token));
        await using var next=await pool.RentAsync(timeout.Token);
        var read=Read(next,"second",timeout.Token);
        using var replacement=await listener.AcceptTcpClientAsync(timeout.Token);
        Assert.Equal("second",(await Request(replacement.GetStream(),timeout.Token)).QueryId);
        await Reply(replacement.GetStream(),2,timeout.Token);
        Assert.Equal(2,await read);
    }

    [Fact]
    public async Task LostResponseDoesNotReplayAndReturnsNoTransport(){
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener=Listen();
        using var pool=new ClientPool("127.0.0.1",Port(listener),new(){MaximumConnections=1});
        await using(var client=await pool.RentAsync(timeout.Token)){
            var update=Read(client,"update",timeout.Token);
            using(var peer=await listener.AcceptTcpClientAsync(timeout.Token)){
                Assert.Equal("update",(await Request(peer.GetStream(),timeout.Token)).QueryId);
                peer.Client.Shutdown(SocketShutdown.Send);
                await Assert.ThrowsAsync<EndOfStreamException>(()=>update);
            }
        }
        Assert.False(listener.Pending());
        await using var next=await pool.RentAsync(timeout.Token);
        var read=Read(next,"verify",timeout.Token);
        using var replacement=await listener.AcceptTcpClientAsync(timeout.Token);
        Assert.Equal("verify",(await Request(replacement.GetStream(),timeout.Token)).QueryId);
        await Reply(replacement.GetStream(),2,timeout.Token);
        Assert.Equal(2,await read);
    }

    private static TcpListener Listen(){var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();return listener;}
    private static int Port(TcpListener listener)=>((IPEndPoint)listener.LocalEndpoint).Port;
    private static Task<int> Read(NativeQueryClient client,string query,CancellationToken cancellation)=>client.ExecuteScalarAsync<int,int>("wire",query,"fixture","wire",1,cancellation);
    private static async Task<NativeQueryRequest> Request(NetworkStream stream,CancellationToken cancellation){
        var frame=await NativeFrame.ReadAsync(stream,cancellation);
        Assert.NotNull(frame);
        Assert.Equal(NativeFrame.Request,frame.Value.Kind);
        return MemoryPackSerializer.Deserialize<NativeQueryRequest>(frame.Value.Payload)!;
    }
    private static async Task Reply(NetworkStream stream,int value,CancellationToken cancellation){
        await NativeFrame.WriteAsync(stream,NativeFrame.Row,MemoryPackSerializer.Serialize(value),cancellation);
        await Complete(stream,cancellation);
    }
    private static ValueTask Complete(NetworkStream stream,CancellationToken cancellation)=>NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,cancellation);
}
