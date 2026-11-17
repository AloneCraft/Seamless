using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tests;

public sealed class TdsLoopbackTests{
    [Theory]
    [InlineData(256,false)]
    [InlineData(257,true)]
    public async Task TdsAndNativeShareReceiveBudgetIncludingTheTdsFinalCopy(int maximumBytes,bool finalCopyFits){
        var budget=new FrameBufferQuota(maximumBytes);
        var tds=new Pipe();
        var blocked=new Pipe();
        var recovered=new Pipe();
        var output=new Pipe();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var nativeFragment=new byte[]{17,0,0,0,9};
        LengthPrefixedPipe Native(Pipe input)=>new(new Duplex(input.Reader,output.Writer),64,false,null,null,budget);
        try{
            await tds.Writer.WriteAsync(new byte[]{0x01,0,0,9,0,0,1,0,42},timeout.Token);
            var message=TdsPacketReader.ReadMessageAsync(tds.Reader,timeout.Token,null,budget).AsTask();
            Assert.False(message.IsCompleted);
            await blocked.Writer.WriteAsync(nativeFragment,timeout.Token);
            await Assert.ThrowsAsync<IOException>(async()=>await Native(blocked).ReadAsync(bytes=>bytes.ToArray(),timeout.Token));

            await tds.Writer.WriteAsync(new byte[]{0x01,1,0,8,0,0,2,0},timeout.Token);
            if(finalCopyFits){
                var completed=Assert.IsType<TdsMessage>(await message);
                Assert.Equal(new byte[]{42},completed.Payload);
            }else{
                await Assert.ThrowsAsync<TdsPacketReader.ReceiveBufferLimitExceeded>(()=>message);
            }
            await recovered.Writer.WriteAsync(nativeFragment,timeout.Token);
            var resumed=Native(recovered).ReadAsync(bytes=>bytes.ToArray(),timeout.Token).AsTask();
            Assert.False(resumed.IsCompleted);
            await recovered.Writer.WriteAsync(new byte[16],timeout.Token);
            var bytes=Assert.IsType<byte[]>(await resumed);
            Assert.Equal(17,bytes.Length);
            Assert.Equal(9,bytes[0]);
        }finally{
            foreach(var pipe in new[]{tds,blocked,recovered,output}){
                await pipe.Writer.CompleteAsync();
                await pipe.Reader.CompleteAsync();
            }
        }
    }

    private sealed record Duplex(PipeReader Input,PipeWriter Output):IDuplexPipe;

    [Fact]
    public async Task TdsWriteSharesNativeWriteBudgetAndDoesNotAppendARejectedPacket(){
        var budget=new FrameBufferQuota(4101);
        var incoming=new Pipe();
        var native=new Pipe(new(pauseWriterThreshold:32,resumeWriterThreshold:16));
        var tds=new Pipe();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancelled=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var frames=new LengthPrefixedPipe(new Duplex(incoming.Reader,native.Writer),8192,false,null,null,null,budget);
        try{
            var writing=frames.WriteAsync(1,new byte[4096],cancelled.Token).AsTask();
            Assert.False(writing.IsCompleted);
            var tdsWriter=new TdsPacketWriter(tds.Writer,null,budget);
            await Assert.ThrowsAsync<TdsPacketWriter.WriteBudgetExceeded>(async()=>await tdsWriter.SendPreLoginResponseAsync(timeout.Token));
            Assert.False(tds.Reader.TryRead(out _));

            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>writing);
            await tdsWriter.SendPreLoginResponseAsync(timeout.Token);
            var packet=await tds.Reader.ReadAsync(timeout.Token);
            try{Assert.Equal(47,packet.Buffer.Length);}
            finally{tds.Reader.AdvanceTo(packet.Buffer.End);}
        }finally{
            foreach(var pipe in new[]{incoming,native,tds}){
                await pipe.Writer.CompleteAsync();
                await pipe.Reader.CompleteAsync();
            }
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task GatewayConnectionLimitIsSharedAcrossTdsHttpAndNative(){
        Assert.Throws<ArgumentOutOfRangeException>(()=>new GatewayServerOptions{MaximumConnections=0}.ValidateNativeTransport(false));
        using var server=new GatewayServer(new EmptyAdapter(),new(){
            TdsPort=0,NativePort=0,HttpPort=0,MaximumConnections=1,Http=new(){ListenAddress=IPAddress.Loopback}
        });
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var address=new Uri($"http://127.0.0.1:{server.HttpPort}");
        using(var http=new HttpClient(new SocketsHttpHandler{UseProxy=false}){BaseAddress=address}){
            await using(var tds=new SqlConnection(Options("127.0.0.1",server.TdsPort).ConnectionString)){
                await tds.OpenAsync(timeout.Token);
                await RejectAsync(server.HttpPort);
                await RejectAsync(server.NativePort);
                using var command=tds.CreateCommand();
                command.CommandText="SELECT DB_NAME()";
                Assert.Equal("fixture",await command.ExecuteScalarAsync(timeout.Token));
            }
            while(true){
                try{
                    using var response=await http.GetAsync("/api/databases",timeout.Token);
                    Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                    Assert.NotEqual(true,response.Headers.ConnectionClose);
                    break;
                }catch(HttpRequestException){
                    await Task.Delay(25,timeout.Token);
                }
            }
            await RejectAsync(server.TdsPort);
            await RejectAsync(server.NativePort);
            using var reused=await http.GetAsync("/api/databases",timeout.Token);
            Assert.Equal(HttpStatusCode.OK,reused.StatusCode);
        }
        await server.StopAsync();
        server.Start();
        await using var recovered=new SqlConnection(Options("127.0.0.1",server.TdsPort).ConnectionString);
        await recovered.OpenAsync(timeout.Token);

        async Task RejectAsync(int port){
            using var socket=new TcpClient();
            try{
                await socket.ConnectAsync(IPAddress.Loopback,port,timeout.Token);
                Assert.Equal(0,await socket.GetStream().ReadAsync(new byte[1],timeout.Token));
            }catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task BufferedTdsRequestLimitClosesOnlyTheExcessConnection(){
        const int limit=4096;
        using var server=new GatewayServer(new EmptyAdapter(),new(){TdsPort=0,NativePort=0,HttpPort=0,
            Http=new(){ListenAddress=IPAddress.Loopback},MaximumBufferedRequestBytes=limit});
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var idle=new SqlConnection(Options("127.0.0.1",server.TdsPort).ConnectionString);
        await idle.OpenAsync(timeout.Token);
        using var excess=new TcpClient();
        await excess.ConnectAsync(IPAddress.Loopback,server.TdsPort,timeout.Token);
        var packet=new byte[limit+9];
        packet[0]=TdsConstants.PacketTypePreLogin;
        packet[1]=1;
        global::System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2),(ushort)packet.Length);
        await excess.GetStream().WriteAsync(packet,timeout.Token);
        try{Assert.Equal(0,await excess.GetStream().ReadAsync(new byte[1],timeout.Token));}
        catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}
        using var command=idle.CreateCommand();
        command.CommandText="SELECT DB_NAME()";
        Assert.Equal("fixture",await command.ExecuteScalarAsync(timeout.Token));
    }

    [Fact]
    public async Task SegmentedHeaderAndCoalescedMessagesPreservePayloads(){
        var first=new PacketSegment(new byte[]{0x01,0x01,0x00});
        var second=new PacketSegment(new byte[]{10,0,0,1,0,42});
        var last=new PacketSegment(new byte[]{
            43,
            0x03,0x00,0,9,0,0,1,0,7,
            0x03,0x01,0,9,0,0,2,0,8
        });
        first.Link(second);
        second.Link(last);
        var reader=PipeReader.Create(new ReadOnlySequence<byte>(first,0,last,last.Memory.Length));
        try{
            var single=Assert.IsType<TdsMessage>(await TdsPacketReader.ReadMessageAsync(reader));
            Assert.Equal((byte)0x01,single.PacketType);
            Assert.Equal(new byte[]{42,43},single.Payload);
            var fragmented=Assert.IsType<TdsMessage>(await TdsPacketReader.ReadMessageAsync(reader));
            Assert.Equal((byte)0x03,fragmented.PacketType);
            Assert.Equal(new byte[]{7,8},fragmented.Payload);
            Assert.Null(await TdsPacketReader.ReadMessageAsync(reader));
        }finally{
            await reader.CompleteAsync();
        }
    }

    [Fact]
    public async Task RejectsChangedPacketTypeAndReturnsPipeBuffer(){
        var reader=PipeReader.Create(new ReadOnlySequence<byte>(new byte[]{
            0x00,0x00,0,8,0,0,1,0,
            0x03,0x01,0,8,0,0,2,0
        }));
        try{
            await Assert.ThrowsAsync<InvalidDataException>(async()=>{await TdsPacketReader.ReadMessageAsync(reader);});
            Assert.True(reader.TryRead(out var remaining));
            Assert.Equal(8,remaining.Buffer.Length);
            reader.AdvanceTo(remaining.Buffer.End);
        }finally{
            await reader.CompleteAsync();
        }
    }


    private sealed class PacketSegment:ReadOnlySequenceSegment<byte>{
        public PacketSegment(byte[] bytes){this.Memory=bytes;}
        public void Link(PacketSegment next){
            next.RunningIndex=this.RunningIndex+this.Memory.Length;
            this.Next=next;
        }
    }


    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteMessageDeadlineKeepsOtherLoggedInConnections(bool fullPacket){
        var adapter=new EmptyAdapter();
        using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter),null,null,null,null,TimeSpan.FromMilliseconds(300));
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var idle=new SqlConnection(Options("127.0.0.1",server.Port).ConnectionString);
        await idle.OpenAsync(timeout.Token);
        using var stalled=new TcpClient();
        await stalled.ConnectAsync(IPAddress.Loopback,server.Port,timeout.Token);
        var stream=stalled.GetStream();
        var login=new byte[80];login[0]=0x10;login[1]=1;login[3]=80;login[6]=1;
        await stream.WriteAsync(login,timeout.Token);
        var header=new byte[8];
        do{
            await stream.ReadExactlyAsync(header,timeout.Token);
            var size=global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
            Assert.True(size>=header.Length);
            await stream.ReadExactlyAsync(new byte[size-header.Length],timeout.Token);
        }while((header[1]&1)==0);
        byte[] incomplete=[1,0,0,8,0,0,1,0];
        await stream.WriteAsync(incomplete.AsMemory(0,fullPacket?incomplete.Length:1),timeout.Token);
        try{Assert.Equal(0,await stream.ReadAsync(new byte[1],timeout.Token));}
        catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}
        using var command=idle.CreateCommand();command.CommandText="SELECT DB_NAME()";
        Assert.Equal("fixture",await command.ExecuteScalarAsync(timeout.Token));
    }
    [Fact]
    public async Task PacketWriteDeadlineInterruptsBlockedFlush(){
        var pipe=new global::System.IO.Pipelines.Pipe(new(pauseWriterThreshold:16,resumeWriterThreshold:8));
        var writer=new TdsPacketWriter(pipe.Writer,TimeSpan.FromMilliseconds(100));
        using var safety=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try{
            await Assert.ThrowsAsync<TdsPacketWriter.WriteDeadlineExceeded>(async()=>await writer.SendPreLoginResponseAsync(safety.Token));
            Assert.False(safety.IsCancellationRequested);
        }finally{
            await pipe.Writer.CompleteAsync();
            await pipe.Reader.CompleteAsync();
        }
    }
    [Fact,Trait("Category","Integration")]
    public async Task LoginDeadlineClosesPreLoginStallButKeepsLoggedInSession(){
        var adapter=new EmptyAdapter();
        using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter),null,null,TimeSpan.FromSeconds(2));
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var loggedIn=new SqlConnection(Options("127.0.0.1",server.Port).ConnectionString);
        await loggedIn.OpenAsync(timeout.Token);
        using var stalled=new TcpClient();
        await stalled.ConnectAsync(IPAddress.Loopback,server.Port,timeout.Token);
        var stream=stalled.GetStream();
        await stream.WriteAsync(new byte[]{0x12,1,0,9,0,0,1,0,0xff},timeout.Token);
        var header=new byte[8];
        await stream.ReadExactlyAsync(header,timeout.Token);
        var length=global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
        Assert.True(length>header.Length);
        await stream.ReadExactlyAsync(new byte[length-header.Length],timeout.Token);
        try{Assert.Equal(0,await stream.ReadAsync(new byte[1],timeout.Token));}
        catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}
        using var command=loggedIn.CreateCommand();command.CommandText="SELECT DB_NAME()";
        Assert.Equal("fixture",await command.ExecuteScalarAsync(timeout.Token));
    }
    [Fact,Trait("Category","Integration")]
    public async Task ConnectionLimitIsSharedByLoopbackListeners(){
        var adapter=new EmptyAdapter();
        using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter),null,1);
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var held=new SqlConnection(Options("127.0.0.1",server.Port).ConnectionString);
        await held.OpenAsync(timeout.Token);
        using(var rejected=new TcpClient()){
            var address=IPAddress.Loopback;
            if(Socket.OSSupportsIPv6){address=IPAddress.IPv6Loopback;}
            await rejected.ConnectAsync(address,server.Port,timeout.Token);
            try{Assert.Equal(0,await rejected.GetStream().ReadAsync(new byte[1],timeout.Token));}
            catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}
        }
        using var command=held.CreateCommand();command.CommandText="SELECT DB_NAME()";
        Assert.Equal("fixture",await command.ExecuteScalarAsync(timeout.Token));
        await server.StopAsync();
        server.Start();
        await Check("127.0.0.1",server.Port);
    }
    [Fact,Trait("Category","Integration")]
    public async Task LoopbackAddressesAndRestart(){
        var adapter=new EmptyAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));
        for(var pass=0;pass<2;pass++){
            server.Start();Assert.InRange(server.Port,1,65535);
            await Check("127.0.0.1",server.Port);
            if(Socket.OSSupportsIPv6){await Check("[::1]",server.Port);}
            var settings=Options("127.0.0.1",server.Port);await using var held=new SqlConnection(settings.ConnectionString);await held.OpenAsync();
            await server.StopAsync();
        }
    }
    private static SqlConnectionStringBuilder Options(string host,int port)=>new(){DataSource=$"tcp:{host},{port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=5};
    private static async Task Check(string host,int port){
        var options=Options(host,port);await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
        using var command=connection.CreateCommand();command.CommandText="SELECT DB_NAME()";
        Assert.Equal("fixture",await command.ExecuteScalarAsync());
    }
    [Fact,Trait("Category","Integration")]
    public async Task IPv6BindFailureDoesNotLeaveIPv4Listener(){
        if(!Socket.OSSupportsIPv6){return;}
        var occupied=new TcpListener(IPAddress.IPv6Loopback,0);occupied.Server.DualMode=false;occupied.ExclusiveAddressUse=true;occupied.Start();
        var port=((IPEndPoint)occupied.LocalEndpoint).Port;var adapter=new EmptyAdapter();using var server=new TdsServer(port,adapter,new SqlExecutionEngine(adapter));
        try{
            Assert.Throws<SocketException>(()=>server.Start());
            var probe=new TcpListener(IPAddress.Loopback,port);try{probe.Start();}finally{probe.Stop();}
        }finally{occupied.Stop();}
        server.Start();await server.StopAsync();
    }
    private sealed class EmptyAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata>GetTables()=>[];public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
