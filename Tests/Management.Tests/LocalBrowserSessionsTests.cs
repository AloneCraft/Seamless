using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class LocalBrowserSessionsTests{
    [Fact]public void StablePathNeverReusesAnotherHostSessionOrLaunch(){
        var instance=Guid.NewGuid();
        using var before=new LocalBrowserSessions(instance);
        using var after=new LocalBrowserSessions(instance);
        const string origin="http://127.0.0.1:25001";
        before.BindOrigin(new(origin));after.BindOrigin(new(origin));
        Assert.Equal("/manager/",before.PathBase);
        Assert.Equal(before.PathBase,after.PathBase);
        var launch=before.CreateLaunch();
        var session=before.Redeem(new Uri(launch.Url).Fragment[1..],origin);
        Assert.Throws<ManagementException>(()=>after.Authenticate(session.Id));
        var unused=before.CreateLaunch();
        Assert.Throws<ManagementException>(()=>after.Redeem(new Uri(unused.Url).Fragment[1..],origin));
        var own=after.Redeem(new Uri(after.CreateLaunch().Url).Fragment[1..],origin);
        _=after.Authenticate(own.Id);
        Assert.Throws<ManagementException>(()=>before.Authenticate(own.Id));
    }
    [Fact]public void LaunchIsBoundToOriginAndCanOnlyBeRedeemedOnce(){
        using var sessions=new LocalBrowserSessions(Guid.NewGuid());
        sessions.BindOrigin(new("http://127.0.0.1:25001/"));
        var launch=sessions.CreateLaunch();
        Assert.Equal(sessions.PathBase+"launch",new Uri(launch.Url).AbsolutePath);
        var token=new Uri(launch.Url).Fragment[1..];
        Assert.DoesNotContain(token,launch.ToString());
        Assert.Equal("Authentication",Assert.Throws<ManagementException>(()=>sessions.Redeem(token,"http://127.0.0.1:25002")).Code);
        var session=sessions.Redeem(token,"http://127.0.0.1:25001");
        Assert.Equal(ManagementPermission.All,Assert.Single(sessions.Authenticate(session.Id).Grants).Permissions);
        Assert.DoesNotContain(session.Id,session.ToString());
        Assert.Throws<ManagementException>(()=>sessions.Redeem(token,"http://127.0.0.1:25001"));
        sessions.RevokeAll();
        Assert.Throws<ManagementException>(()=>sessions.Authenticate(session.Id));
    }

    [Fact]public void DefaultSessionHasAFixedEightHourDeadlineAndCanBeReopened(){
        var clock=new TestClock();
        using var sessions=new LocalBrowserSessions(Guid.NewGuid(),timeProvider:clock);
        const string origin="http://127.0.0.1:25001";
        sessions.BindOrigin(new(origin));
        var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],origin);
        Assert.Equal(TimeSpan.FromHours(8),sessions.SessionLifetime);
        Assert.Equal(clock.GetUtcNow()+TimeSpan.FromHours(8),session.ExpiresAt);
        clock.Advance(TimeSpan.FromHours(8)-TimeSpan.FromTicks(1));
        _=sessions.Authenticate(session.Id);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal("Authentication",Assert.Throws<ManagementException>(()=>sessions.Authenticate(session.Id)).Code);
        var reopened=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],origin);
        Assert.Equal(clock.GetUtcNow()+TimeSpan.FromHours(8),reopened.ExpiresAt);
        _=sessions.Authenticate(reopened.Id);
    }

    [Fact]public void ExpiryCapacityAndShutdownRejectFurtherUse(){
        var clock=new TestClock();
        using var sessions=new LocalBrowserSessions(Guid.NewGuid(),TimeSpan.FromSeconds(10),TimeSpan.FromMinutes(1),1,clock);
        sessions.BindOrigin(new("http://127.0.0.1:25001/"));
        var expired=sessions.CreateLaunch();
        Assert.Equal("Busy",Assert.Throws<ManagementException>(()=>sessions.CreateLaunch()).Code);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Throws<ManagementException>(()=>sessions.Redeem(new Uri(expired.Url).Fragment[1..],"http://127.0.0.1:25001"));
        var launch=sessions.CreateLaunch();
        var session=sessions.Redeem(new Uri(launch.Url).Fragment[1..],"http://127.0.0.1:25001");
        Assert.Equal("Busy",Assert.Throws<ManagementException>(()=>sessions.CreateLaunch()).Code);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Throws<ManagementException>(()=>sessions.Authenticate(session.Id));
        _=sessions.CreateLaunch();
        sessions.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>sessions.CreateLaunch());
    }

    [Fact]public async Task ConcurrentRedemptionHasOneWinner(){
        using var sessions=new LocalBrowserSessions(Guid.NewGuid());
        sessions.BindOrigin(new("http://127.0.0.1:25001/"));
        var token=new Uri(sessions.CreateLaunch().Url).Fragment[1..];
        var results=await Task.WhenAll(Enumerable.Range(0,16).Select(index=>Task.Run(()=>{
            try{_=sessions.Redeem(token,"http://127.0.0.1:25001");return true;}
            catch(ManagementException){return false;}
        })));
        Assert.Equal(1,results.Count(item=>item));
    }

    [Theory][InlineData("http://example.com/")][InlineData("http://localhost/")][InlineData("http://127.0.0.1/path")][InlineData("http://user@127.0.0.1/")]
    public void OnlyAnExplicitLoopbackOriginCanBeBound(string origin){
        using var sessions=new LocalBrowserSessions(Guid.NewGuid());
        Assert.Throws<ArgumentException>(()=>sessions.BindOrigin(new(origin)));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExistingOperationsAdapterRejectsAnInvalidatedSessionBeforeSaving(bool expire){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        using var host=InProcessManagementHost.Create(root);
        var clock=new TestClock();
        using var sessions=new LocalBrowserSessions(host.Info.InstanceId,sessionLifetime:TimeSpan.FromMinutes(1),timeProvider:clock);
        sessions.BindOrigin(new("http://127.0.0.1:25001/"));
        var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:25001");
        var operations=new BrowserManagementOperations(sessions,host,()=>Task.FromResult<string?>(session.Id));
        Assert.Equal(host.Info.InstanceId,(await operations.InfoAsync()).InstanceId);
        if(expire){clock.Advance(TimeSpan.FromMinutes(1));}
        else{sessions.RevokeAll();}
        var request=new SaveDraftRequest(Seamless.Management.DatabaseDesigner.Empty("denied","DeniedDb"),0,0);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveDraftAsync(request))).Code);
        Assert.Equal("NotFound",Assert.Throws<ManagementException>(()=>host.GetDraft("denied")).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.InfoAsync())).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.FindDraftAsync("unknown"))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveSourceProjectDraftAsync("unknown"))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.InspectSourceProjectBuildAsync(new("unknown","missing.dll","old")))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.ImportSourceProjectBuildAsync(new(new("unknown","missing.dll","old"),"old")))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.GetImportedSourceBuildAsync(new("unknown",1)))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.FindImportedSourceBuildAsync(new("unknown",1)))).Code);
    }

    [Fact]public Task OnlyVerifiedOwnerIpcIssuesAndRevokesBrowserLaunches()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        using var sessions=new LocalBrowserSessions(store.InstanceId);
        sessions.BindOrigin(new("http://127.0.0.1:25001/"));
        using(var other=new LocalBrowserSessions(Guid.NewGuid())){
            Assert.Throws<ArgumentException>(()=>Seamless.Management.Server.ManagementServer.ForLocalOwner(service,other));
        }
        await using(var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service,certificate,new(System.Net.IPAddress.Loopback,0),sessions)){
            server.Start();
            using var pipe=ManagementClient.ForLocalPipe(server.PipeName!);
            var launch=await pipe.CreateLocalBrowserLaunchAsync();
            var session=sessions.Redeem(new Uri(launch.Url).Fragment[1..],"http://127.0.0.1:25001");
            Assert.Equal((await pipe.LocalIdentityAsync()).Name,sessions.Authenticate(session.Id).Name);
            using var tcp=new ManagementClient("localhost",server.Port,certificate);
            await tcp.LoginAsync("admin","TestPassword-123");
            Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(()=>tcp.CreateLocalBrowserLaunchAsync())).Code);
            Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(()=>tcp.RevokeLocalBrowserSessionsAsync())).Code);
            await pipe.RevokeLocalBrowserSessionsAsync();
            Assert.Throws<ManagementException>(()=>sessions.Authenticate(session.Id));
            _=await pipe.CreateLocalBrowserLaunchAsync();
        }
        Assert.Throws<ObjectDisposedException>(()=>sessions.CreateLaunch());
        await using var legacy=new Seamless.Management.Server.ManagementServer(service,certificate,new(System.Net.IPAddress.Loopback,0),"admin");
        legacy.Start();
        using var legacyPipe=ManagementClient.ForLocalPipe(legacy.PipeName!);
        Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(()=>legacyPipe.CreateLocalBrowserLaunchAsync())).Code);
    });

    private sealed class TestClock:TimeProvider{
        private DateTimeOffset now=new(2026,11,7,0,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()=>this.now;
        public void Advance(TimeSpan elapsed)=>this.now+=elapsed;
    }
}
