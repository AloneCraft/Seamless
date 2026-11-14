using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MemoryPack;
using Microsoft.Data.SqlClient;
using Seamless.Authentication;
using Seamless.Database.Client;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Native;
using Seamless.Security;
using Seamless.Transport.Kestrel;
using AuthenticationWire=Seamless.Database.Client.NativeAuthenticationWire;

namespace Management.Tests;

public sealed partial class NativeTls{
    [Fact]
    public Task AuthenticatedGatewaySharesTlsHandshakeBudgetAcrossNativeHttpsAndTds()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var database=new Fixture(null,start:false);
        var serverId=Guid.NewGuid();
        var databaseId=Guid.NewGuid();
        var roleId=Guid.NewGuid();
        using var manager=new AuthorizationManager(new Seamless.Security.AuthorizationPolicySnapshot(1,[
            new(serverId,null,SecurityResourceKind.Server,"gateway"),
            new(databaseId,serverId,SecurityResourceKind.Database,"fixture")
        ],[new(roleId,"reader",[new(databaseId,SecurityPermission.Discover)])],
            [new(new(issuer.Id,"dev-user"),SecurityPrincipalKind.User,roleId)]));
        using var verifier=OidcTokenVerifier.ForServer(issuer.Settings,32);
        ValueTask<IAuthorizationSession> CreateSession(AuthenticatedIdentity identity,CancellationToken _)=>
            ValueTask.FromResult<IAuthorizationSession>(manager.CreateSession(identity));
        var nativeAuthorization=new NativeGatewayAuthorization(verifier,CreateSession,
            (lease,_)=>lease.Demand(databaseId,SecurityPermission.Discover),
            static(_,_,_,_)=>throw new InvalidOperationException("The probe has no published query."),
            static(_,_)=>throw new InvalidOperationException("The probe has no arguments."));
        var sqlAuthorization=new HttpGatewayAuthorization(verifier,manager,name=>name=="fixture"?databaseId:null);
        using var gateway=new GatewayServer(database.Registry,new(){
            TdsPort=0,NativePort=0,HttpPort=0,MaximumConcurrentTlsHandshakes=1,
            TdsAuthorization=new(certificate,sqlAuthorization,CreateSession),
            NativeCertificate=certificate,NativeAuthorization=nativeAuthorization,NativeAuthenticatedLimits=new(),
            Http=new(){Certificate=certificate,ListenAddress=IPAddress.Loopback}
        },sqlAuthorization);
        gateway.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var token=timeout.Token;
        var credential=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        await using var native=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(credential),port:gateway.NativePort,trustedRoot:certificate);
        await native.ConnectAsync(token);
        Assert.Equal(0,gateway.SharedTlsHandshakeCount);

        int[] ports=[gateway.TdsPort,gateway.NativePort,gateway.HttpPort];
        foreach(var occupiedPort in ports){
            using(var occupied=new TcpClient(AddressFamily.InterNetwork)){
                await occupied.ConnectAsync(IPAddress.Loopback,occupiedPort,token);
                await WaitForHandshakesAsync(1);
                foreach(var competingPort in ports.Where(port=>port!=occupiedPort)){
                    using var rejected=new TcpClient(AddressFamily.InterNetwork);
                    using var rejection=CancellationTokenSource.CreateLinkedTokenSource(token);
                    rejection.CancelAfter(TimeSpan.FromSeconds(3));
                    await rejected.ConnectAsync(IPAddress.Loopback,competingPort,rejection.Token);
                    await AssertPeerClosedAsync(rejected,rejection.Token);
                    Assert.Equal(1,gateway.SharedTlsHandshakeCount);
                }
                if(occupiedPort==gateway.TdsPort){
                    await occupied.GetStream().WriteAsync("not TLS\r\n"u8.ToArray(),token);
                    await AssertPeerClosedAsync(occupied,token);
                }
            }
            await WaitForHandshakesAsync(0);
        }

        var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
        trust.CustomTrustStore.Add(certificate);
        using var http=new HttpClient(new SocketsHttpHandler{UseProxy=false,SslOptions=new(){CertificateChainPolicy=trust}});
        http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",credential);
        using var response=await http.GetAsync($"https://localhost:{gateway.HttpPort}/api/databases",token);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var pin=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N")+".cer");
        await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert),token);
        try{
            var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{gateway.TdsPort}",InitialCatalog="fixture",
                Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
            builder["Server Certificate"]=pin;
            await using var tds=new SqlConnection(builder.ConnectionString){AccessToken=credential};
            await tds.OpenAsync(token);
            using var command=new SqlCommand("SELECT 1",tds){CommandTimeout=5};
            Assert.Equal(1,(int)(await command.ExecuteScalarAsync(token))!);
            await using var reopened=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(credential),port:gateway.NativePort,trustedRoot:certificate);
            await reopened.ConnectAsync(token);
            Assert.Equal(0,gateway.SharedTlsHandshakeCount);
        }finally{File.Delete(pin);}

        async Task WaitForHandshakesAsync(int expected){
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            while(gateway.SharedTlsHandshakeCount!=expected){await Task.Delay(10,deadline.Token);}
        }
    },includeLoopbackAddress:true);

    [Fact]
    public Task AuthenticatedGatewaySharesPendingWriteBudgetAcrossNativeHttpsAndTds()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var database=new Fixture(null,start:false);
        var serverId=Guid.NewGuid();
        var databaseId=Guid.NewGuid();
        var largeId=Guid.NewGuid();
        var smallId=Guid.NewGuid();
        var roleId=Guid.NewGuid();
        using var manager=new AuthorizationManager(new Seamless.Security.AuthorizationPolicySnapshot(1,[
            new(serverId,null,SecurityResourceKind.Server,"gateway"),
            new(databaseId,serverId,SecurityResourceKind.Database,"fixture"),
            new(largeId,databaseId,SecurityResourceKind.Query,"large"),
            new(smallId,databaseId,SecurityResourceKind.Query,"small")
        ],[new(roleId,"reader",[new(databaseId,SecurityPermission.Discover),new(largeId,SecurityPermission.Execute),new(smallId,SecurityPermission.Execute)])],
            [new(new(issuer.Id,"dev-user"),SecurityPrincipalKind.User,roleId)]));
        using var verifier=OidcTokenVerifier.ForServer(issuer.Settings,32);
        var module=new WriteBudgetModule();
        var queryIds=new Dictionary<string,Guid>(StringComparer.Ordinal){{"large",largeId},{"small",smallId}};
        ValueTask<IAuthorizationSession> CreateSession(AuthenticatedIdentity identity,CancellationToken _)=>
            ValueTask.FromResult<IAuthorizationSession>(manager.CreateSession(identity));
        var authorization=new NativeGatewayAuthorization(verifier,CreateSession,
            (lease,_)=>lease.Demand(databaseId,SecurityPermission.Discover),
            (lease,request,implementation,_)=>{
                Assert.Same(module,implementation);
                lease.Demand(queryIds[request.QueryId],SecurityPermission.Execute);
            },static(_,bytes)=>NativeWireValidation.Validate<int>(bytes.Span));
        var sqlAuthorization=new HttpGatewayAuthorization(verifier,manager,name=>name=="fixture"?databaseId:null);
        var limit=module.LargeFrameBytes;
        using var gateway=new GatewayServer(database.Registry,new(){
            TdsEnabled=true,TdsPort=0,NativePort=0,HttpPort=0,
            MaximumPendingWriteBytes=limit,
            TdsAuthorization=new(certificate,sqlAuthorization,CreateSession),
            NativeCertificate=certificate,NativeAuthorization=authorization,
            NativeAuthenticatedLimits=new(){FrameWriteTimeout=TimeSpan.FromSeconds(8)},
            Http=new(){Certificate=certificate,ListenAddress=IPAddress.Loopback}
        },sqlAuthorization);
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        gateway.Native.Register(new("write-budget","","","",version,[Definition<byte[]>("large"),Definition<int>("small")]),module,version);
        gateway.Start();

        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token=timeout.Token;
        var credential=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        var pin=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N")+".cer");
        await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert),token);
        try{
            await using var tds=CreateTdsConnection();
            await tds.OpenAsync(token);
            Assert.Equal(7,await ReadTdsAsync(tds));
            await VerifyHttpsAsync(expectResponse:true);

            using var slowSocket=new TcpClient(AddressFamily.InterNetwork){ReceiveBufferSize=4096};
            await slowSocket.ConnectAsync(IPAddress.Loopback,gateway.NativePort,token);
            using var slow=await OpenReauthenticationTlsAsync(slowSocket,certificate,token);
            await NativeFrame.WriteAsync(slow,AuthenticationWire.Authenticate,AuthenticationWire.Request(credential),token);
            Assert.Equal(AuthenticationWire.Authenticated,(await NativeFrame.ReadAsync(slow,token))!.Value.Kind);
            var request=new NativeQueryRequest("write-budget","large","fixture",Fingerprint,MemoryPackSerializer.Serialize(0),
                ArgumentsContract:NativeWireContract.For<int>(),ResultContract:NativeWireContract.For<byte[]>());
            await NativeFrame.WriteAsync(slow,NativeFrame.Request,MemoryPackSerializer.Serialize(request),token);
            while(true){
                var started=module.LargeRowsStarted;
                await Task.Delay(50,token);
                if(started>0&&started==module.LargeRowsStarted&&gateway.SharedPendingWriteBytes==limit){break;}
            }
            await Task.WhenAll(VerifyHttpsAsync(expectResponse:false),VerifyRejectedTdsAsync(tds));
            Assert.Equal(limit,gateway.SharedPendingWriteBytes);

            await module.Released.Task.WaitAsync(token);
            Assert.Equal(0,gateway.SharedPendingWriteBytes);
            Assert.InRange(module.LargeRowsStarted,1,WriteBudgetModule.MaximumRows-1);
            await VerifyHttpsAsync(expectResponse:true);
            await using var newTds=CreateTdsConnection();
            await newTds.OpenAsync(token);
            Assert.Equal(7,await ReadTdsAsync(newTds));
            await using var native=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(credential),port:gateway.NativePort,trustedRoot:certificate);
            Assert.Equal(8,await native.ExecuteScalarAsync<int,int>("write-budget","small","fixture",Fingerprint,7,token));
        }finally{File.Delete(pin);}

        SqlConnection CreateTdsConnection(){
            var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{gateway.TdsPort}",InitialCatalog="fixture",
                Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
            builder["Server Certificate"]=pin;
            return new(builder.ConnectionString){AccessToken=credential};
        }
        async Task<int> ReadTdsAsync(SqlConnection connection){
            using var command=new SqlCommand("SELECT @value AS [Value]",connection){CommandTimeout=5};
            command.Parameters.AddWithValue("@value",7);
            return (int)(await command.ExecuteScalarAsync(token))!;
        }
        async Task VerifyRejectedTdsAsync(SqlConnection connection){
            var error=await Assert.ThrowsAsync<SqlException>(()=>ReadTdsAsync(connection));
            Assert.NotEqual(-2,error.Number);
            // Gateway が返すエラーの通知は常に重大度16なので、20以上は通知ではなく接続が切られたことを表す。
            // 切断後に SqlConnection.State が Closed へ変わる時機は SqlClient 内部の進み方次第で、全件実行の負荷下では Open のまま返ることがあった。
            Assert.True(error.Class>=20,$"接続が切られず通知で返った: number={error.Number} class={error.Class} message={error.Message}");
        }
        async Task VerifyHttpsAsync(bool expectResponse){
            using var socket=new TcpClient(AddressFamily.InterNetwork);
            await socket.ConnectAsync(IPAddress.Loopback,gateway.HttpPort,token);
            using var stream=await OpenReauthenticationTlsAsync(socket,certificate,token);
            var bytes=Encoding.ASCII.GetBytes($"GET /api/databases HTTP/1.1\r\nHost: localhost\r\nAuthorization: Bearer {credential}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(bytes,token);
            if(expectResponse){
                var prefix=new byte[12];
                await stream.ReadExactlyAsync(prefix,token);
                Assert.Equal("HTTP/1.1 200"u8.ToArray(),prefix);
                await stream.CopyToAsync(Stream.Null,token);
            }else{
                try{Assert.Equal(0,await stream.ReadAsync(new byte[1],token));}
                catch(IOException error)when(error.InnerException is SocketException socketError&&socketError.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted){}
            }
        }
    },includeLoopbackAddress:true);

    private sealed class WriteBudgetModule:INativeQueryModule{
        public const int MaximumRows=2048;
        private readonly byte[] large=MemoryPackSerializer.Serialize(new byte[32768]);
        private int largeRowsStarted;
        public int LargeFrameBytes=>this.large.Length+5;
        public int LargeRowsStarted=>Volatile.Read(ref this.largeRowsStarted);
        public TaskCompletionSource Released{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            if(queryId=="small"){
                yield return MemoryPackSerializer.Serialize(MemoryPackSerializer.Deserialize<int>(arguments.Span)+1);
                yield break;
            }
            try{
                for(var row=0;row<MaximumRows;row++){
                    context.CancellationToken.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref this.largeRowsStarted);
                    yield return this.large;
                }
            }finally{this.Released.TrySetResult();}
        }
    }

    [Fact]
    public Task HttpsAndNativeShareOnlyTheTlsHandshakeBudget()=>ManagementTransport.WithServerCertificate(async certificate=>{
        Assert.Throws<ArgumentOutOfRangeException>(()=>new GatewayServerOptions{MaximumConcurrentTlsHandshakes=0}.ValidateNativeTransport(false));
        var shared=new ConnectionQuota(1);
        var nativeOnly=new ConnectionQuota(1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var finishNative=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var native=new KestrelTcpHost(new(IPAddress.Loopback,0),async connection=>{
            await connection.Transport.Output.WriteAsync(new byte[]{42},connection.ConnectionClosed);
            await finishNative.Task.WaitAsync(connection.ConnectionClosed);
        },certificate:certificate){TlsHandshakeQuota=nativeOnly,SharedTlsHandshakeQuota=shared};
        await using var http=new KestrelHttpHost(new(IPAddress.Loopback,0),context=>{
            context.Response.StatusCode=204;
            return Task.CompletedTask;
        },certificate){SharedTlsHandshakeQuota=shared};
        await native.StartAsync(timeout.Token);
        await http.StartAsync(timeout.Token);
        await VerifyCompetition(native.Port,http.Port,1);
        await VerifyCompetition(http.Port,native.Port,0);

        var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
        trust.CustomTrustStore.Add(certificate);
        using var socket=new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback,native.Port,timeout.Token);
        using var stream=new SslStream(socket.GetStream());
        try{
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=trust},timeout.Token);
            var response=new byte[1];
            await stream.ReadExactlyAsync(response,timeout.Token);
            Assert.Equal((byte)42,response[0]);
            Assert.Equal(0,shared.Count);
            Assert.Equal(0,nativeOnly.Count);

            using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false,SslOptions=new(){CertificateChainPolicy=trust}});
            using var accepted=await client.GetAsync($"https://localhost:{http.Port}/",timeout.Token);
            Assert.Equal(HttpStatusCode.NoContent,accepted.StatusCode);
            Assert.Equal(0,shared.Count);
        }finally{finishNative.TrySetResult();}

        async Task VerifyCompetition(int occupiedPort,int competingPort,int expectedNativeCount){
            using(var occupied=new TcpClient()){
                await occupied.ConnectAsync(IPAddress.Loopback,occupiedPort,timeout.Token);
                while(shared.Count!=1){await Task.Delay(10,timeout.Token);}
                Assert.Equal(expectedNativeCount,nativeOnly.Count);
                using var rejected=new TcpClient();
                await rejected.ConnectAsync(IPAddress.Loopback,competingPort,timeout.Token);
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                await AssertPeerClosedAsync(rejected,deadline.Token);
                Assert.Equal(1,shared.Count);
            }
            while(shared.Count!=0||nativeOnly.Count!=0){await Task.Delay(10,timeout.Token);}
        }
    });

    [Theory][InlineData("disconnect")][InlineData("invalid")][InlineData("deadline")][InlineData("stop")]
    public Task TlsHandshakeCapacityReturnsAfterUnsuccessfulNegotiation(string end)=>ManagementTransport.WithServerCertificate(async certificate=>{
        var quota=new ConnectionQuota(1);
        var calls=0;
        KestrelTcpHost CreateHost()=>new(new(IPAddress.Loopback,0),async context=>{
            Interlocked.Increment(ref calls);
            await context.Transport.Output.WriteAsync(new byte[]{42});
        },certificate:certificate,quota:new ConnectionQuota(4)){TlsHandshakeQuota=quota};
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await using var host=CreateHost();
        await host.StartAsync(timeout.Token);
        using var first=new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback,host.Port,timeout.Token);
        async Task WaitForCount(int expected){
            while(quota.Count!=expected){await Task.Delay(10,timeout.Token);}
        }
        await WaitForCount(1);
        using(var rejected=new TcpClient()){
            await rejected.ConnectAsync(IPAddress.Loopback,host.Port,timeout.Token);
            using var shortDeadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await AssertPeerClosedAsync(rejected,shortDeadline.Token);
        }
        Assert.Equal(1,quota.Count);
        Assert.Equal(0,Volatile.Read(ref calls));
        switch(end){
            case "disconnect":first.Dispose();break;
            case "invalid":
                await first.GetStream().WriteAsync("not TLS\r\n"u8.ToArray(),timeout.Token);
                await AssertPeerClosedAsync(first,timeout.Token);
                break;
            case "deadline":await AssertPeerClosedAsync(first,timeout.Token);break;
            case "stop":await host.DisposeAsync().AsTask().WaitAsync(timeout.Token);break;
        }
        await WaitForCount(0);
        Assert.Equal(0,Volatile.Read(ref calls));
        if(end=="stop"){
            await using var restarted=CreateHost();
            await restarted.StartAsync(timeout.Token);
            await CheckTlsAsync(restarted.Port);
        }else{
            await CheckTlsAsync(host.Port);
        }
        Assert.Equal(1,Volatile.Read(ref calls));
        Assert.Equal(0,quota.Count);

        async Task CheckTlsAsync(int port){
            using var client=new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback,port,timeout.Token);
            using var stream=new SslStream(client.GetStream());
            var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
            trust.CustomTrustStore.Add(certificate);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=trust},timeout.Token);
            var response=new byte[1];
            await stream.ReadExactlyAsync(response,timeout.Token);
            Assert.Equal((byte)42,response[0]);
        }
    });
    private static async Task AssertPeerClosedAsync(TcpClient client,CancellationToken cancellationToken){
        try{
            Assert.Equal(0,await client.GetStream().ReadAsync(new byte[1],cancellationToken));
        }catch(IOException error) when(error.InnerException is SocketException socket&&socket.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted){
            // KestrelのAbortはOSによってEOFまたはTCPリセットになる。
        }
    }
    [Fact]public Task CompletedHandshakeDoesNotHoldTheSlotDuringAuthentication()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessions=0;
        using var host=new AuthenticatedFixture(certificate,issuer,sessionHook:async(session,token)=>{
            if(Interlocked.Increment(ref sessions)==1){entered.TrySetResult();await release.Task.WaitAsync(token);}
            return session;
        },maximumAuthentications:2,limits:new NativeServerLimits{MaximumConnections=4,MaximumConcurrentHandshakes=1});
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var first=host.Client();
        var opening=first.ConnectAsync(timeout.Token);
        try{
            await entered.Task.WaitAsync(timeout.Token);
            await using var second=host.Client();
            Assert.Equal(8,await second.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
            Assert.False(opening.IsCompleted);
            Assert.Equal(2,Volatile.Read(ref sessions));
        }finally{release.TrySetResult();}
        await opening;
        Assert.Equal(9,await first.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,8,timeout.Token));
    });
}
