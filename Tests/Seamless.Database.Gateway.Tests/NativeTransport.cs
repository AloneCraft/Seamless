using System.Buffers.Binary;
using System.Net.Sockets;
using System.Reflection;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Tests;

public sealed class NativeTransport{
    private sealed class Module:INativeQueryModule{
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Released=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            if(queryId=="wait"){
                this.Entered.TrySetResult();try{context.CancellationToken.WaitHandle.WaitOne();context.CancellationToken.ThrowIfCancellationRequested();}finally{this.Released.TrySetResult();}
            }
            yield return arguments.ToArray();
        }
    }
    private sealed class Fixture:IDisposable{
        private readonly RuntimeSqlGeneratedRoot database=new(new(){Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        public Module Module{get;}=new();
        public NativeQueryServer Server{get;}
        public Fixture(){var databases=new DatabaseRegistry();databases.Register("fixture",new InProcessDatabaseAdapter(this.database),schemaFingerprint:"wire");this.Server=new(databases,0);var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];this.Server.Register(new("wire","","","",version,[new("echo","fixture","wire","byte[]","byte[]"),new("wait","fixture","wire","byte[]","byte[]")]),this.Module,version);this.Server.Start();}
        public void Dispose(){this.Server.Dispose();this.database.Dispose();}
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
    [Fact,Trait("Category","Integration")]
    public async Task ServerStopCancelsAnExecutingQueryAndReleasesItsEnumerator(){
        using var fixture=new Fixture();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await using var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port);var operation=client.ExecuteScalarAsync<byte[],byte[]>("wire","wait","fixture","wire",[],timeout.Token);await fixture.Module.Entered.Task.WaitAsync(timeout.Token);await fixture.Server.StopAsync().WaitAsync(timeout.Token);await fixture.Module.Released.Task.WaitAsync(timeout.Token);await Assert.ThrowsAnyAsync<IOException>(async()=>await operation);
    }
}
