using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ConcurrentDispatch{
    [Fact]public async Task AsyncOperationsLeaveTheCallersSynchronizationContextBeforeDatabaseWork(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"dispatch-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var operations=new ConcurrentManagementOperations(service);
        var user=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var previous=SynchronizationContext.Current;Task<SynchronizationContext?> result;
        try{
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            result=operations.InvokeAuthenticatedTaskAsync(()=>user,(_,_)=>Task.FromResult(SynchronizationContext.Current),CancellationToken.None);
        }finally{SynchronizationContext.SetSynchronizationContext(previous);}
        Assert.Null(await result.WaitAsync(TimeSpan.FromSeconds(10)));
    }
}
