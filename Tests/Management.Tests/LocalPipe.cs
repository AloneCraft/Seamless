using System.Net;
using System.IO.Pipes;
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
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        // 接続は1要求ごとに閉じる（2026-11-12 の決定）。行の結果と完了を受け取ったあとで、サーバーが閉じる。
        await using(var pipe=new NamedPipeClientStream(".",server.PipeName!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly)){
            await pipe.ConnectAsync(5000,deadline.Token);
            var execute=new RpcRequest(Guid.NewGuid(),"execute",null,"Native",ManagementJson.Element(new ExecuteRequest("ipc-query",null,ManagementJson.Element(new{}))));
            await ManagementWire.WriteAsync(pipe,execute,deadline.Token);
            var rowResponse=await ManagementWire.ReadAsync<RpcResponse>(pipe,deadline.Token);
            Assert.Equal(execute.Id,rowResponse.Id);
            Assert.Equal("row",rowResponse.Kind);
            Assert.Equal(42,ManagementJson.Read<ExecutionRow>(rowResponse.Payload).Value.GetInt32());
            var complete=await ManagementWire.ReadAsync<RpcResponse>(pipe,deadline.Token);
            Assert.Equal(execute.Id,complete.Id);
            Assert.Equal("complete",complete.Kind);
            Assert.Equal(0,await pipe.ReadAsync(new byte[1],deadline.Token));
        }

        // 権限を外したあとの要求は、新しい接続でも本人確認から拒否される。
        service.ChangeUser(admin,new("reader",null,[new("Native",ManagementPermission.View)],true));
        await using(var pipe=new NamedPipeClientStream(".",server.PipeName!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly)){
            await pipe.ConnectAsync(5000,deadline.Token);
            var next=new RpcRequest(Guid.NewGuid(),"localIdentity",null,"",ManagementJson.Element(new{}));
            await ManagementWire.WriteAsync(pipe,next,deadline.Token);
            var denied=await ManagementWire.ReadAsync<RpcResponse>(pipe,deadline.Token);
            Assert.Equal(next.Id,denied.Id);
            Assert.Equal("Authentication",denied.ErrorCode);
            Assert.Equal(0,await pipe.ReadAsync(new byte[1],deadline.Token));
        }
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>client.LocalIdentityAsync(deadline.Token))).Code);
    }
}
