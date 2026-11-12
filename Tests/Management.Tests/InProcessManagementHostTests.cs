using Seamless.Management;
using Seamless.Management.Server;
using System.IO.Pipes;
using System.Security.Principal;

namespace Management.Tests;

public sealed class InProcessManagementHostTests{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));

    [Fact]public async Task LocalOperationsUseOneThreadAndTheSameStoreWithoutLogin(){
        var root=Root();Guid instance;
        using(var host=InProcessManagementHost.Create(root)){
            instance=host.Info.InstanceId;
            Assert.Equal(Environment.ProcessId,host.ProcessId);
            Assert.NotEqual(Environment.CurrentManagedThreadId,host.ExecutionThreadId);
            Assert.Equal(ManagementPermission.All,Assert.Single(host.Owner.Grants).Permissions);
            Assert.True(Assert.Single(host.Databases().Items).IsSystem);
            var tasks=Enumerable.Range(0,16).Select(_=>host.InvokeAsync((service,user)=>{
                Assert.True(Assert.Single(service.Databases(user).Items).IsSystem);
                return Environment.CurrentManagedThreadId;
            }).AsTask()).ToArray();
            Assert.All(await Task.WhenAll(tasks),id=>Assert.Equal(host.ExecutionThreadId,id));
            Assert.Equal(instance,host.Info.InstanceId);
            Assert.Throws<IOException>(()=>new ManagementStore(root));
        }
        using var reopened=InProcessManagementHost.Open(root);
        Assert.Equal(instance,reopened.Info.InstanceId);
        IManagementOperations operations=reopened;
        Assert.Equal(instance,(await operations.InfoAsync()).InstanceId);
        Assert.True(Assert.Single((await operations.DatabasesAsync()).Items).IsSystem);
    }

    [Fact]public Task TheSameListContractUsesExistingTlsAndLocalPipe()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(System.Net.IPAddress.Loopback,0),"admin");
        server.Start();
        using var pipe=ManagementClient.ForLocalPipe(server.PipeName!);
        using var tcp=new ManagementClient("localhost",server.Port,certificate);
        IManagementOperations local=pipe;IManagementOperations remote=tcp;
        Assert.Equal(store.InstanceId,(await local.InfoAsync()).InstanceId);
        Assert.True(Assert.Single((await local.DatabasesAsync()).Items).IsSystem);
        await Assert.ThrowsAsync<ManagementException>(()=>remote.DatabasesAsync());
        await tcp.LoginAsync("admin","TestPassword-123");
        Assert.Equal(store.InstanceId,(await remote.InfoAsync()).InstanceId);
        Assert.True(Assert.Single((await remote.DatabasesAsync()).Items).IsSystem);
    });

    [Fact]public async Task OsOwnerPipeUsesVerifiedIdentityWithoutAnApplicationAccount(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);
        server.Start();
        Assert.Equal(0,server.Port);
        using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Contains("OsOwnerPipeV1",(await client.InfoAsync()).Capabilities);
        var identity=await client.LocalIdentityAsync();
        Assert.Equal(ManagementPermission.All,Assert.Single(identity.Grants).Permissions);
        Assert.Null(client.Token);
        Assert.Null(store.Read<UserAccount>("user",ManagementStore.Normalize(identity.Name)));
        Assert.True(Assert.Single((await client.DatabasesAsync()).Items).IsSystem);
        using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15))){
            await using var raw=new NamedPipeClientStream(".",server.PipeName!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,TokenImpersonationLevel.Identification);
            await raw.ConnectAsync(5000,deadline.Token);
            var request=new RpcRequest(Guid.NewGuid(),"localIdentity","untrusted-request-token","",ManagementJson.Element(new{Name="untrusted-request-name"}));
            await ManagementWire.WriteAsync(raw,request,deadline.Token);
            var response=await ManagementWire.ReadAsync<RpcResponse>(raw,deadline.Token);
            Assert.Null(response.ErrorCode);
            Assert.Equal(identity.Name,ManagementJson.Read<UserInfo>(response.Payload).Name);
        }
        var definition=NativeWorkflow.Database();
        var draft=await client.CallAsync<Draft>("saveDraft",definition.Database,new SaveDraftRequest(definition,0,0));
        Assert.Equal(identity.Name,draft.Owner);
        var key=new DraftKey(definition.Id,draft.Revision);
        var preview=await client.CallAsync<ChangePreview>("preview",definition.Database,key);
        Assert.True(preview.CanApply);
        var operationId=Guid.NewGuid();
        var operation=await client.CallAsync<OperationInfo>("apply",definition.Database,new ApplyRequest(definition.Id,draft.Revision,preview.Token,operationId));
        Assert.Equal(OperationState.Succeeded,operation.State);
        var published=await client.DatabasesAsync();
        Assert.Contains(published.Items,item=>item.Name==definition.Database);
        Assert.Equal(operationId,(await client.CallAsync<OperationInfo>("operation",definition.Database,operationId)).Id);
    }

    [Fact]public async Task FailureCancellationAndReentryKeepTheExecutionThreadUsable(){
        using var host=InProcessManagementHost.Create(Root(),queueCapacity:1);
        var failure=new InvalidOperationException("injected");
        Assert.Same(failure,Assert.Throws<InvalidOperationException>(()=>host.Invoke<int>((_,_)=>throw failure)));
        Assert.Equal(host.ExecutionThreadId,host.Invoke((_,_)=>host.Invoke((_,_)=>Environment.CurrentManagedThreadId)));
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var running=host.InvokeAsync((_,_)=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));return 1;}).AsTask();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        using var cancellation=new CancellationTokenSource();var calls=0;
        var queued=host.InvokeAsync((_,_)=>Interlocked.Increment(ref calls),cancellation.Token).AsTask();
        cancellation.Cancel();release.Set();
        Assert.Equal(1,await running);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>queued);
        Assert.Equal(0,calls);
        Assert.True(Assert.Single(host.Databases().Items).IsSystem);
    }

    [Fact]public void DisposeRejectsFurtherOperationsAndReleasesTheOwner(){
        var root=Root();var host=InProcessManagementHost.Create(root);
        host.Dispose();host.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>host.Databases());
        using var reopened=InProcessManagementHost.Open(root);
        Assert.True(Assert.Single(reopened.Databases().Items).IsSystem);
    }

    [Fact]public async Task ShutdownDrainsAnAcceptedOperationIncludingItsReentrantCalls(){
        using var host=InProcessManagementHost.Create(Root());
        var instance=host.Info.InstanceId;
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var running=host.InvokeAsync((_,_)=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(15)));return host.Info.InstanceId;}).AsTask();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));
        var stopping=Task.Run(host.Dispose);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        var closed=false;
        try{
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(DateTime.UtcNow<deadline){
                var error=await Record.ExceptionAsync(()=>host.InfoAsync(cancelled.Token));
                if(error is ObjectDisposedException){closed=true;break;}
                Assert.IsAssignableFrom<OperationCanceledException>(error);
                await Task.Yield();
            }
            Assert.True(closed);
        }finally{release.Set();}
        Assert.Equal(instance,await running);
        await stopping;
    }
}
