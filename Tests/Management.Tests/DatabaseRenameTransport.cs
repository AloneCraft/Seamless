using System.IO.Pipes;
using System.Security.Principal;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class DatabaseRenameTransport{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LostPipeResponseCanBeResolvedWithTheSameOperationAfterRestart(bool disconnectBeforeCommit){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"rename-transport-"+Guid.NewGuid().ToString("N"));
        var user=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var request=new RenameDatabaseRequest("db:db","Before","After",Guid.NewGuid());
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Service(store,null,startAuditWorker:false)){
            var version=new DefinitionVersion(Designer.Empty("db","Before"),1,"admin",DateTimeOffset.UtcNow,"immutable-artifact","generations/immutable");
            store.Write(()=>{store.Put("active","db",version);store.Put("version","db:1",version);});
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);
            server.Start();
            using var entered=new ManualResetEventSlim();
            using var release=new ManualResetEventSlim();
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var client=ManagementClient.ForLocalPipe(server.PipeName!);
            var identity=await client.LocalIdentityAsync(deadline.Token);
            user=new(identity.Name,"",identity.Grants);
            service.RenameCheckpoint=()=>{
                entered.Set();
                if(!release.Wait(TimeSpan.FromSeconds(20))){throw new TimeoutException("名前変更の試験バリアを解放できませんでした。");}
            };
            await using var pipe=new NamedPipeClientStream(".",server.PipeName!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(5000,deadline.Token);
            await ManagementWire.WriteAsync(pipe,new RpcRequest(Guid.NewGuid(),"renameDatabase",null,"",ManagementJson.Element(request)),deadline.Token);
            try{
                Assert.True(await Task.Run(()=>entered.Wait(TimeSpan.FromSeconds(20)),deadline.Token));
                if(disconnectBeforeCommit){pipe.Dispose();}
            }finally{release.Set();}
            if(!disconnectBeforeCommit){
                Assert.Equal("After",service.GetDefinition(user,"db").Database);
                pipe.Dispose();
            }
            var result=await client.RenameDatabaseAsync(request,deadline.Token);
            Assert.Equal("After",result.Name);
            Assert.Equal(result,await client.RenameDatabaseAsync(request,deadline.Token));
            Assert.Single(service.History(user).Items,static entry=>entry.Action=="DatabaseRenamed");
        }
        using(var store=new ManagementStore(root))
        using(var service=new Service(store,null,startAuditWorker:false)){
            Assert.Equal("After",service.RenameDatabase(user,request).Name);
            Assert.Single(service.History(user).Items,static entry=>entry.Action=="DatabaseRenamed");
        }
    }
}
