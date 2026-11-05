using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class LocalPipe{
    [Fact]public async Task PermissionsAndDisabledAccount(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        NativeWorkflow.Publish(service,admin,NativeWorkflow.Database());service.ChangeUser(admin,new("reader","ReaderPassword-123",[new("Native",ManagementPermission.View|ManagementPermission.Execute)]));
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0),"reader");server.Start();var client=ManagementClient.ForLocalPipe(server.PipeName!);
        var identity=await client.LocalIdentityAsync();Assert.Equal("reader",identity.Name);Assert.Null(client.Token);
        var databases=await client.CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{});Assert.False(Assert.Single(databases.Items).IsSystem);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>client.CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",new CatalogRequest("system")))).Code);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>client.CallAsync<Draft>("saveDraft","Native",new SaveDraftRequest(NativeWorkflow.Database(),1,1)))).Code);
        var query=new Definition("ipc-query","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","namespace Model;public static class Query{public static int Run()=>42;}")],[]));NativeWorkflow.Publish(service,admin,query);
        var rows=new List<ExecutionRow>();await foreach(var row in client.ExecuteAsync("Native",new("ipc-query",null,ManagementJson.Element(new{})))){rows.Add(row);}Assert.Equal(42,Assert.Single(rows).Value.GetInt32());
        service.ChangeUser(admin,new("reader",null,[new("Native",ManagementPermission.View)],true));Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>client.LocalIdentityAsync())).Code);
    }
}
