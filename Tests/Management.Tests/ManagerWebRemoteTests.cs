using System.Net;
using System.Text;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagerWebRemoteTests{
    [Fact]public Task HttpsLoginIsSeparateFromTheLocalOwnerSession()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        service.ChangeUser(admin,new("viewer","ViewerPassword-123",[new("*",ManagementPermission.View)]));
        await using var execution=new ConcurrentManagementOperations(service);
        await using var local=await ManagerWebHost.StartAsync(execution);
        await using var remote=await ManagerWebHost.StartExternalAsync(service,execution,certificate,IPAddress.Loopback,0);
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(_,peer,_,_)=>peer is not null&&peer.RawData.AsSpan().SequenceEqual(certificate.RawData)};
        using var client=new HttpClient(handler);
        var launch=local.Sessions.CreateLaunch();
        using(var ownerRequest=new HttpRequestMessage(HttpMethod.Post,new Uri(local.Origin,local.Sessions.PathBase+"session"))){
            ownerRequest.Headers.Add("Origin",local.Origin.GetLeftPart(UriPartial.Authority));
            ownerRequest.Content=new StringContent(new Uri(launch.Url).Fragment[1..],Encoding.UTF8,"text/plain");
            Assert.Equal(HttpStatusCode.NoContent,(await client.SendAsync(ownerRequest)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(new Uri(local.Origin,local.Sessions.PathBase))).StatusCode);
        var remoteRoot=new Uri(remote.Origin,"manager/");
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync(remoteRoot)).StatusCode);
        var loginPage=await client.GetAsync(new Uri(remoteRoot,"login"));
        Assert.Equal(HttpStatusCode.OK,loginPage.StatusCode);
        Assert.Contains("<meta name=\"referrer\" content=\"same-origin\">",await loginPage.Content.ReadAsStringAsync());
        Assert.Equal("no-referrer",Assert.Single(loginPage.Headers.GetValues("Referrer-Policy")));
        using(var opaqueOrigin=Login(remoteRoot,"viewer","ViewerPassword-123","null")){
            Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(opaqueOrigin)).StatusCode);
        }
        using(var wrongOrigin=Login(remoteRoot,"viewer","ViewerPassword-123","https://example.invalid")){
            Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(wrongOrigin)).StatusCode);
        }
        using(var wrongPassword=Login(remoteRoot,"viewer","IncorrectPassword-123",remote.Origin.GetLeftPart(UriPartial.Authority))){
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.SendAsync(wrongPassword)).StatusCode);
        }
        using(var login=Login(remoteRoot,"viewer","ViewerPassword-123",remote.Origin.GetLeftPart(UriPartial.Authority))){
            var response=await client.SendAsync(login);
            Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            Assert.Contains(response.Headers.GetValues("Set-Cookie"),value=>value.Contains("secure",StringComparison.OrdinalIgnoreCase)&&value.Contains("httponly",StringComparison.OrdinalIgnoreCase));
        }
        var page=await client.GetAsync(remoteRoot);
        Assert.Equal(HttpStatusCode.OK,page.StatusCode);
        var html=await page.Content.ReadAsStringAsync();
        Assert.Contains("viewer",html);Assert.DoesNotContain("ソースを開く",html);
        using(var logout=new HttpRequestMessage(HttpMethod.Post,new Uri(remoteRoot,"logout"))){
            logout.Headers.Add("Origin",remote.Origin.GetLeftPart(UriPartial.Authority));
            Assert.Equal(HttpStatusCode.Redirect,(await client.SendAsync(logout)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync(remoteRoot)).StatusCode);
        Assert.Throws<ManagementException>(()=>remote.Sessions);
    });
    [Fact]public async Task UserOperationsRecheckGrantsAndDisabledAccounts(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        service.ChangeUser(admin,new("viewer","ViewerPassword-123",[new("*",ManagementPermission.View)]));
        await using var execution=new ConcurrentManagementOperations(service);
        using var sessions=new UserBrowserSessions(service);
        var session=sessions.SignIn(new("viewer","ViewerPassword-123"));
        IManagementOperations operations=new AuthenticatedManagementOperations(execution,sessions,()=>Task.FromResult<string?>(session.Id));
        Assert.DoesNotContain("OwnerSourceProjectsV1",(await operations.InfoAsync()).Capabilities);
        Assert.False(operations is IManagementSourceOperations);
        var request=new SaveDraftRequest(Seamless.Management.DatabaseDesigner.Empty("permission-db","PermissionDb"),0,0);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveDraftAsync(request))).Code);
        service.ChangeUser(admin,new("viewer",null,[new("*",ManagementPermission.View|ManagementPermission.Edit)]));
        Assert.Equal("viewer",(await operations.SaveDraftAsync(request)).Owner);
        Assert.Equal("viewer",(await operations.FindDraftAsync(request.Definition.Id))!.Owner);
        service.ChangeUser(admin,new("viewer",null,[new("*",ManagementPermission.View)]));
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>operations.FindDraftAsync(request.Definition.Id))).Code);
        service.ChangeUser(admin,new("viewer",null,[new("*",ManagementPermission.View)],Disabled:true));
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.InfoAsync())).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.FindDraftAsync(request.Definition.Id))).Code);
    }
    private static HttpRequestMessage Login(Uri root,string name,string password,string origin){
        var request=new HttpRequestMessage(HttpMethod.Post,new Uri(root,"login")){Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"name",name},{"password",password}})};
        request.Headers.Add("Origin",origin);return request;
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"remote-web-"+Guid.NewGuid().ToString("N"));
}
