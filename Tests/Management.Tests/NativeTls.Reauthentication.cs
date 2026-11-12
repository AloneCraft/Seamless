using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using MemoryPack;
using Seamless.Database.Client;
using AuthenticationWire=Seamless.Database.Client.NativeAuthenticationWire;

namespace Management.Tests;

public sealed partial class NativeTls{
    [Fact]
    public Task PooledTlsKeepsOneSubjectAcrossLendingAndReplacement()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var correct=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        var wrong=issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:"other-user",scope:"seamless.database");
        var current=correct;var authentications=0;
        ValueTask<string> Token(CancellationToken _){authentications++;return ValueTask.FromResult(current);}
        using(var pool=NativeConnectionPool.ForAuthenticatedTls("localhost",host.Fixture.Server.Port,new(){MaximumConnections=1},new(issuer.Id,"dev-user"),Token,certificate)){
            await using(var first=await pool.RentAsync(deadline.Token)){Assert.Equal(8,await Read(first,deadline.Token));}
            await using(var second=await pool.RentAsync(deadline.Token)){
                Assert.Equal("dev-user",second.AuthenticatedPrincipal?.Subject);
                Assert.Equal(8,await Read(second,deadline.Token));
            }
            Assert.Equal(1,authentications);
        }
        var policy=new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseAfterOperation);
        using var replacing=NativeConnectionPool.ForAuthenticatedTls("localhost",host.Fixture.Server.Port,new(){MaximumConnections=1,ConnectionPolicy=policy},new(issuer.Id,"dev-user"),Token,certificate);
        await using(var first=await replacing.RentAsync(deadline.Token)){Assert.Equal(8,await Read(first,deadline.Token));}
        current=wrong;
        await using(var rejected=await replacing.RentAsync(deadline.Token)){
            await Assert.ThrowsAsync<InvalidDataException>(()=>Read(rejected,deadline.Token));
            Assert.Null(rejected.AuthenticatedPrincipal);
        }
        Assert.Equal(3,host.Fixture.Module.Calls);
        current=correct;
        await using var last=await replacing.RentAsync(deadline.Token);
        Assert.Equal(8,await Read(last,deadline.Token));
        Assert.Equal(4,authentications);
    });

    [Fact]
    public Task PerOperationTlsReconnectNeverSendsAQueryForADifferentSubject()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var original=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        var current=original;
        await using var client=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(current),"localhost",host.Fixture.Server.Port,certificate,new(NativeConnectionLifetime.ReleaseAfterOperation));
        await client.ConnectAsync(timeout.Token);
        Assert.Equal("dev-user",client.AuthenticatedPrincipal?.Subject);
        Assert.Equal(8,await Read(client,timeout.Token));
        Assert.Null(client.AuthenticatedPrincipal);
        Assert.Equal(8,await Read(client,timeout.Token));
        Assert.Equal(2,host.Fixture.Module.Calls);

        current=issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:"other-user",scope:"seamless.database");
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>Read(client,timeout.Token));
        Assert.Contains("different subject",error.Message,StringComparison.Ordinal);
        Assert.Equal(2,host.Fixture.Module.Calls);
        Assert.Null(client.AuthenticatedPrincipal);

        current=original;
        Assert.Equal(8,await Read(client,timeout.Token));
        Assert.Equal(3,host.Fixture.Module.Calls);
    });

    [Fact]public Task ReauthenticationUpdatesAnExpiredConnectionWithoutStartingAnotherSession()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var clock=new AdmissionClock();var sessions=0;
        using var host=new AuthenticatedFixture(certificate,issuer,time:clock,sessionHook:(session,_)=>{Interlocked.Increment(ref sessions);return ValueTask.FromResult(session);});
        var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database",lifetime:TimeSpan.FromMinutes(2));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(token),port:host.Fixture.Server.Port,trustedRoot:certificate);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.ReauthenticateAsync(timeout.Token));
        await client.ConnectAsync(timeout.Token);
        var principal=client.AuthenticatedPrincipal;
        clock.Advance();
        var expired=await Assert.ThrowsAsync<NativeQueryException>(()=>Read(client,timeout.Token));
        Assert.True(expired.IsRecoverable);Assert.Contains("Reauthentication",expired.Message,StringComparison.Ordinal);
        token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        await client.ReauthenticateAsync(timeout.Token);
        Assert.Equal(principal,client.AuthenticatedPrincipal);
        Assert.Equal(8,await Read(client,timeout.Token));

        await using(var results=client.QueryAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token).GetAsyncEnumerator()){
            Assert.True(await results.MoveNextAsync());
            var refresh=client.ReauthenticateAsync(timeout.Token);
            Assert.False(refresh.IsCompleted);
            Assert.False(await results.MoveNextAsync());
            await refresh;
        }
        Assert.Equal(1,Volatile.Read(ref sessions));
    });

    [Fact]public Task ReauthenticationRejectsAnotherSubjectAndADisabledSubjectOnTheSameConnection()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        await using var client=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(token),port:host.Fixture.Server.Port,trustedRoot:certificate);
        await client.ConnectAsync(timeout.Token);
        var principal=client.AuthenticatedPrincipal;
        token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:"other-user",scope:"seamless.database");
        await RejectAsync();
        Assert.Equal(8,await Read(client,timeout.Token));
        token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");
        var subject=new Seamless.Security.SecuritySubjectId(issuer.Id,"dev-user");
        host.Manager.SetDisabled(subject,true);
        await RejectAsync();
        host.Manager.SetDisabled(subject,false);
        await client.ReauthenticateAsync(timeout.Token);
        Assert.Equal(8,await Read(client,timeout.Token));

        async Task RejectAsync(){
            var denied=await Assert.ThrowsAsync<NativeAuthenticationException>(()=>client.ReauthenticateAsync(timeout.Token));
            Assert.Equal(NativeAuthenticationError.Rejected,denied.Error);
            Assert.Equal(principal,client.AuthenticatedPrincipal);
        }
    });

    [Fact]public Task ReauthenticationCannotInterruptAnActiveTransaction()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await using var client=host.Client();
        await using(var transaction=await client.BeginTransactionAsync("fixture",timeout.Token)){
            Assert.Equal(1,await Scalar(transaction,"insert",7,timeout.Token));
            var before=issuer.MembershipChecks;
            await Assert.ThrowsAsync<InvalidOperationException>(()=>client.ReauthenticateAsync(timeout.Token));
            Assert.Equal(before,issuer.MembershipChecks);
            await transaction.CommitAsync(timeout.Token);
        }
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
        using var socket=new TcpClient();
        await socket.ConnectAsync("localhost",host.Fixture.Server.Port,timeout.Token);
        using var stream=await OpenReauthenticationTlsAsync(socket,certificate,timeout.Token);
        var packet=AuthenticationWire.Request(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        await NativeFrame.WriteAsync(stream,AuthenticationWire.Authenticate,packet,timeout.Token);
        Assert.Equal(AuthenticationWire.Authenticated,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
        var id=Guid.NewGuid();
        await ControlAsync("begin");
        var calls=issuer.MembershipChecks;
        await NativeFrame.WriteAsync(stream,AuthenticationWire.Reauthenticate,packet,timeout.Token);
        var denied=(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value;
        Assert.Equal(AuthenticationWire.Error,denied.Kind);
        Assert.Equal(NativeAuthenticationError.Busy,AuthenticationWire.ReadFailure(denied.Payload));
        Assert.Equal(calls,issuer.MembershipChecks);
        await ControlAsync("rollback");
        await NativeFrame.WriteAsync(stream,AuthenticationWire.Reauthenticate,packet,timeout.Token);
        Assert.Equal(AuthenticationWire.Authenticated,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);

        async Task ControlAsync(string operation){
            await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(new NativeQueryRequest("$transaction",operation,"fixture","",[],id)),timeout.Token);
            Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
        }
    });

    [Theory][InlineData(true)][InlineData(false)]
    public Task ReauthenticationRejectsAnOversizedOrUnauthenticatedFrameBeforeIdentityLookup(bool authenticate)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var socket=new TcpClient();
        await socket.ConnectAsync("localhost",host.Fixture.Server.Port,timeout.Token);
        using var stream=await OpenReauthenticationTlsAsync(socket,certificate,timeout.Token);
        var packet=AuthenticationWire.Request(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        if(authenticate){
            await NativeFrame.WriteAsync(stream,AuthenticationWire.Authenticate,packet,timeout.Token);
            Assert.Equal(AuthenticationWire.Authenticated,(await NativeFrame.ReadAsync(stream,timeout.Token))!.Value.Kind);
        }
        var calls=issuer.MembershipChecks;
        // 本体を送らずヘッダーだけで、再認証frameの長さまたは時点を拒否できることを確認する。
        byte[] frame;
        if(!authenticate){
            frame=new byte[5+packet.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame,packet.Length+1);
            packet.CopyTo(frame,5);
        }else{
            frame=new byte[5];
            BinaryPrimitives.WriteInt32LittleEndian(frame,AuthenticationWire.MaximumRequestBytes+2);
        }
        frame[4]=AuthenticationWire.Reauthenticate;
        await stream.WriteAsync(frame,timeout.Token);
        var failure=await Record.ExceptionAsync(async()=>{
            var response=await NativeFrame.ReadAsync(stream,timeout.Token);
            if(!authenticate&&response is{} rejected){Assert.Equal(AuthenticationWire.Error,rejected.Kind);}
            else{Assert.Null(response);}
        });
        if(failure is not null){Assert.IsAssignableFrom<IOException>(failure);}
        Assert.Equal(calls,issuer.MembershipChecks);Assert.Equal(0,host.Fixture.Module.Calls);
    });

    [Fact]public Task ReauthenticationDiscardsAConnectionWithNoConfirmedReply()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var host=new AuthenticatedFixture(certificate,issuer,maximumVerifications:1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        await using var client=host.Client();
        await client.ConnectAsync(timeout.Token);
        var gate=HoldOneMembershipCheck(issuer);
        var pending=client.ReauthenticateAsync(cancellation.Token);
        try{
            await gate.Entered.WaitAsync(timeout.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
            Assert.Null(client.AuthenticatedPrincipal);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>client.ReauthenticateAsync(timeout.Token));
        }finally{gate.Release.TrySetResult();}
        // 停止で元の更新処理が終了し、同じ検証器を使用した再起動で枠が戻る。
        await host.Fixture.Server.StopAsync().WaitAsync(timeout.Token);
        host.Fixture.Server.Start();
        await using var next=host.Client();
        Assert.Equal(8,await Read(next,timeout.Token));
    });

    private static async Task<SslStream> OpenReauthenticationTlsAsync(TcpClient socket,X509Certificate2 certificate,CancellationToken cancellationToken){
        var stream=new SslStream(socket.GetStream());
        try{
            var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
            trust.CustomTrustStore.Add(certificate);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=trust},cancellationToken);
            return stream;
        }catch{stream.Dispose();throw;}
    }
}
