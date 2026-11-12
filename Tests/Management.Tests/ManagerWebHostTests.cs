using System.Net;
using System.Text;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagerWebHostTests{
    [Fact]public void PublishedEmbeddedHostSelectsOnlyItsOwnEntryManifestWhenTheServerManifestIsAbsent(){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"web-assets-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var server=Path.Combine(directory,"Server.staticwebassets.endpoints.json");
        var entry=Path.Combine(directory,"Business.staticwebassets.endpoints.json");
        File.WriteAllText(Path.Combine(directory,"Unrelated.staticwebassets.endpoints.json"),"{}");
        Assert.Null(ManagerWebHost.ResolveStaticAssetsManifest(directory,"Server","Business"));
        Assert.Null(ManagerWebHost.ResolveStaticAssetsManifest(directory,"Server",null));
        File.WriteAllText(entry,"{}");
        Assert.Equal(entry,ManagerWebHost.ResolveStaticAssetsManifest(directory,"Server","Business"));
        Assert.Null(ManagerWebHost.ResolveStaticAssetsManifest(directory,"Server",null));
        File.WriteAllText(server,"{}");
        Assert.Equal(server,ManagerWebHost.ResolveStaticAssetsManifest(directory,"Server","Business"));
    }
    [Fact]public Task CoLocatedServerSharesTheStoreWithCSharpClientsAndOwnerLaunch()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"server-web-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var operations=new ConcurrentManagementOperations(service);
        await using var web=await ManagerWebHost.StartAsync(operations);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service,certificate,new(IPAddress.Loopback,0),web.Sessions);
        server.Start();
        var managerConnection=LocalManagerConnection.Publish(root,store.InstanceId,server.PipeName!);
        try{
            Assert.False(File.Exists(Path.Combine(root,Seamless.Management.LocalServerConnection.FileName)));
            var launch=await ManagerBrowserLauncher.CreateLaunchAsync(root);
            Assert.Equal(web.Sessions.PathBase+"launch",new Uri(launch.Url).AbsolutePath);
            Assert.Equal(store.InstanceId,(await operations.InfoAsync()).InstanceId);
            await Task.WhenAll(Enumerable.Range(0,4).Select(async _=>{
                using var client=new ManagementClient("localhost",server.Port,certificate);
                await client.LoginAsync("admin","TestPassword-123");
                Assert.Equal(store.InstanceId,(await client.InfoAsync()).InstanceId);
                Assert.True(Assert.Single((await client.DatabasesAsync()).Items).IsSystem);
            }));
            using var local=ManagementClient.ForLocalPipe(server.PipeName!);
            Assert.Equal(ManagementPermission.All,Assert.Single((await local.LocalIdentityAsync()).Grants).Permissions);
        }finally{managerConnection.Remove(root);}
    });
    [Fact]public async Task EmbeddedManagerUsesTheSameHostAndRequiresAnOwnerLaunch(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"web-"+Guid.NewGuid().ToString("N"));
        using var embedded=InProcessManagementHost.Create(root);
        await using var web=await ManagerWebHost.StartAsync(embedded);
        Assert.Equal(Environment.ProcessId,web.ProcessId);
        Assert.Equal(embedded.Info.InstanceId,web.Sessions.InstanceId);
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        var url=new Uri(web.Origin,web.Sessions.PathBase);
        var anonymous=await client.GetAsync(url);
        Assert.Equal("/manager/",url.AbsolutePath);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        Assert.Contains("role=\"status\"",await anonymous.Content.ReadAsStringAsync());
        using var otherBrowser=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Unauthorized,(await otherBrowser.GetAsync(url)).StatusCode);
        var launch=web.Sessions.CreateLaunch();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(new Uri(launch.Url.Split('#')[0]))).StatusCode);
        using var request=new HttpRequestMessage(HttpMethod.Post,new Uri(url,"session"));
        request.Headers.Add("Origin",web.Origin.GetLeftPart(UriPartial.Authority));
        request.Content=new StringContent(new Uri(launch.Url).Fragment[1..],Encoding.UTF8,"text/plain");
        Assert.Equal(HttpStatusCode.NoContent,(await client.SendAsync(request)).StatusCode);
        var page=await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK,page.StatusCode);
        Assert.Contains("Seamless",await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(new Uri(url,"_content/Seamless.Management.Components/manager.css"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(new Uri(url,"_framework/blazor.web.js"))).StatusCode);
        using var wrongOrigin=new HttpRequestMessage(HttpMethod.Get,url);
        wrongOrigin.Headers.Add("Origin","http://example.invalid");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(wrongOrigin)).StatusCode);
        using var wrongHost=new HttpRequestMessage(HttpMethod.Get,url);
        wrongHost.Headers.Host="example.invalid";
        Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(wrongHost)).StatusCode);
        web.Sessions.RevokeAll();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(url)).StatusCode);
        Assert.Equal(embedded.Info.InstanceId,(await embedded.InfoAsync()).InstanceId);
        if(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_LOG") is{} log){
            Directory.CreateDirectory(log);
            var proof=new{InstanceId=embedded.Info.InstanceId,Pid=Environment.ProcessId,DbThreadId=embedded.ExecutionThreadId,ServerModule=typeof(ManagerWebHost).Assembly.ManifestModule.ModuleVersionId,
                Unauthenticated=401,Authenticated=200,Css=200,RuntimeScript=200,WrongOrigin=403,WrongHost=400,Revoked=401};
            await using var file=new FileStream(Path.Combine(log,"manager-web-http.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None);
            await System.Text.Json.JsonSerializer.SerializeAsync(file,proof);
        }
    }
    [Fact]public async Task EmbeddedManagerExecutesReadRowsCommitRowsAndPlans(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"web-ops-"+Guid.NewGuid().ToString("N"));
        using var embedded=InProcessManagementHost.Create(root);
        var dbDef=NativeWorkflow.Database();
        var draft=await embedded.SaveDraftAsync(new(dbDef,0,0));
        var preview=await embedded.PreviewAsync(new(dbDef.Id,draft.Revision));
        Assert.True(preview.CanApply);
        var apply=await embedded.ApplyAsync(new(dbDef.Id,draft.Revision,preview.Token,Guid.NewGuid()));
        Assert.Equal(OperationState.Succeeded,apply.State);

        // 1. PlansAsync による実行計画取得
        var plans=await embedded.PlansAsync(dbDef.Id);
        Assert.NotNull(plans);

        // 2. ReadRowsAsync によるテーブル行読み取り
        var rows=await embedded.ReadRowsAsync(new(dbDef.Database,"Rows",10));
        Assert.NotNull(rows);
        Assert.Empty(rows.Rows);

        // 3. BrowserManagementOperations 経由での委譲確認
        await using var web=await ManagerWebHost.StartAsync(embedded);
        var launch=web.Sessions.CreateLaunch();
        var token=new Uri(launch.Url).Fragment[1..];
        var session=web.Sessions.Redeem(token,web.Origin.GetLeftPart(UriPartial.Authority));
        IManagementOperations browserOps=new BrowserManagementOperations(web.Sessions,embedded,()=>Task.FromResult<string?>(session.Id));

        var browserRows=await browserOps.ReadRowsAsync(new(dbDef.Database,"Rows",10));
        Assert.NotNull(browserRows);
        Assert.Empty(browserRows.Rows);

        var browserPlans=await browserOps.PlansAsync(dbDef.Id);
        Assert.NotNull(browserPlans);
    }
}
