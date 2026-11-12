using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using MemoryPack;
using Seamless.Authentication;
using Seamless.Database.Client;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Native;
using Seamless.Security;
using AuthenticationWire=Seamless.Database.Client.NativeAuthenticationWire;

namespace Management.Tests;

public sealed partial class NativeTls{
    [Fact]
    public Task ConnectionPolicyProfilesResolveAfterAuthenticationAndRespectExplicitOverrides()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer);
        var keepAlive=new NativeConnectionPolicy(NativeConnectionLifetime.KeepAlive);
        var idle=new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseWhenIdle,TimeSpan.FromSeconds(30));
        var perOperation=new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseAfterOperation);
        var profiles=new Dictionary<string,NativeConnectionPolicy>(StringComparer.OrdinalIgnoreCase){{"interactive",idle},{"batch",perOperation}};
        Assert.Throws<ArgumentException>(()=>new GatewayServerOptions{
            NativeConnectionPolicyProfiles=new(){{"bad profile",idle}}
        }.ValidateNativeTransport(false));
        using var gateway=new GatewayServer(host.Fixture.Registry,new(){
            TdsEnabled=false,HttpEnabled=false,NativePort=0,NativeCertificate=certificate,
            NativeAuthorization=host.Authorization,NativeAuthenticatedLimits=new(),
            NativeRecommendedConnectionPolicy=keepAlive,NativeConnectionPolicyProfiles=profiles
        });
        // Gatewayは起動候補を固定し、呼び出し元での構成変更を稼働中の接続へ反映しない。
        profiles["batch"]=keepAlive;
        gateway.Native.Register(host.Fixture.Manifest,host.Fixture.Module,host.Fixture.Version);
        gateway.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token=timeout.Token;
        ValueTask<string> Access(CancellationToken _)=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        using(var socket=new TcpClient()){
            await socket.ConnectAsync("localhost",gateway.NativePort,token);
            using var stream=await OpenReauthenticationTlsAsync(socket,certificate,token);
            await NativeFrame.WriteAsync(stream,NativeConnectionPolicyWire.Discover,NativeConnectionPolicyWire.WriteDiscovery("batch"),token);
            var rejected=await NativeFrame.ReadAsync(stream,token);
            Assert.Equal(AuthenticationWire.Error,rejected!.Value.Kind);
        }
        await using(var batch=NativeQueryClient.ForAuthenticatedTlsWithServerConnectionPolicy("batch",Access,port:gateway.NativePort,trustedRoot:certificate)){
            await CheckRpc(batch);
            Assert.Equal(perOperation,batch.ServerRecommendedConnectionPolicy);
            Assert.Null(batch.AuthenticatedPrincipal);
        }
        await using(var unknown=NativeQueryClient.ForAuthenticatedTlsWithServerConnectionPolicy("INTERACTIVE",Access,port:gateway.NativePort,trustedRoot:certificate)){
            await CheckRpc(unknown);
            Assert.Equal(keepAlive,unknown.ServerRecommendedConnectionPolicy);
        }
        var principal=new NativeAuthenticatedPrincipal(issuer.Id,"dev-user");
        using(var pool=NativeConnectionPool.ForAuthenticatedTls("localhost",gateway.NativePort,new(){
            MaximumConnections=1,UseServerConnectionPolicy=true,ServerConnectionPolicyProfile="interactive"
        },principal,Access,certificate)){
            await using(var first=await pool.RentAsync(token)){await CheckRpc(first);Assert.Equal(idle,first.ConnectionPolicy);}
            await using var again=await pool.RentAsync(token);
            Assert.Equal(idle,again.ServerRecommendedConnectionPolicy);
            await CheckRpc(again);
        }
        using(var pool=NativeConnectionPool.ForAuthenticatedTls("localhost",gateway.NativePort,new(){
            MaximumConnections=1,UseServerConnectionPolicy=true,ServerConnectionPolicyProfile="batch",ConnectionPolicy=keepAlive
        },principal,Access,certificate)){
            await using var explicitClient=await pool.RentAsync(token);
            await CheckRpc(explicitClient);
            Assert.Equal(keepAlive,explicitClient.ConnectionPolicy);
            Assert.Null(explicitClient.ServerRecommendedConnectionPolicy);
        }

        async Task CheckRpc(NativeQueryClient client){
            Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,token));
        }
    });

    [Fact]
    public void ConnectionPolicyDiscoveryPreservesLegacyAndValidatesNamedFrames(){
        Assert.Equal(new byte[]{1},NativeConnectionPolicyWire.WriteDiscovery(null));
        Assert.Null(NativeConnectionPolicyWire.ReadDiscovery(new ReadOnlySequence<byte>(new byte[]{1})));
        var profile=new string('a',NativeConnectionPolicyWire.MaximumProfileLength-4)+".b-1";
        var discovery=NativeConnectionPolicyWire.WriteDiscovery(profile);
        Assert.Equal(NativeConnectionPolicyWire.MaximumDiscoveryBytes,discovery.Length);
        Assert.Equal(profile,NativeConnectionPolicyWire.ReadDiscovery(new ReadOnlySequence<byte>(discovery)));
        Assert.Throws<ArgumentException>(()=>NativeConnectionPolicyWire.WriteDiscovery(profile+"x"));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadDiscovery(new ReadOnlySequence<byte>(new byte[]{1,2})));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadDiscovery(new ReadOnlySequence<byte>(new byte[]{2})));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadDiscovery(new ReadOnlySequence<byte>(new byte[]{2,0xff})));
        Assert.Throws<InvalidDataException>(()=>NativeConnectionPolicyWire.ReadDiscovery(new ReadOnlySequence<byte>(new byte[]{2,32})));
    }

    [Fact]public Task RecommendedConnectionPolicyIsAppliedOnlyAfterAuthentication()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer);
        var recommendation=new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseAfterOperation);
        using var gateway=new GatewayServer(host.Fixture.Registry,new(){
            TdsEnabled=false,HttpEnabled=false,NativePort=0,NativeCertificate=certificate,
            NativeAuthorization=host.Authorization,NativeAuthenticatedLimits=new(),NativeRecommendedConnectionPolicy=recommendation
        });
        gateway.Native.Register(host.Fixture.Manifest,host.Fixture.Module,host.Fixture.Version);
        gateway.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using(var unauthenticated=NativeQueryClient.ForTlsWithServerConnectionPolicy(port:gateway.NativePort,trustedRoot:certificate)){
            await Assert.ThrowsAsync<InvalidDataException>(()=>unauthenticated.ConnectAsync(timeout.Token));
            Assert.Null(unauthenticated.ServerRecommendedConnectionPolicy);
        }
        var tokens=0;
        ValueTask<string> Token(CancellationToken _){
            tokens++;
            return ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        }
        await using(var client=NativeQueryClient.ForAuthenticatedTlsWithServerConnectionPolicy(Token,port:gateway.NativePort,trustedRoot:certificate)){
            Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
            Assert.Equal(recommendation,client.ServerRecommendedConnectionPolicy);
            Assert.Equal(recommendation,client.ConnectionPolicy);
            Assert.Null(client.AuthenticatedPrincipal);
            Assert.Equal(9,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,8,timeout.Token));
            Assert.Equal(2,tokens);
        }
        var explicitPolicy=new NativeConnectionPolicy(NativeConnectionLifetime.KeepAlive);
        await using(var client=NativeQueryClient.ForAuthenticatedTls(Token,"localhost",gateway.NativePort,certificate,explicitPolicy)){
            Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
            Assert.Equal(9,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,8,timeout.Token));
            Assert.Equal(explicitPolicy,client.ConnectionPolicy);
            Assert.Null(client.ServerRecommendedConnectionPolicy);
            Assert.Equal(3,tokens);
        }
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task StaticModulesKeepTheSchemaCheckEvenWithCompleteScalarContracts(bool authenticated)=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        if(authenticated){await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer);await Check(host.Fixture,host.Client);}
        else{using var fixture=new Fixture(certificate);await Check(fixture,()=>NativeQueryClient.ForTls(port:fixture.Server.Port,trustedRoot:certificate));}
        async Task Check(Fixture fixture,Func<NativeQueryClient> create){
            await using(var client=create()){
                var error=await Assert.ThrowsAsync<NativeQueryException>(()=>client.ExecuteScalarAsync<int,int>("tls","value","fixture","obsolete",7,timeout.Token));
                Assert.Contains("fingerprint",error.Message,StringComparison.Ordinal);Assert.Equal(0,fixture.Module.Calls);
            }
            await using var compatible=create();Assert.Equal(8,await compatible.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
        }
    });

    [Theory,InlineData(false),InlineData(true)]public Task AuthenticatedTlsExecutesAnApprovedModuleAndKeepsThePrincipal(bool recordResources)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tokens=0;var budget=new Seamless.Execution.ResourceBudget(1<<20,0);var limits=new Seamless.Execution.Codecs.RecordGraphValidationLimits{MaximumDepth=12};
        ValueTask<string> Access(CancellationToken _){tokens++;return ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));}
        NativeQueryClient Create(){if(recordResources){return NativeQueryClient.ForAuthenticatedTls(Access,"localhost",host.Fixture.Server.Port,certificate,budget,limits);}return NativeQueryClient.ForAuthenticatedTls(Access,port:host.Fixture.Server.Port,trustedRoot:certificate);}
        await using var client=Create();if(recordResources){Assert.Same(budget,client.RecordBudget);Assert.Same(limits,client.RecordLimits);}
        await client.ConnectAsync(timeout.Token);Assert.Equal(new(issuer.Id,"dev-user"),client.AuthenticatedPrincipal);
        Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));Assert.Equal(9,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,8,timeout.Token));Assert.Equal(1,tokens);
        host.Manager.PublishPolicy(host.Policy(2,false));var denied=await Assert.ThrowsAsync<NativeQueryException>(()=>client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));Assert.True(denied.IsRecoverable);Assert.Equal(2,host.Fixture.Module.Calls);
        host.Manager.PublishPolicy(host.Policy(3,true));Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));Assert.Equal(1,tokens);
    });

    [Theory][InlineData("missing")][InlineData("token")][InlineData("subject")][InlineData("hostname")]
    public Task AuthenticationFailureNeverRunsTheModuleOrFallsBack(string kind)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var calls=0;var token=kind=="token"?"invalid-token":issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:kind=="subject"?"other-user":"dev-user",scope:"seamless.database");
        await using var client=kind=="missing"?NativeQueryClient.ForTls(port:host.Fixture.Server.Port,trustedRoot:certificate):NativeQueryClient.ForAuthenticatedTls(_=>{calls++;return ValueTask.FromResult(token);},host:kind=="hostname"?"127.0.0.1":"localhost",port:host.Fixture.Server.Port,trustedRoot:certificate);
        var failure=await Record.ExceptionAsync(()=>client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));Assert.NotNull(failure);Assert.Equal(0,host.Fixture.Module.Calls);Assert.False(timeout.IsCancellationRequested);
        if(kind is "token" or "hostname"){Assert.Null(client.AuthenticatedPrincipal);}if(kind=="hostname"){Assert.Equal(0,calls);}
    });

    [Fact]public Task ExplicitTransactionRetainsItsLeaseUntilCommitThenUsesTheNewPolicy()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client=host.Client();await using var transaction=await client.BeginTransactionAsync("fixture",timeout.Token);
        Assert.Equal(1,await Scalar(transaction,"insert",7,timeout.Token));host.Manager.PublishPolicy(host.Policy(2,false));
        Assert.Equal(1,await Scalar(transaction,"insert",8,timeout.Token));await transaction.CommitAsync(timeout.Token);
        await Assert.ThrowsAsync<NativeQueryException>(()=>client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));host.Manager.PublishPolicy(host.Policy(3,true));
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,8,timeout.Token));
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task IdleTransactionIsRolledBackByDeadlineOrEmergencyDisable(bool disable)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer,disable?TimeSpan.FromMinutes(30):TimeSpan.FromSeconds(1));using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client=host.Client();var transaction=await client.BeginTransactionAsync("fixture",timeout.Token);Assert.Equal(1,await Scalar(transaction,"insert",7,timeout.Token));
        if(disable){
            host.Manager.SetDisabled(new(issuer.Id,"dev-user"),true);
            host.Manager.SetDisabled(new(issuer.Id,"dev-user"),false);
        }else{
            // 設定した1秒のアイドル期限を経過させ、後続接続の読取と同じキーへの追加で解放を検証する。
            await Task.Delay(TimeSpan.FromSeconds(1),timeout.Token);
        }
        await using var observer=host.Client();Assert.Equal(0,await observer.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
        Assert.Equal(1,await observer.ExecuteScalarAsync<int,int>("tls","insert","fixture",Fingerprint,7,timeout.Token));
        await Assert.ThrowsAnyAsync<Exception>(()=>transaction.CommitAsync(timeout.Token));Assert.True(transaction.IsCompleted);
    });

    [Fact]public Task AuthenticatedInputIsValidatedBeforeTheModuleDeserializer()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var socket=new TcpClient();await socket.ConnectAsync("localhost",host.Fixture.Server.Port,timeout.Token);using var stream=new SslStream(socket.GetStream());
        var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.Online};trust.CustomTrustStore.Add(certificate);
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=trust},timeout.Token);
        var authentication=AuthenticationWire.Request(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        await NativeFrame.WriteAsync(stream,AuthenticationWire.Authenticate,authentication,timeout.Token);Assert.Equal(AuthenticationWire.Authenticated,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
        await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(new NativeQueryRequest("tls","value","fixture",Fingerprint,[1,2,3],ArgumentsContract:NativeWireContract.For<int>(),ResultContract:NativeWireContract.For<int>())),timeout.Token);
        var rejected=await NativeFrame.ReadAsync(stream,timeout.Token);Assert.Equal(NativeFrame.Error,rejected!.Value.Kind);Assert.Equal(0,host.Fixture.Module.Calls);
        // 正常なMemoryPackだが外側の上限超過。scanner無しなら契約不一致の応答となるため、EOFと区別できる。
        await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(new NativeQueryRequest("tls","value","fixture",Fingerprint,MemoryPackSerializer.Serialize(7),ArgumentsContract:new string('x',1025),ResultContract:NativeWireContract.For<int>())),timeout.Token);
        var closed=await NativeFrame.ReadAsync(stream,timeout.Token);Assert.Null(closed);Assert.Equal(0,host.Fixture.Module.Calls);
    });

    private static async Task<int> Scalar(NativeTransaction transaction,string query,int argument,CancellationToken token){
        var count=0;var result=0;await foreach(var row in transaction.QueryAsync<int,int>("tls",query,Fingerprint,argument,token)){result=row;count++;}Assert.Equal(1,count);return result;
    }
    [Fact]public Task AuthenticationCapacityRejectsWithoutStartingAnotherSession()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var sessions=0;
        using var host=new AuthenticatedFixture(certificate,issuer,sessionHook:async(session,_)=>{if(Interlocked.Increment(ref sessions)==1){entered.TrySetResult();await release.Task;}return session;},maximumAuthentications:1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await using var first=host.Client();var opening=first.ConnectAsync(timeout.Token);
        try{
            await entered.Task.WaitAsync(timeout.Token);await using var second=host.Client();var busy=await Assert.ThrowsAsync<NativeAuthenticationException>(()=>second.ConnectAsync(timeout.Token));Assert.Equal(NativeAuthenticationError.Busy,busy.Error);Assert.Equal(1,sessions);Assert.Equal(0,host.Fixture.Module.Calls);
        }finally{release.TrySetResult();}
        await opening;Assert.Equal(8,await first.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
    });
    [Fact]public Task LateAuthenticationSessionIsReleasedAfterItsDeadline()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var expired=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);IAuthorizationSession? late=null;var sessions=0;
        using var host=new AuthenticatedFixture(certificate,issuer,sessionHook:async(session,token)=>{
            if(Interlocked.Increment(ref sessions)==1){late=session;using var registration=token.Register(()=>expired.TrySetResult());entered.TrySetResult();await release.Task;}return session;
        },maximumAuthentications:1,authenticationTimeout:TimeSpan.FromSeconds(2));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await using var first=host.Client();var opening=first.ConnectAsync(timeout.Token);
        try{await entered.Task.WaitAsync(timeout.Token);await expired.Task.WaitAsync(timeout.Token);}finally{release.TrySetResult();}
        await Assert.ThrowsAnyAsync<IOException>(()=>opening);Assert.Null(first.AuthenticatedPrincipal);Assert.NotNull(late);Assert.Throws<ObjectDisposedException>(()=>late.BeginOperation());Assert.Equal(0,host.Fixture.Module.Calls);
        await using var second=host.Client();Assert.Equal(8,await second.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
    });
    private sealed class AuthenticatedFixture:IDisposable{
        private readonly OidcDevelopmentIssuer issuer;
        private readonly OidcTokenVerifier verifier;
        private static readonly Guid ServerResource=Guid.Parse("b49a425a-cfc2-4989-bd75-d7cfd09be501"),DatabaseResource=Guid.Parse("b49a425a-cfc2-4989-bd75-d7cfd09be502"),QueryResource=Guid.Parse("b49a425a-cfc2-4989-bd75-d7cfd09be503");
        public AuthorizationManager Manager{get;}
        public NativeGatewayAuthorization Authorization{get;}
        public Fixture Fixture{get;}
        public OidcTokenVerifier Tokens=>this.verifier;
        public Guid DatabaseId=>DatabaseResource;
        private readonly X509Certificate2 certificate;
        public AuthenticatedFixture(X509Certificate2 certificate,OidcDevelopmentIssuer issuer,TimeSpan? maximumTransactionDuration=null,Func<IAuthorizationSession,CancellationToken,ValueTask<IAuthorizationSession>>? sessionHook=null,int maximumAuthentications=32,TimeSpan? authenticationTimeout=null,System.Net.IPEndPoint? endpoint=null,TimeProvider? time=null,NativeServerLimits? limits=null,int? maximumVerifications=null){
            this.certificate=certificate;this.issuer=issuer;
            if(maximumVerifications is{} maximum){this.verifier=OidcTokenVerifier.ForServer(issuer.Settings,maximum,time);}
            else{this.verifier=new(issuer.Settings,time);}
            this.Manager=new(this.Policy(1,true),time,limits:new(){MaximumTransactionDuration=maximumTransactionDuration??TimeSpan.FromMinutes(30)});
            Fixture? target=null;
            async ValueTask<IAuthorizationSession> CreateSession(AuthenticatedIdentity identity,CancellationToken token){var session=this.Manager.CreateSession(identity);try{if(sessionHook is not null){return await sessionHook(session,token);}return session;}catch{session.Dispose();throw;}}
            this.Authorization=new NativeGatewayAuthorization(this.verifier,CreateSession,
                (lease,request)=>{if(request.Database!="fixture"){throw new AuthorizationException("UnknownResource","Unknown database.");}lease.Demand(DatabaseResource,SecurityPermission.Discover);},
                (lease,request,module,definition)=>{
                    if(!ReferenceEquals(target!.Module,module)||!target.Manifest.Queries.Any(item=>ReferenceEquals(item,definition))||request.ModuleId!="tls"){throw new AuthorizationException("UnapprovedModule","The executable is not approved.");}
                    lease.Demand(QueryResource,SecurityPermission.Execute);
                },static(_,bytes)=>NativeWireValidation.Validate<int>(bytes.Span),maximumAuthentications,authenticationTimeout);
            this.Fixture=target=new(certificate,start:false,authorization:this.Authorization,authenticatedEndpoint:endpoint,authenticatedLimits:limits);this.Fixture.Server.Start();
        }
        public Seamless.Security.AuthorizationPolicySnapshot Policy(long version,bool execute){
            var permissions=SecurityPermission.None;if(execute){permissions=SecurityPermission.Execute;}
            var role=new SecurityRole(QueryResource,"executor",[new(DatabaseResource,SecurityPermission.Discover),new(QueryResource,permissions)]);
            return new(version,[new(ServerResource,null,SecurityResourceKind.Server,"server"),new(DatabaseResource,ServerResource,SecurityResourceKind.Database,"fixture"),new(QueryResource,DatabaseResource,SecurityResourceKind.Query,"tls")],[role],[new(new(this.issuer.Id,"dev-user"),SecurityPrincipalKind.User,role.Id)]);
        }
        public NativeQueryClient Client()=>NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(this.issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database")),port:this.Fixture.Server.Port,trustedRoot:this.certificate);
        public void Dispose(){this.Fixture.Dispose();this.Manager.Dispose();this.verifier.Dispose();}
    }
}
