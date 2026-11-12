using System.Net;
using System.Text;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class FixedManagerAddress{
    [Fact]public void StablePathKeepsExpiryAndCrossHostCredentialsIsolated(){
        var clock=new Clock();
        var instance=Guid.NewGuid();
        const string origin="http://127.0.0.1:25001";
        using var first=new LocalBrowserSessions(instance,timeProvider:clock);
        using var second=new LocalBrowserSessions(instance,timeProvider:clock);
        first.BindOrigin(new(origin));second.BindOrigin(new(origin));
        Assert.Equal("/manager/",first.PathBase);
        Assert.Equal(first.PathBase,second.PathBase);
        var launch=first.CreateLaunch();
        var token=new Uri(launch.Url).Fragment[1..];
        Assert.Throws<ManagementException>(()=>second.Redeem(token,origin));
        var session=first.Redeem(token,origin);
        Assert.Throws<ManagementException>(()=>first.Redeem(token,origin));
        Assert.Throws<ManagementException>(()=>second.Authenticate(session.Id));
        clock.Now+=TimeSpan.FromHours(8);
        Assert.Throws<ManagementException>(()=>first.Authenticate(session.Id));
        var fresh=first.Redeem(new Uri(first.CreateLaunch().Url).Fragment[1..],origin);
        _=first.Authenticate(fresh.Id);
    }

    [Fact]public async Task RestartKeepsTheAddressAndRequiresANewOwnerLaunch(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"fixed-"+Guid.NewGuid().ToString("N"));
        using var embedded=InProcessManagementHost.Create(root);
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        using var anonymous=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        var first=await ManagerWebHost.StartAsync(embedded);
        var address=new Uri(first.Origin,"manager/");
        var port=first.Origin.Port;
        try{
            var rejected=await anonymous.GetAsync(address);
            Assert.Equal(HttpStatusCode.Unauthorized,rejected.StatusCode);
            Assert.Contains("role=\"status\"",await rejected.Content.ReadAsStringAsync());
            await Launch(client,first);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(address)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(address)).StatusCode);
            using var crossOrigin=new HttpRequestMessage(HttpMethod.Get,address);
            crossOrigin.Headers.Add("Origin","http://example.invalid");
            Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(crossOrigin)).StatusCode);
        }finally{await first.DisposeAsync();}
        await using var restarted=await ManagerWebHost.StartAsync(embedded,port);
        Assert.Equal(address,new Uri(restarted.Origin,restarted.Sessions.PathBase));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(address)).StatusCode);
        await Launch(client,restarted);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(address)).StatusCode);
        restarted.Sessions.RevokeAll();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(address)).StatusCode);
    }

    private static async Task Launch(HttpClient client,ManagerWebHost web){
        var launch=web.Sessions.CreateLaunch();
        using var request=new HttpRequestMessage(HttpMethod.Post,new Uri(web.Origin,web.Sessions.PathBase+"session"));
        request.Headers.Add("Origin",web.Origin.GetLeftPart(UriPartial.Authority));
        request.Content=new StringContent(new Uri(launch.Url).Fragment[1..],Encoding.UTF8,"text/plain");
        Assert.Equal(HttpStatusCode.NoContent,(await client.SendAsync(request)).StatusCode);
    }
    private sealed class Clock:TimeProvider{
        public DateTimeOffset Now{get;set;}=new(2026,11,10,0,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()=>this.Now;
    }
}
