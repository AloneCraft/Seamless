using System.Reflection;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;
using Designer=Seamless.Management.DatabaseDesigner;
using Server=Seamless.Management.Server.ManagementServer;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class QueryTraceIntegration{
    [Fact]public async Task NormalEntryPointRecordsIdentityOutcomesAndCancelledGateWithoutChangingResults(){
        var root=Root();using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Service(store,null,startAuditWorker:false);
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var definition=Designer.ApplyQuery(Designer.Empty("trace-db","TraceDb"),new("Value","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 7;"));
        definition=Designer.ApplyQuery(definition,new("Fail","",DatabaseQueryKind.ScalarFunction,[],[],"int","throw new System.InvalidOperationException(\"original failure\");"));
        NativeWorkflow.Publish(service,owner,definition);
        var plain=await service.ExecuteObjectAsync(owner,new("TraceDb","Value",[]));
        Assert.Null(plain.TraceExecutionId);Assert.Null(plain.Statistics);
        Assert.False(Directory.Exists(Path.Combine(root,"query-trace-tsavorite-v1")));
        var ids=new List<Guid>();
        foreach(var options in new[]{new QueryTraceOptions(),new(QueryTraceRecordMode.BoundaryEvents),new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete)}){
            var result=await service.ExecuteObjectAsync(owner,new("TraceDb","Value",[]){TraceOptions=options});
            Assert.Equal(7,result.Value!.Value.GetInt32());Assert.Null(result.Statistics);
            ids.Add(Assert.IsType<Guid>(result.TraceExecutionId));
        }
        var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ExecuteObjectAsync(owner,new("TraceDb","Fail",[]){TraceOptions=new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete)}));
        Assert.Equal("original failure",failure.Message);
        var runtime=Runtime(service);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var holding=Task.Run(()=>runtime.GetType().GetMethod("WithDatabaseIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(typeof(int)).Invoke(runtime,["trace-db",CancellationToken.None,(Func<int>)(()=>{entered.Set();release.Wait();return 0;})]));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        try{
            using var cancellation=new CancellationTokenSource();
            var waiting=service.ExecuteObjectAsync(owner,new("TraceDb","Value",[]){TraceOptions=new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete)},cancellation.Token);
            Assert.False(waiting.IsCompleted);cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>waiting);
        }finally{release.Set();await holding;}
        await runtime.TraceRecorder!.DisposeAsync();
        foreach(var id in ids){
            var page=await service.QueryTraceAsync(owner,new("trace-db","Value",id));
            Assert.Equal(new[]{"Started","GateAcquired","IdentityBound","FirstOutput","Completed"},page.Frames.Where(frame=>frame.Kind!="Sample").Select(frame=>frame.Kind));
            Assert.Equal("Succeeded",page.Frames[^1].Outcome);Assert.Equal(1,page.Frames[^1].ObservedOutputCount);
            Assert.All(page.Frames.SkipWhile(frame=>frame.Kind!="IdentityBound"),frame=>{Assert.True(frame.IdentityResolved);Assert.Equal(1,frame.Version);Assert.NotNull(frame.ArtifactHash);});
            Assert.Equal(0,page.InvalidFiles);Assert.Equal(0,page.Status.SaveFailures);Assert.Equal(0,page.Status.ActiveCount);
        }
        var failed=await service.QueryTraceAsync(owner,new("trace-db","Fail"));
        Assert.True(failed.Frames[^1].Outcome=="Failed",System.Text.Json.JsonSerializer.Serialize(failed));
        var all=await service.QueryTraceAsync(owner,new("trace-db","Value"));
        var cancelled=Assert.Single(all.Frames,frame=>frame.Outcome=="Cancelled");Assert.False(cancelled.IdentityResolved);Assert.Equal("DatabaseGate",cancelled.Phase);
        Assert.Equal(7,(await service.ExecuteObjectAsync(owner,new("TraceDb","Value",[]))).Value!.Value.GetInt32());
    }

    [Fact]public async Task OwnerAndIpcWatchDoNotHoldTheOnlyQueryExecutionSlot(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);
        await using var operations=new ConcurrentManagementOperations(service,capacity:1);
        await using var server=Server.ForLocalOwner(service);server.ShareExecutionSlots(operations);server.Start();
        using var ipc=ManagementClient.ForLocalPipe(server.PipeName!);
        var hub=(QueryTraceNotifications)typeof(Service).GetField("traceNotifications",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
        foreach(var endpoint in new IManagementOperations[]{operations,ipc}){
            var initial=await endpoint.WatchQueryTraceAsync(new("trace-db","Value"));
            var waiting=endpoint.WatchQueryTraceAsync(new("trace-db","Value",initial.HubSession,initial.Revision,TimeoutMilliseconds:5000));
            Assert.True(SpinWait.SpinUntil(()=>{
                var count=(int)typeof(QueryTraceNotifications).GetField("watches",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(hub)!;
                return count==1;
            },TimeSpan.FromSeconds(2)));
            var info=await operations.InfoAsync().WaitAsync(TimeSpan.FromSeconds(2));Assert.NotNull(info);Assert.False(waiting.IsCompleted);
            hub.Pulse("trace-db","Value");Assert.True((await waiting.WaitAsync(TimeSpan.FromSeconds(2))).Changed);
            Assert.Empty((await endpoint.QueryTraceAsync(new("trace-db","Value"))).Frames);
        }
    }

    [Fact]public async Task WatchRechecksPermissionsBeforeReturningAnEvent(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        service.ChangeUser(owner,new("reader","TestPassword-456",[new("*",ManagementPermission.Manage)]));
        var reader=service.Authenticate(service.Login(new("reader","TestPassword-456")).Token);
        var initial=await service.WatchQueryTraceAsync(reader,new("trace-db","Value"));
        var waiting=service.WatchQueryTraceAsync(reader,new("trace-db","Value",initial.HubSession,initial.Revision));
        Assert.False(waiting.IsCompleted);service.ChangeUser(owner,new("reader",null,[]));
        var hub=(QueryTraceNotifications)typeof(Service).GetField("traceNotifications",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
        hub.Pulse("trace-db","Value");Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>waiting)).Code);
    }
    private static NativeRuntime Runtime(Service service)=>(NativeRuntime)typeof(Service).GetField("runtime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"trace-integration-"+Guid.NewGuid().ToString("N"));
}
