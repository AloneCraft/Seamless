using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Seamless.Authentication;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Security;

namespace Management.Tests;

public sealed partial class NativeTls{
    [Theory][InlineData(true)][InlineData(false)]
    public Task IdentityVerificationCapacityIsSharedAcrossProtocols(bool holdHttp)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var time=new AdmissionClock();
        using var host=new AuthenticatedFixture(certificate,issuer,time:time,maximumVerifications:1);
        var registry=host.Fixture.Registry;
        using var http=new HttpServer(0,registry.Resolve("fixture").Adapter,new SqlExecutionEngine(registry),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate},
            new HttpGatewayAuthorization(host.Tokens,host.Manager,name=>name=="fixture"?host.DatabaseId:null));
        http.Start();
        var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
        trust.CustomTrustStore.Add(certificate);
        using var web=new HttpClient(new SocketsHttpHandler{UseProxy=false,SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=trust}}){BaseAddress=new($"https://localhost:{http.Port}")};
        var access=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        web.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",access);

        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        var subject=new SecuritySubjectId(issuer.Id,"dev-user");
        var role=Guid.NewGuid();
        var policy=new Seamless.Security.AuthorizationPolicySnapshot(1,
            [new(store.InstanceId,null,SecurityResourceKind.Server,"server"),new(host.DatabaseId,store.InstanceId,SecurityResourceKind.Database,"fixture")],
            [new(role,"root",[new(store.InstanceId,SecurityPermission.All)])],[new(subject,SecurityPrincipalKind.User,role)]);
        var now=DateTimeOffset.UtcNow;
        using(StoredAuthorization.Initialize(store,policy,new(subject,"developer",[],now.AddMinutes(30),now),issuer.Settings)){}
        using var stored=StoredAuthorization.Open(store,time);
        await using var server=Seamless.Management.Server.ManagementServer.ForAuthorization(stored,host.Tokens,certificate,new(IPAddress.Loopback,0));
        server.Start();
        var manager=new ManagementClient("localhost",server.Port,certificate);

        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await using var reader=host.Client();
        await using var contender=host.Client();
        await using var writer=host.Client();
        await reader.ConnectAsync(timeout.Token);
        await contender.ConnectAsync(timeout.Token);
        await using var transaction=await writer.BeginTransactionAsync("fixture",timeout.Token);
        Assert.Equal(1,await Scalar(transaction,"insert",7,timeout.Token));
        var before=issuer.MembershipChecks;
        var gate=HoldOneMembershipCheck(issuer);
        time.Advance();
        var pending=HoldAsync();
        try{
            await gate.Entered.WaitAsync(timeout.Token);
            using var unavailable=await web.GetAsync("/api/databases",timeout.Token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,unavailable.StatusCode);
            var managementBusy=await Assert.ThrowsAsync<ManagementException>(()=>manager.DatabaseAuthorizationAsync(access,host.DatabaseId,cancellationToken:timeout.Token));
            Assert.Equal("AuthenticationBusy",managementBusy.Code);
            await using var newcomer=host.Client();
            var nativeBusy=await Assert.ThrowsAsync<NativeAuthenticationException>(()=>newcomer.ConnectAsync(timeout.Token));
            Assert.Equal(NativeAuthenticationError.Busy,nativeBusy.Error);
            var refreshBusy=await Assert.ThrowsAsync<NativeQueryException>(()=>Read(contender,timeout.Token));
            Assert.True(refreshBusy.IsRecoverable);
            Assert.Contains("identity verification limit",refreshBusy.Message,StringComparison.Ordinal);
            var exchangeBusy=await Assert.ThrowsAsync<NativeAuthenticationException>(()=>contender.ReauthenticateAsync(timeout.Token));
            Assert.Equal(NativeAuthenticationError.Busy,exchangeBusy.Error);
            if(holdHttp){await transaction.CommitAsync(timeout.Token);}
            else{await transaction.RollbackAsync(timeout.Token);}
            Assert.True(transaction.IsCompleted);
            Assert.Equal(before+1,issuer.MembershipChecks);
            Assert.Equal(1,host.Fixture.Module.Calls);
        }finally{gate.Release.TrySetResult();await pending;}
        Assert.Equal(8,await Read(contender,timeout.Token));
        using var reopened=await web.GetAsync("/api/databases",timeout.Token);
        Assert.Equal(HttpStatusCode.OK,reopened.StatusCode);
        Assert.Equal(1,(await manager.DatabaseAuthorizationAsync(access,host.DatabaseId,cancellationToken:timeout.Token)).Version);
        var expected=0;
        if(holdHttp){expected=1;}
        Assert.Equal(expected,await reader.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));

        async Task HoldAsync(){
            if(holdHttp){
                using var response=await web.GetAsync("/api/databases",timeout.Token);
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            }else{
                Assert.Equal(8,await Read(reader,timeout.Token));
            }
        }
    });
}
