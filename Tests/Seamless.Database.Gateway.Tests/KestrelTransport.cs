using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tests;

public sealed class KestrelTransport{
    private sealed record Frame(byte[] Bytes);
    private sealed record Duplex(PipeReader Input,PipeWriter Output):IDuplexPipe;
    [Fact,Trait("Category","Unit")]
    public void QuotaReleasesExactlyOnce(){
        var quota=new ConnectionQuota(2);var first=quota.TryEnter()!;var second=quota.TryEnter()!;Assert.Null(quota.TryEnter());Assert.Equal(2,quota.Count);first.Dispose();first.Dispose();Assert.Equal(1,quota.Count);second.Dispose();Assert.Equal(0,quota.Count);
    }
    [Fact,Trait("Category","Unit")]
    public async Task FragmentedLargeFramesAndBufferedNextFrame(){
        foreach(var bigEndian in new[]{false,true}){
            var incoming=new Pipe(new(pauseWriterThreshold:32,resumeWriterThreshold:16,minimumSegmentSize:16));var outgoing=new Pipe();var frames=new LengthPrefixedPipe(new Duplex(incoming.Reader,outgoing.Writer),8192,bigEndian);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var expected=Enumerable.Range(0,4097).Select(i=>(byte)i).ToArray();var header=new byte[4];if(bigEndian){BinaryPrimitives.WriteInt32BigEndian(header,expected.Length);}else{BinaryPrimitives.WriteInt32LittleEndian(header,expected.Length);}
            var sending=Task.Run(async()=>{foreach(var value in header){await incoming.Writer.WriteAsync(new[]{value},timeout.Token);}for(var offset=0;offset<expected.Length;offset+=17){await incoming.Writer.WriteAsync(expected.AsMemory(offset,Math.Min(17,expected.Length-offset)),timeout.Token);}if(bigEndian){BinaryPrimitives.WriteInt32BigEndian(header,1);}else{BinaryPrimitives.WriteInt32LittleEndian(header,1);}incoming.Writer.Write(header);incoming.Writer.Write(new byte[]{99});await incoming.Writer.CompleteAsync();});
            var result=await frames.ReadAsync(bytes=>new Frame(bytes.ToArray()),timeout.Token);Assert.Equal(expected,result!.Bytes);Assert.Equal(new byte[]{99},(await frames.ReadAsync(bytes=>new Frame(bytes.ToArray()),timeout.Token))!.Bytes);Assert.Null(await frames.ReadAsync(bytes=>new Frame(bytes.ToArray()),timeout.Token));await sending;await incoming.Reader.CompleteAsync();await outgoing.Reader.CompleteAsync();await outgoing.Writer.CompleteAsync();
        }
    }
    [Fact,Trait("Category","Unit")]
    public async Task InvalidLengthKindAndIncompleteFrames(){
        foreach(var length in new[]{-1,0,65}){var pipe=new Pipe();var output=new Pipe();var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,length);await pipe.Writer.WriteAsync(header);await pipe.Writer.CompleteAsync();await Assert.ThrowsAsync<InvalidDataException>(async()=>await new LengthPrefixedPipe(new Duplex(pipe.Reader,output.Writer),64).ReadAsync(bytes=>new Frame(bytes.ToArray())));await pipe.Reader.CompleteAsync();await output.Writer.CompleteAsync();await output.Reader.CompleteAsync();}
        foreach(var bytes in new byte[][]{[1,0],[2,0,0,0,1]}){var pipe=new Pipe();var output=new Pipe();await pipe.Writer.WriteAsync(bytes);await pipe.Writer.CompleteAsync();await Assert.ThrowsAsync<EndOfStreamException>(async()=>await new LengthPrefixedPipe(new Duplex(pipe.Reader,output.Writer),64).ReadAsync(value=>new Frame(value.ToArray())));await pipe.Reader.CompleteAsync();await output.Writer.CompleteAsync();await output.Reader.CompleteAsync();}
        {var pipe=new Pipe();var output=new Pipe();await pipe.Writer.WriteAsync(new byte[]{2,0,0,0,7});await Assert.ThrowsAsync<InvalidDataException>(async()=>await new LengthPrefixedPipe(new Duplex(pipe.Reader,output.Writer),64).ReadAsync(bytes=>new Frame(bytes.ToArray()),maximumPayload:_=>0));await pipe.Writer.CompleteAsync();await pipe.Reader.CompleteAsync();await output.Writer.CompleteAsync();await output.Reader.CompleteAsync();}
    }
    [Fact,Trait("Category","Unit")]
    public async Task BorrowedStreamDoesNotCompletePipesAndBlockedOutputCancels(){
        var incoming=new Pipe();var outgoing=new Pipe(new(pauseWriterThreshold:32,resumeWriterThreshold:16));using(var stream=new DuplexPipeStream(new Duplex(incoming.Reader,outgoing.Writer))){}
        await incoming.Writer.WriteAsync(new byte[]{1});var result=await incoming.Reader.ReadAsync();Assert.Equal(1,result.Buffer.Length);incoming.Reader.AdvanceTo(result.Buffer.End);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await new LengthPrefixedPipe(new Duplex(incoming.Reader,outgoing.Writer),8192).WriteAsync(1,new byte[4096],timeout.Token));await incoming.Writer.CompleteAsync();await incoming.Reader.CompleteAsync();await outgoing.Writer.CompleteAsync();await outgoing.Reader.CompleteAsync();
    }
    [Fact,Trait("Category","Integration")]
    public async Task IdleTcpConnectionsStopAndRestartWithoutDedicatedWorkers(){
        for(var pass=0;pass<2;pass++){
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var count=0;await using var host=new KestrelTcpHost(new(IPAddress.Loopback,0),async connection=>{if(Interlocked.Increment(ref count)==32){entered.TrySetResult();}try{var result=await connection.Transport.Input.ReadAsync(connection.ConnectionClosed);connection.Transport.Input.AdvanceTo(result.Buffer.End);}catch(OperationCanceledException){}});
            await host.StartAsync();var clients=new List<TcpClient>();try{for(var index=0;index<32;index++){var client=new TcpClient();clients.Add(client);await client.ConnectAsync(IPAddress.Loopback,host.Port);}await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));}finally{foreach(var client in clients){client.Dispose();}}
        }
    }
    [Fact,Trait("Category","Integration")]
    public async Task TcpQuotaRejectsBeforeProtocolWorkAndStopReleasesActiveSlots(){
        var quota=new ConnectionQuota(2);await using var host=new KestrelTcpHost(new(IPAddress.Loopback,0),async connection=>{await connection.Transport.Output.WriteAsync(new byte[]{42});try{var result=await connection.Transport.Input.ReadAsync(connection.ConnectionClosed);connection.Transport.Input.AdvanceTo(result.Buffer.End);}catch(OperationCanceledException){}},quota:quota);await host.StartAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));using var first=new TcpClient();using var second=new TcpClient();using var rejected=new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback,host.Port,timeout.Token);await second.ConnectAsync(IPAddress.Loopback,host.Port,timeout.Token);var bytes=new byte[1];await first.GetStream().ReadExactlyAsync(bytes,timeout.Token);Assert.Equal(42,bytes[0]);await second.GetStream().ReadExactlyAsync(bytes,timeout.Token);Assert.Equal(2,quota.Count);await rejected.ConnectAsync(IPAddress.Loopback,host.Port,timeout.Token);Assert.Equal(0,await rejected.GetStream().ReadAsync(bytes,timeout.Token));await host.DisposeAsync().AsTask().WaitAsync(timeout.Token);Assert.Equal(0,quota.Count);
    }
    [Fact,Trait("Category","Integration")]
    public async Task StopKeepsApplicationAliveUntilItsFinallyEvenAfterTransportGracePeriod(){
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var ended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host=new KestrelTcpHost(new(IPAddress.Loopback,0),async _=>{entered.TrySetResult();try{await release.Task;}finally{ended.TrySetResult();}});await host.StartAsync();using var client=new TcpClient();await client.ConnectAsync(IPAddress.Loopback,host.Port);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));var stopping=host.DisposeAsync().AsTask();
        try{var winner=await Task.WhenAny(stopping,Task.Delay(TimeSpan.FromSeconds(35)));Assert.NotSame(stopping,winner);Assert.False(ended.Task.IsCompleted);}
        finally{release.TrySetResult();await stopping.WaitAsync(TimeSpan.FromSeconds(10));await ended.Task.WaitAsync(TimeSpan.FromSeconds(10));}
    }
}
