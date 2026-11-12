using System.Reflection;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class QueryTraceSamplingIntegration{
    [Fact]public async Task RealServiceTimerPersistsGateWaitSampleBeforeTheQueryCanComplete(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"trace-sampling-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var definition=Designer.ApplyQuery(Designer.Empty("sampling-db","SamplingDb"),new("Value","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 7;"));
        NativeWorkflow.Publish(service,owner,definition);
        var runtime=(NativeRuntime)typeof(Service).GetField("runtime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var holding=Task.Run(()=>runtime.GetType().GetMethod("WithDatabaseIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(typeof(int)).Invoke(runtime,["sampling-db",CancellationToken.None,(Func<int>)(()=>{entered.Set();release.Wait();return 0;})]));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task<ObjectExecutionResult>? execution=null;
        QueryTraceFrame? observed=null;
        try{
            var cursor=await service.WatchQueryTraceAsync(owner,new("sampling-db","Value"),deadline.Token);
            execution=service.ExecuteObjectAsync(owner,new("SamplingDb","Value",[]){TraceOptions=new(QueryTraceRecordMode.TimeSampled)},deadline.Token);
            while(observed is null){
                var page=await service.QueryTraceAsync(owner,new("sampling-db","Value"),deadline.Token);
                Assert.Equal(0,page.InvalidFiles);Assert.Equal(0,page.Status.SaveFailures);
                observed=page.Frames.FirstOrDefault(frame=>frame.Kind=="Sample"&&frame.Phase=="DatabaseGate");
                if(observed is not null){break;}
                cursor=await service.WatchQueryTraceAsync(owner,new("sampling-db","Value",cursor.HubSession,cursor.Revision,TimeoutMilliseconds:2000),deadline.Token);
            }
            Assert.False(execution.IsCompleted);Assert.False(observed.IdentityResolved);Assert.Null(observed.Version);Assert.Null(observed.ArtifactHash);Assert.Equal(0,observed.ObservedOutputCount);Assert.True(observed.Elapsed>=TimeSpan.FromMilliseconds(100));
            release.Set();await holding.WaitAsync(TimeSpan.FromSeconds(10));
            var result=await execution.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal(7,result.Value!.Value.GetInt32());Assert.Equal(observed.ExecutionId,result.TraceExecutionId);
            await runtime.TraceRecorder!.DisposeAsync();
            var completed=await service.QueryTraceAsync(owner,new("sampling-db","Value",observed.ExecutionId));
            Assert.Contains(completed.Frames,frame=>frame.Kind=="Sample"&&frame.Phase=="DatabaseGate");
            Assert.Equal("Completed",completed.Frames[^1].Kind);Assert.Equal("Succeeded",completed.Frames[^1].Outcome);Assert.True(completed.Frames[^1].IdentityResolved);Assert.Equal(1,completed.Frames[^1].ObservedOutputCount);
        }finally{
            release.Set();deadline.Cancel();await holding.WaitAsync(TimeSpan.FromSeconds(10));
            if(execution is not null){try{await execution.WaitAsync(TimeSpan.FromSeconds(10));}catch(OperationCanceledException){}}
        }
    }
}
