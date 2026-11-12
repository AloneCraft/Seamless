using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Seamless.Authentication;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;
using Seamless.Security;
using Seamless.Transport.Kestrel;
using Server=Seamless.Database.Gateway.Http.HttpServer;

namespace Seamless.Database.Gateway.Tests;

public sealed class HttpServerTests{
    [Fact,Trait("Category","Integration")]
    public async Task ConnectionLimitCountsIdleKeepAliveAndReleasesTheSlotOnDisconnect(){
        Assert.Throws<ArgumentOutOfRangeException>(()=>new HttpServerOptions{MaximumConnections=0});
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var adapter=new InProcessDatabaseAdapter(database);
        using var server=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){
            ListenAddress=IPAddress.Loopback,
            MaximumConnections=1,
            MaximumConcurrentRequests=2
        });
        server.Start();
        var address=new Uri($"http://127.0.0.1:{server.Port}");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var other=new HttpClient(new SocketsHttpHandler{UseProxy=false}){BaseAddress=address};
        using(var holder=new HttpClient(new SocketsHttpHandler{UseProxy=false}){BaseAddress=address}){
            using var completed=await holder.GetAsync("/api/databases",timeout.Token);
            Assert.Equal(HttpStatusCode.OK,completed.StatusCode);
            await Assert.ThrowsAsync<HttpRequestException>(async()=>{
                using var rejected=await other.GetAsync("/api/databases",timeout.Token);
            });
            using var reused=await holder.GetAsync("/api/databases",timeout.Token);
            Assert.Equal(HttpStatusCode.OK,reused.StatusCode);
        }
        while(true){
            try{
                using var next=await other.GetAsync("/api/databases",timeout.Token);
                Assert.Equal(HttpStatusCode.OK,next.StatusCode);
                break;
            }catch(HttpRequestException){
                await Task.Delay(25,timeout.Token);
            }
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task ResponseWriteTimeoutReleasesRequestSlotWhenClientStopsReading(){
        const int payloadBytes=16*1024*1024;
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var adapter=new InProcessDatabaseAdapter(database);
        using var server=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){
            ListenAddress=IPAddress.Loopback,
            MaximumConcurrentRequests=1,
            ResponseWriteTimeout=TimeSpan.FromSeconds(1)
        });
        server.Start();
        var address=new Uri($"http://127.0.0.1:{server.Port}");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var slow=new HttpClient(new SocketsHttpHandler{UseProxy=false,ConnectCallback=async(context,token)=>{
            var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp){ReceiveBufferSize=4096};
            try{
                await socket.ConnectAsync(context.DnsEndPoint,token);
                return new NetworkStream(socket,ownsSocket:true);
            }catch{socket.Dispose();throw;}
        }}){BaseAddress=address};
        using var other=new HttpClient(new SocketsHttpHandler{UseProxy=false}){BaseAddress=address};
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){
            Content=new StringContent($"{{\"query\":\"SELECT REPLICATE(CAST('x' AS varchar(max)),{payloadBytes}) AS payload\"}}")
        };
        using var response=await slow.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Content.Headers.ContentLength>=payloadBytes);

        using var slotDeadline=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        slotDeadline.CancelAfter(TimeSpan.FromSeconds(4));
        while(true){
            using var next=await other.GetAsync("/api/databases",slotDeadline.Token);
            if(next.StatusCode==HttpStatusCode.OK){break;}
            Assert.Equal(HttpStatusCode.ServiceUnavailable,next.StatusCode);
            await Task.Delay(25,slotDeadline.Token);
        }

        using var body=await response.Content.ReadAsStreamAsync(timeout.Token);
        await Assert.ThrowsAnyAsync<IOException>(()=>body.CopyToAsync(Stream.Null,timeout.Token));
    }

    [Fact,Trait("Category","Integration")]
    public async Task ResponseBudgetAbortsDeliveryWithoutUndoingCommittedSql(){
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var catalog=database.RuntimeSql;
        using(var transaction=catalog.BeginTransaction()){
            catalog.CreateTable("dbo","pending_output",[new RuntimeSqlColumn("id","int",false,IsPrimaryKey:true)]);
            transaction.Commit();
        }
        var adapter=new InProcessDatabaseAdapter(database);
        var budget=new FrameBufferQuota(16);
        using var server=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){ListenAddress=IPAddress.Loopback,ResponseWriteTimeout=TimeSpan.FromSeconds(2)},null,budget);
        server.Start();
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false}){BaseAddress=new($"http://127.0.0.1:{server.Port}"),Timeout=TimeSpan.FromSeconds(10)};
        using(var occupied=budget.TryReserve(16)){
            Assert.NotNull(occupied);
            await Assert.ThrowsAsync<HttpRequestException>(async()=>{
                using var ignored=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"INSERT INTO dbo.pending_output (id) VALUES (7)\"}"));
            });
        }
        using(var read=database.BeginReadSession()){
            var row=Assert.Single(catalog.ReadRows("dbo","pending_output",catalog.SchemaRevision));
            Assert.Equal(7,Assert.Single(row.Values));
        }
        using var response=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"SELECT id FROM dbo.pending_output\"}"));
        response.EnsureSuccessStatusCode();
        var bytes=await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length>16);
        Assert.Equal(bytes.LongLength,response.Content.Headers.ContentLength);
        using var result=global::System.Text.Json.JsonDocument.Parse(bytes);
        Assert.Equal(7,Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public async Task BufferedSqlBodyBudgetRejectsCompetingRequestsAndReturnsTheReservation(bool chunked,bool shared){
        const int limit=96;
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var adapter=new InProcessDatabaseAdapter(database);
        using var server=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){
            ListenAddress=IPAddress.Loopback,MaximumBufferedRequestBodyBytes=shared?null:limit
        },null,null,sharedRequestBodies:shared?new(limit):null);
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false,Expect100ContinueTimeout=TimeSpan.FromSeconds(30)}){BaseAddress=new($"http://127.0.0.1:{server.Port}")};
        using var created=await client.PostAsync("/api/session",null,timeout.Token);
        created.EnsureSuccessStatusCode();
        using var session=global::System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync(timeout.Token));
        var sessionId=session.RootElement.GetProperty("id").GetString();
        var body="{\"query\":\"SELECT 1\"}".PadRight(64);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var holding=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){Content=new PausedBody(global::System.Text.Encoding.UTF8.GetBytes(body),entered,release)};
        holding.Headers.ExpectContinue=true;
        var first=client.SendAsync(holding,timeout.Token);
        try{
            await entered.Task.WaitAsync(timeout.Token);
            using var competing=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){Content=new StringContent(body)};
            competing.Headers.TransferEncodingChunked=chunked;
            competing.Headers.Add("X-Seamless-Session",sessionId);
            using var rejected=await client.SendAsync(competing,timeout.Token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,rejected.StatusCode);
        }finally{release.TrySetResult();}
        using var completed=await first;
        Assert.Equal(HttpStatusCode.OK,completed.StatusCode);

        using var oversized=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){Content=new StringContent(body.PadRight(limit+1))};
        oversized.Headers.TransferEncodingChunked=chunked;
        using var tooLarge=await client.SendAsync(oversized,timeout.Token);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,tooLarge.StatusCode);

        using var next=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){
            Content=new StringContent("{\"query\":\"SELECT 1\"}",global::System.Text.Encoding.Unicode)
        };
        next.Headers.TransferEncodingChunked=chunked;
        next.Headers.Add("X-Seamless-Session",sessionId);
        using var accepted=await client.SendAsync(next,timeout.Token);
        Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);
    }

    [Fact,Trait("Category","Integration")]
    public async Task GatewaySqlBodyAndTdsShareTheBufferedRequestBudget(){
        const int limit=4096;
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        using var server=new GatewayServer(new InProcessDatabaseAdapter(database),new(){
            TdsPort=0,NativePort=0,HttpPort=0,MaximumBufferedRequestBytes=limit,
            Http=new(){ListenAddress=IPAddress.Loopback,MaximumBufferedRequestBodyBytes=limit*2}
        });
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false,Expect100ContinueTimeout=TimeSpan.FromSeconds(30)}){
            BaseAddress=new($"http://127.0.0.1:{server.HttpPort}")
        };
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var holding=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){
            Content=new PausedBody(global::System.Text.Encoding.UTF8.GetBytes("{\"query\":\"SELECT 1\"}".PadRight(limit)),entered,release)
        };
        holding.Headers.ExpectContinue=true;
        var first=client.SendAsync(holding,timeout.Token);
        try{
            await entered.Task.WaitAsync(timeout.Token);
            Assert.False(await TdsPreLoginRespondsAsync());
        }finally{release.TrySetResult();}
        using var completed=await first;
        Assert.Equal(HttpStatusCode.OK,completed.StatusCode);
        while(!await TdsPreLoginRespondsAsync()){await Task.Delay(25,timeout.Token);}

        async Task<bool> TdsPreLoginRespondsAsync(){
            using var socket=new TcpClient();
            await socket.ConnectAsync(IPAddress.Loopback,server.TdsPort,timeout.Token);
            var stream=socket.GetStream();
            try{
                await stream.WriteAsync(new byte[]{TdsConstants.PacketTypePreLogin,1,0,9,0,0,1,0,0},timeout.Token);
                var response=new byte[1];
                if(await stream.ReadAsync(response,timeout.Token)==0){return false;}
                Assert.Equal((byte)4,response[0]);
                return true;
            }catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){
                return false;
            }
        }
    }

    private sealed class PausedBody(byte[] bytes,TaskCompletionSource entered,TaskCompletionSource release):HttpContent{
        protected override bool TryComputeLength(out long length){length=bytes.Length;return true;}
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>this.SerializeToStreamAsync(stream,context,CancellationToken.None);
        protected override async Task SerializeToStreamAsync(Stream stream,TransportContext? context,CancellationToken cancellationToken){
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            await stream.WriteAsync(bytes,cancellationToken);
        }
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BodyLimitRejectsOversizedSqlWithoutClosingSession(bool chunked){
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var adapter=new InProcessDatabaseAdapter(database);
        using var server=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){ListenAddress=IPAddress.Loopback,MaximumRequestBodyBytes=64});
        server.Start();
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false}){BaseAddress=new($"http://127.0.0.1:{server.Port}"),Timeout=TimeSpan.FromSeconds(10)};
        using var created=await client.PostAsync("/api/session",null);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        using var session=global::System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-Seamless-Session",session.RootElement.GetProperty("id").GetString());
        using var oversized=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){Content=new StringContent("{\"query\":\"SELECT 1"+new string(' ',80)+"\"}")};
        oversized.Headers.TransferEncodingChunked=chunked;
        using var rejected=await client.SendAsync(oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,rejected.StatusCode);
        using var accepted=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"SELECT 1\"}"));
        Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);
    }
    [Fact,Trait("Category","Integration")]
    public async Task DefaultListenerKeepsIPv4AndIPv6Reachability(){
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});var adapter=new InProcessDatabaseAdapter(database);using var server=new Server(0,adapter,new SqlExecutionEngine(adapter));server.Start();using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(10)};using var ipv4=await client.GetAsync($"http://127.0.0.1:{server.Port}/api/tables");Assert.Equal(HttpStatusCode.OK,ipv4.StatusCode);
        if(global::System.Net.Sockets.Socket.OSSupportsIPv6){using var ipv6=await client.GetAsync($"http://[::1]:{server.Port}/api/tables");Assert.Equal(HttpStatusCode.OK,ipv6.StatusCode);}
    }
    [Fact,Trait("Category","Integration")]
    public async Task HttpsDynamicPortAndHostNameValidation(){
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);var names=new SubjectAlternativeNameBuilder();names.AddDnsName("localhost");request.CertificateExtensions.Add(names.Build());request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));
        // Windows Schannel用の一時ユーザーキーは利用者の許可済み範囲。試験後に削除する。
        var certificate=X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);string? keyName=null;CngProvider? provider=null;
        if(OperatingSystem.IsWindows()){using var privateKey=certificate.GetRSAPrivateKey();if(privateKey is RSACng cng){keyName=cng.Key.KeyName;provider=cng.Key.Provider;}}
        try{
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});var adapter=new InProcessDatabaseAdapter(database);using var server=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate});
        using var handler=new SocketsHttpHandler{SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck}}};handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(certificate);using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(10)};
        for(var pass=0;pass<2;pass++){
            server.Start();Assert.InRange(server.Port,1,65535);using var response=await client.GetAsync($"https://localhost:{server.Port}/api/tables");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.StartsWith("[",await response.Content.ReadAsStringAsync());await Assert.ThrowsAsync<HttpRequestException>(async()=>{using var mismatch=await client.GetAsync($"https://127.0.0.1:{server.Port}/api/tables");});await server.StopAsync();
        }
        var issuer=Guid.NewGuid();var serverId=Guid.NewGuid();
        using var verifier=new OidcTokenVerifier(new(issuer,"http://127.0.0.1:1","test-http-api","test-client",DevelopmentLoopback:true));
        using var policy=new AuthorizationManager(new(1,[new(serverId,null,SecurityResourceKind.Server,"test")],[],[]));
        var protection=new HttpGatewayAuthorization(verifier,policy,static _=>null);
        Assert.Throws<ArgumentException>(()=>{using var insecure=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){ListenAddress=IPAddress.Loopback},protection);});
        using var protectedServer=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate},protection);
        protectedServer.Start();
        using var unauthenticated=await client.GetAsync($"https://localhost:{protectedServer.Port}/api/databases");
        Assert.Equal(HttpStatusCode.Unauthorized,unauthenticated.StatusCode);
        Assert.Contains(unauthenticated.Headers.WwwAuthenticate,header=>header.Scheme=="Bearer");
        using var malformed=new HttpRequestMessage(HttpMethod.Get,$"https://localhost:{protectedServer.Port}/api/session");
        malformed.Headers.TryAddWithoutValidation("Authorization","Bearer invalid token");
        using var rejected=await client.SendAsync(malformed);
        Assert.Equal(HttpStatusCode.Unauthorized,rejected.StatusCode);
        }finally{certificate.Dispose();if(OperatingSystem.IsWindows()&&keyName is not null&&provider is not null){if(CngKey.Exists(keyName,provider)){using var owned=CngKey.Open(keyName,provider);owned.Delete();}Assert.False(CngKey.Exists(keyName,provider));}}
    }

    [Fact]
    public async Task HttpSessionRequiresMatchingSubjectAndIssuer(){
        using var sessions=new HttpSessionStore(new());
        var subject=new SecuritySubjectId(Guid.NewGuid(),"alice");
        var token=sessions.Create("fixture",subject);
        foreach(var other in new SecuritySubjectId?[]{null,new(subject.AuthorityId,"bob"),new(Guid.NewGuid(),"alice")}){
            var error=await Assert.ThrowsAsync<HttpSessionException>(async()=>{using var rejected=await sessions.AcquireAsync(token,CancellationToken.None,other);});
            Assert.Equal(404,error.Status);
        }
        using var owned=await sessions.AcquireAsync(token,CancellationToken.None,subject);
        Assert.Equal("fixture",owned.Session.DatabaseName);
    }
}
