using Seamless.Management;
using Seamless.Management.Server;
using Host=Seamless.Management.Server.ManagementApplicationHost;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class SharedManagementAdmission{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ManagerAndRpcShareTheConfiguredExecutionCapacity(bool pipe){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"admission-"+Guid.NewGuid().ToString("N"));var root=Path.Combine(directory,"db");
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","AdmissionTestPassword-123",Path.Combine(directory,"launch.json"));
        await using var application=await Host.OpenAsync(new(root,ManagementExecutionMode.Server){Certificate=Path.Combine(root,"server.pfx"),Defaults=new(){Port=0},ConcurrentCapacity=1});
        using var pin=System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));
        using var client=Client(application,pipe,pin);if(!pipe){await client.LoginAsync("admin","AdmissionTestPassword-123");}
        Assert.Single((await client.DatabasesAsync()).Items);
        var operations=Assert.IsType<ConcurrentManagementOperations>(application.Operations);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await operations.InvokeAuthenticatedAsync(Owner,(service,user)=>{NativeWorkflow.Publish(service,user,NativeWorkflow.Database());return true;},CancellationToken.None);
        var version=(await client.ReadRowsAsync(new("Native","Rows"))).Version;
        var holder=operations.InvokeAuthenticatedTaskAsync(()=>new("test-owner","",[new("*",ManagementPermission.All)]),async(_,_)=>{entered.SetResult();await release.Task;return true;},CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));var admission=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);operations.BeforeExecutionAdmission=()=>admission.TrySetResult();var pending=client.DatabasesAsync();
        try{
            await admission.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotSame(pending,await Task.WhenAny(pending,Task.Delay(250)));
            using var cancellation=new CancellationTokenSource();var cancelled=application.Operations.DatabasesAsync(cancellationToken:cancellation.Token);cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cancelled.WaitAsync(TimeSpan.FromSeconds(2)));
            var mutationAdmitted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var mutationCancelled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            operations.BeforeExecutionAdmission=()=>mutationAdmitted.TrySetResult();operations.ExecutionAdmissionCancelled=()=>mutationCancelled.TrySetResult();
            using var mutationCancellation=new CancellationTokenSource();
            var mutation=client.CommitRowsAsync(new("Native",version,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=10}))]),mutationCancellation.Token);
            await mutationAdmitted.Task.WaitAsync(TimeSpan.FromSeconds(10));mutationCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>mutation.WaitAsync(TimeSpan.FromSeconds(2)));
            await mutationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }finally{release.TrySetResult();await holder;}
        Assert.Equal(2,(await pending.WaitAsync(TimeSpan.FromSeconds(5))).Items.Length);
        Assert.Empty((await application.Operations.ReadRowsAsync(new("Native","Rows"))).Rows);
        Assert.Equal(1,(await client.CommitRowsAsync(new("Native",version,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=2,Value=20}))]))).ChangedRows);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RpcExecutionBlocksManagerAndStoppingCancelsWaiters(bool pipe){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"admission-stop-"+Guid.NewGuid().ToString("N"));var root=Path.Combine(directory,"db");
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","AdmissionTestPassword-123",Path.Combine(directory,"launch.json"));
        var application=await Host.OpenAsync(new(root,ManagementExecutionMode.Server){Certificate=Path.Combine(root,"server.pfx"),Defaults=new(){Port=0},ConcurrentCapacity=1});
        var operations=Assert.IsType<ConcurrentManagementOperations>(application.Operations);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service=await operations.InvokeAuthenticatedAsync(Owner,(value,user)=>{
            NativeWorkflow.Publish(value,user,NativeWorkflow.Database());
            NativeWorkflow.Publish(value,user,new("count","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Rows.Count;}")],[])));return value;
        },CancellationToken.None);
        service.BeforeQueryExecution=()=>{entered.TrySetResult();Assert.True(release.Task.Wait(TimeSpan.FromSeconds(15)));};
        using var pin=System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));
        using var client=Client(application,pipe,pin);if(!pipe){await client.LoginAsync("admin","AdmissionTestPassword-123");}
        var execution=ReadCount(client);
        try{
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var admission=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);operations.BeforeExecutionAdmission=()=>admission.TrySetResult();
            var manager=application.Operations.DatabasesAsync();await admission.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(manager.IsCompleted);
            var stopped=application.DisposeAsync().AsTask();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>manager.WaitAsync(TimeSpan.FromSeconds(5)));Assert.False(stopped.IsCompleted);
            release.TrySetResult();await Assert.ThrowsAnyAsync<Exception>(()=>execution.WaitAsync(TimeSpan.FromSeconds(5)));
            await stopped.WaitAsync(TimeSpan.FromSeconds(10));
        }finally{release.TrySetResult();await application.DisposeAsync();}
        using var reopened=new ManagementStore(root);Assert.NotEqual(Guid.Empty,reopened.InstanceId);
    }
    [Fact]public async Task QueuedTcpRequestRechecksAuthenticationAtExecution(){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"admission-auth-"+Guid.NewGuid().ToString("N"));var root=Path.Combine(directory,"db");
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","AdmissionTestPassword-123",Path.Combine(directory,"launch.json"));
        await using var application=await Host.OpenAsync(new(root,ManagementExecutionMode.Server){Certificate=Path.Combine(root,"server.pfx"),Defaults=new(){Port=0},ConcurrentCapacity=1});
        var operations=Assert.IsType<ConcurrentManagementOperations>(application.Operations);var service=await operations.InvokeAuthenticatedAsync(Owner,static(value,_)=>value,CancellationToken.None);
        using var pin=System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));
        using var client=new ManagementClient("localhost",application.TcpPort,pin);await client.LoginAsync("admin","AdmissionTestPassword-123");
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder=operations.InvokeAuthenticatedTaskAsync(Owner,async(_,_)=>{entered.SetResult();await release.Task;return true;},CancellationToken.None);await entered.Task;
        var admitted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);operations.BeforeExecutionAdmission=()=>admitted.TrySetResult();var queued=client.DatabasesAsync();
        try{await admitted.Task.WaitAsync(TimeSpan.FromSeconds(10));service.ChangeUser(Owner(),new("admin",null,[new("*",ManagementPermission.All)],true));}
        finally{release.TrySetResult();await holder;}
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>queued)).Code);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancellationControlRemainsAvailableWhileTheExecutionSlotIsFull(bool pipe){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"admission-cancel-"+Guid.NewGuid().ToString("N"));var root=Path.Combine(directory,"db");
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","AdmissionTestPassword-123",Path.Combine(directory,"launch.json"));
        await using var application=await Host.OpenAsync(new(root,ManagementExecutionMode.Server){Certificate=Path.Combine(root,"server.pfx"),Defaults=new(){Port=0},ConcurrentCapacity=1});
        var operations=Assert.IsType<ConcurrentManagementOperations>(application.Operations);var service=await operations.InvokeAuthenticatedAsync(Owner,static(value,_)=>value,CancellationToken.None);
        var draft=await operations.SaveDraftAsync(new(NativeWorkflow.Database(),0,0));var preview=await operations.PreviewAsync(new("db",draft.Revision));Assert.True(preview.CanApply);
        using var pin=System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));
        using var client=Client(application,pipe,pin);if(!pipe){await client.LoginAsync("admin","AdmissionTestPassword-123");}
        var prepared=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var id=Guid.NewGuid();
        service.PublicationCheckpoint=(phase,_)=>{if(phase=="Prepared"){prepared.TrySetResult();Assert.True(release.Task.Wait(TimeSpan.FromSeconds(15)));}};
        var applicationTask=operations.ApplyAsync(new("db",draft.Revision,preview.Token,id));
        try{
            await prepared.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var response=await client.CallAsync<System.Text.Json.JsonElement>("cancel","Native",id).WaitAsync(TimeSpan.FromSeconds(5));Assert.True(response.GetProperty("accepted").GetBoolean());
        }finally{release.TrySetResult();}
        Assert.Equal(OperationState.Cancelled,(await applicationTask).State);
        Assert.Single((await application.Operations.DatabasesAsync()).Items);
    }
    private static UserAccount Owner()=>new("test-owner","",[new("*",ManagementPermission.All)]);
    private static async Task<int> ReadCount(ManagementClient client){
        var count=-1;await foreach(var row in client.ExecuteAsync("Native",new("count",null,ManagementJson.Element(new{})))){count=row.Value.GetInt32();}return count;
    }
    private static ManagementClient Client(Host application,bool pipe,System.Security.Cryptography.X509Certificates.X509Certificate2 pin){
        if(pipe){return ManagementClient.ForLocalPipe(application.PipeName!);}
        return new("localhost",application.TcpPort,pin);
    }
}
