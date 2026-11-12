using System.Reflection;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class QueryExecutionHistoryAuthorization{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AccessRevokedWhileHistoryIoIsPendingCannotReturnThePage(bool disabled){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"history-auth-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Service(store,null,startAuditWorker:false);
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        service.ChangeUser(owner,new("history-reader","TestPassword-456",[new("*",ManagementPermission.Manage)]));
        var reader=service.Authenticate(service.Login(new("history-reader","TestPassword-456")).Token);
        var recorder=typeof(Service).GetField("executionHistory",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
        var periodGate=recorder.GetType().GetField("periodGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(recorder)!;
        using var entered=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        var holding=Task.Run(()=>{lock(periodGate){entered.Set();release.Wait();}});
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        try{
            var reading=service.QueryExecutionHistoryAsync(reader,new("definition","Value"));
            Assert.False(reading.IsCompleted);
            service.ChangeUser(owner,new("history-reader",null,[],disabled));
            release.Set();
            var error=await Assert.ThrowsAsync<ManagementException>(()=>reading);
            var expected="Permission";
            if(disabled){expected="Authentication";}
            Assert.Equal(expected,error.Code);
        }finally{release.Set();await holding;}
        Assert.Empty((await service.QueryExecutionHistoryAsync(owner,new("definition","Value"))).Items);
    }
}
