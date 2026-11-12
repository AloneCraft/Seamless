using System.Diagnostics;
using System.Text.Json;
using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ResourceHeatCollection{
    [Fact]public void ActualOsCountersKeepScopesUnitsAndUnknownDistinctFromZero(){
        using var provider=new OsResourceHeatProvider();var first=provider.Read();var next=provider.Read();
        Assert.Equal("NoData",first.Samples.Single(sample=>sample.LaneId=="process:cpu").State);
        Assert.Contains(next.Lanes,lane=>lane.Id=="process:working-set"&&lane.Scope=="Process"&&lane.Unit=="Bytes");
        Assert.Contains(next.Samples,sample=>sample.LaneId=="process:working-set"&&sample.Value>0&&sample.Aggregation=="Instant");
        var measurement=new ObjectExecutionMeasurement();var trace=new ResourceHeatTrace(measurement.ExecutionId,"fixture","query",null,null,false,new("test-os-clock",Stopwatch.Frequency,measurement.StartedTimestamp,measurement.StartedAtUtc),next.Lanes,next.Samples,TimeSpan.FromMilliseconds(100),0,next.Diagnostics);trace.Validate();
        Assert.All(next.Lanes,lane=>{Assert.True(lane.Scope is "Host" or "Process");if(lane.Kind=="NIC"){Assert.Null(lane.PhysicalResourceId);Assert.NotNull(lane.Topology);}});
        Assert.All(next.Samples,sample=>{Assert.Null(sample.ExecutionId);Assert.Null(sample.Node);if(sample.State!="Available"){Assert.Null(sample.Value);Assert.Null(sample.Percent);Assert.Equal("Unknown",sample.Evidence);}if(sample.Percent is not null){Assert.NotNull(next.Lanes.Single(lane=>lane.Id==sample.LaneId).PercentBasis);}});
        Assert.Contains(next.Diagnostics,message=>message.StartsWith("NotImplemented:",StringComparison.Ordinal));
    }
    [Fact]public void CaptureHasBoundedSamplesAUniqueProcessClockAndReleasesTheParentBudget(){
        var budget=new ResourceBudget(8L<<20,0);var provider=new ScriptedProvider();var measurement=new ObjectExecutionMeasurement();
        ResourceHeatTrace first;
        using(var capture=new QueryResourceCapture(new(MaximumSamples:2),budget,CancellationToken.None,()=>provider)){
            Assert.True(budget.MemoryBytes>0);first=capture.Complete(measurement,"db","Value",null);first.Validate();
            Assert.Equal(measurement.ExecutionId,first.ExecutionId);Assert.Equal(measurement.StartedTimestamp,first.Clock.AnchorTimestamp);Assert.Equal(Stopwatch.Frequency,first.Clock.TicksPerSecond);
            Assert.Equal(2,first.Samples.Length);Assert.Equal(4,first.DroppedSamples);Assert.Contains(first.Diagnostics,message=>message.StartsWith("SampleLimit:",StringComparison.Ordinal));
            var repeated=capture.Complete(measurement,"db","Value",null);Assert.Equal(first.Clock,repeated.Clock);Assert.Equal(first.Samples,repeated.Samples);Assert.Equal(first.DroppedSamples,repeated.DroppedSamples);
        }
        Assert.True(provider.Disposed);Assert.Equal(0,budget.MemoryBytes);
        using(var capture=new QueryResourceCapture(new(MaximumSamples:2),budget,CancellationToken.None,()=>new ScriptedProvider())){
            var second=capture.Complete(new(),"db","Value",null);Assert.Equal(first.Clock.Id,second.Clock.Id);Assert.NotEqual(first.ExecutionId,second.ExecutionId);
        }
        Assert.Equal(0,budget.MemoryBytes);
        Assert.Equal("Limit",Assert.Throws<ManagementException>(()=>new QueryResourceCapture(new(),new ResourceBudget(1024,0),CancellationToken.None,()=>new ScriptedProvider())).Code);
    }
    [Fact]public void ProviderFailureIsReportedWithoutFabricatedResourceValues(){
        var budget=new ResourceBudget(8L<<20,0);var provider=new ScriptedProvider{Failure=true};
        using(var capture=new QueryResourceCapture(new(),budget,CancellationToken.None,()=>provider)){
            var result=capture.Complete(new(),"db","Value",null);result.Validate();Assert.Empty(result.Samples);
            Assert.Contains(result.Diagnostics,message=>message=="ProviderFailure: IOException");Assert.Contains(result.Diagnostics,message=>message.StartsWith("ResponseOnly:",StringComparison.Ordinal));
        }
        Assert.True(provider.Disposed);Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task CompletionWaitsForTheInFlightReaderAndDisposalStopsCollection(){
        var budget=new ResourceBudget(8L<<20,0);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var provider=new ScriptedProvider{Reading=number=>{if(number==2){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException("resource read release");}}}};
        using var cancellation=new CancellationTokenSource();using var capture=new QueryResourceCapture(new(MaximumSamples:64),budget,cancellation.Token,()=>provider);
        try{
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            // 重複timer callbackを決定的に投入し、OS読取のgateを待たず戻ることを検査する。
            var duplicate=Task.Run(()=>typeof(QueryResourceCapture).GetMethod("Tick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(capture,null));
            await duplicate.WaitAsync(TimeSpan.FromSeconds(5));cancellation.Cancel();
            var invoked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ending=Task.Run(()=>{invoked.SetResult();return capture.Complete(new(),"db","Value",null);});await invoked.Task;
            Assert.False(ending.IsCompleted);release.Set();var result=await ending.WaitAsync(TimeSpan.FromSeconds(5));result.Validate();Assert.Contains(result.Diagnostics,message=>message.StartsWith("SkippedTimerWindows:",StringComparison.Ordinal));
            capture.Dispose();Assert.True(provider.Disposed);Assert.Equal(0,budget.MemoryBytes);Assert.Equal(3,provider.ReadCount);
        }finally{release.Set();}
    }
    [Fact]public async Task BothFunctionOriginsReturnRealResourceObservationsOnlyWhenExplicitlyRequestedAndAuthorized(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"heat-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var database=NativeWorkflow.Database();database=database with{Source=database.Source with{Files=database.Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<","public ScalarFunction<int> Value()=>this.ScalarFunction(()=>7);public Table<",StringComparison.Ordinal)}).ToArray()}};
        NativeWorkflow.Publish(service,owner,database);
        NativeWorkflow.Publish(service,owner,new("heat-entry","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context)=>7;}")],[])));
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Contains("ResourceHeatV1",(await client.InfoAsync()).Capabilities);
        var target=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","heat-entry","heat-entry");
        var plain=await client.ExecuteFunctionAsync(new(target,[]));Assert.Null(plain.ResourceHeat);Assert.Null(plain.Statistics);
        var measured=await client.ExecuteFunctionAsync(new(target,[]){ResourceHeatOptions=new(),MeasurementMode=ObjectExecutionMeasurementMode.Summary});
        Assert.Equal(7,measured.Value!.Value.GetInt32());var heat=Assert.IsType<ResourceHeatTrace>(measured.ResourceHeat);heat.Validate();Assert.Equal(measured.Statistics!.ExecutionId,heat.ExecutionId);Assert.Equal(measured.Statistics.StartedAtUtc,heat.Clock.AnchorUtc);Assert.Equal(measured.Statistics.ArtifactHash,heat.ArtifactHash);Assert.Equal("heat-entry",heat.DefinitionId);
        Assert.Equal("Queued",heat.Persistence!.State);
        using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15))){
            ResourceHeatReadResult saved;
            do{
                saved=await client.QueryResourceHeatAsync(ResourceHeatHistory.Query(heat),deadline.Token);
                Assert.True(saved.Status.SaveFailures==0,JsonSerializer.Serialize(saved,ManagementJson.Options));
                if(saved.Trace is null){await Task.Delay(25,deadline.Token);}
            }while(saved.Trace is null);
            Assert.Equal("Saved",saved.Trace.Persistence!.State);Assert.Equal(heat.Samples,saved.Trace.Samples);Assert.Equal(heat.Clock,saved.Trace.Clock);
        }
        var direct=await client.ExecuteFunctionAsync(new(new(FunctionOrigin.DatabaseMember,"db:db","db","Value"),[]){ResourceHeatOptions=new()});
        Assert.Null(direct.Statistics);Assert.Null(direct.TraceExecutionId);var directHeat=Assert.IsType<ResourceHeatTrace>(direct.ResourceHeat);directHeat.Validate();Assert.Equal("db",directHeat.DefinitionId);Assert.Equal("Value",directHeat.ObjectId);Assert.True(directHeat.IdentityResolved);
        Assert.Contains(directHeat.Samples,sample=>sample.LaneId=="process:working-set");Assert.NotEqual(heat.ExecutionId,directHeat.ExecutionId);Assert.Equal(heat.Clock.Id,directHeat.Clock.Id);
        var reader=new UserAccount("reader","",[new("Native",ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.Execute)]);
        Assert.Equal(7,(await service.ExecuteFunctionAsync(reader,new(target,[]))).Value!.Value.GetInt32());
        var denied=await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteFunctionAsync(reader,new(target,[]){ResourceHeatOptions=new()}));Assert.Equal("Permission",denied.Code);
        var objectDenied=await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteObjectAsync(reader,new("Native","Value",[]){ResourceHeatOptions=new()}));Assert.Equal("Permission",objectDenied.Code);
        var readDenied=await Assert.ThrowsAsync<ManagementException>(()=>service.QueryResourceHeatAsync(reader,ResourceHeatHistory.Query(heat)));Assert.Equal("Permission",readDenied.Code);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(measured,ManagementJson.Options).Length<4<<20);
    }
    private sealed class ScriptedProvider:IResourceHeatProvider{
        internal bool Disposed,Failure;
        internal int ReadCount;
        internal Action<int>? Reading;
        public ResourceHeatReading Read(CancellationToken cancellationToken=default){
            ObjectDisposedException.ThrowIf(this.Disposed,this);var number=Interlocked.Increment(ref this.ReadCount);this.Reading?.Invoke(number);
            if(this.Failure){throw new IOException("fixture failure");}var at=Stopwatch.GetTimestamp();
            return new([new("fixture-memory","Synthetic memory fixture","Memory","Process","WorkingSet","Bytes")],Enumerable.Range(0,3).Select(_=>new ResourceSample("fixture-memory",at,at,number,null,Aggregation:"Instant")).ToArray(),[]);
        }
        public void Dispose()=>this.Disposed=true;
    }
}
