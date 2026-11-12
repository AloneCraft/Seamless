using System.Diagnostics;
using System.Text.Json;
using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ResourceHeatHistory{
    [Fact]public async Task LargeRecordsUseMultipleChunksAndSurviveReopeningWithExactIdentity(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var trace=Trace(1024);var request=Query(trace);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(trace,ManagementJson.Options).Length>64*2048);
        await using(var recorder=new ResourceHeatHistoryStore(root,budget)){
            Assert.Equal("Queued",recorder.TryRecord(trace).State);await recorder.DisposeAsync();Assert.Equal(1,recorder.Status().Saved);Assert.Equal(0,recorder.Status().SaveFailures);
        }
        Assert.Equal(0,budget.MemoryBytes);
        await using(var reader=new ResourceHeatHistoryStore(root,budget)){
            var read=await reader.ReadAsync(request,CancellationToken.None);var saved=Assert.IsType<ResourceHeatTrace>(read.Trace);saved.Validate();
            Assert.Equal("Saved",saved.Persistence!.State);Assert.Equal(trace.ExecutionId,saved.ExecutionId);Assert.Equal(trace.Clock,saved.Clock);Assert.Equal(trace.Samples,saved.Samples);Assert.Equal(0,read.InvalidPeriods);
            var mismatch=await reader.ReadAsync(request with{ExpectedArtifactHash=new('B',64)},CancellationToken.None);Assert.Null(mismatch.Trace);Assert.Equal("IdentityMismatch",mismatch.UnavailableReason);
        }
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task MissingCheckpointOrDeletedPeriodLeavesHealthyRecordsAvailableWithoutRecreatingFiles(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var damaged=Trace(16);var healthy=Trace(8);
        await using(var recorder=new ResourceHeatHistoryStore(root,budget)){
            Assert.Equal("Queued",recorder.TryRecord(damaged).State);Assert.Equal("Queued",recorder.TryRecord(healthy).State);await recorder.DisposeAsync();Assert.Equal(2,recorder.Status().Saved);
        }
        var periods=Directory.GetDirectories(root,"period-*").Order(StringComparer.Ordinal).ToArray();Assert.Equal(2,periods.Length);
        var manifest=Path.Combine(periods[0],"store.json");File.Delete(manifest);
        await using(var reader=new ResourceHeatHistoryStore(root,budget)){
            var missing=await reader.ReadAsync(Query(damaged),CancellationToken.None);Assert.Null(missing.Trace);Assert.True(missing.InvalidPeriods>0);Assert.False(File.Exists(manifest));
            var valid=await reader.ReadAsync(Query(healthy),CancellationToken.None);Assert.NotNull(valid.Trace);Assert.Equal(healthy.ExecutionId,valid.Trace.ExecutionId);
        }
        Directory.Delete(periods[0],true);
        await using(var reader=new ResourceHeatHistoryStore(root,budget)){
            var valid=await reader.ReadAsync(Query(healthy),CancellationToken.None);Assert.NotNull(valid.Trace);Assert.Equal(0,valid.InvalidPeriods);Assert.False(Directory.Exists(periods[0]));
        }
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task AFullQueueReturnsBeforeTheWriterAndDisposalDrainsAndReleasesMemory(){
        var budget=new ResourceBudget(16L<<20,128L<<20);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var recorder=new ResourceHeatHistoryStore(Root(),budget,new(MaximumPending:1,MaximumPendingBytes:2L<<20),async(_,_)=>{entered.TrySetResult();await release.Task;});
        try{
            Assert.Equal("Queued",recorder.TryRecord(Trace(2)).State);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second=await Task.Run(()=>recorder.TryRecord(Trace(2))).WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal("Dropped",second.State);Assert.Equal("BufferFull",second.Reason);Assert.Equal(1,recorder.Status().PendingCount);
        }finally{release.TrySetResult();await recorder.DisposeAsync();}
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,recorder.Status().PendingCount);Assert.Equal(1,recorder.Status().Saved);Assert.Equal(1,recorder.Status().Dropped);
    }
    [Fact]public async Task RetentionAndAnIncompletePeriodAreIsolatedFromExistingHealthyRecords(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var previous=Trace(4);var latest=Trace(4);
        await using(var recorder=new ResourceHeatHistoryStore(root,budget,new(MaximumPeriods:1))){
            Assert.Equal("Queued",recorder.TryRecord(previous).State);Assert.Equal("Queued",recorder.TryRecord(latest).State);await recorder.DisposeAsync();Assert.Equal(2,recorder.Status().Saved);Assert.Single(Directory.GetDirectories(root,"period-*"));
        }
        var incomplete=Path.Combine(root,"writing-interrupted");Directory.CreateDirectory(incomplete);File.WriteAllText(Path.Combine(incomplete,"partial"),"preserve");
        await using(var reader=new ResourceHeatHistoryStore(root,budget)){
            Assert.Equal("Queued",reader.TryRecord(Trace(1)).State);await reader.DisposeAsync();Assert.Equal("IncompletePeriod",reader.Status().ErrorCode);
            Assert.NotNull((await reader.ReadAsync(Query(latest),CancellationToken.None)).Trace);Assert.Null((await reader.ReadAsync(Query(previous),CancellationToken.None)).Trace);Assert.Equal("preserve",File.ReadAllText(Path.Combine(incomplete,"partial")));
        }
        await using(var reopened=new ResourceHeatHistoryStore(root,budget)){
            var result=await reopened.ReadAsync(Query(latest),CancellationToken.None);Assert.NotNull(result.Trace);Assert.Equal(1,result.InvalidPeriods);Assert.Equal("preserve",File.ReadAllText(Path.Combine(incomplete,"partial")));
        }
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task DuplicateExecutionsAndReadBudgetExhaustionNeverReturnAnArbitraryRecord(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var trace=Trace(4);
        await using(var recorder=new ResourceHeatHistoryStore(root,budget)){
            Assert.Equal("Queued",recorder.TryRecord(trace).State);Assert.Equal("Queued",recorder.TryRecord(trace).State);await recorder.DisposeAsync();Assert.Equal(2,recorder.Status().Saved);
            var duplicate=await recorder.ReadAsync(Query(trace),CancellationToken.None);Assert.Null(duplicate.Trace);Assert.Equal("DuplicateExecution",duplicate.UnavailableReason);
        }
        var small=new ResourceBudget(1024,128L<<20);await using(var reader=new ResourceHeatHistoryStore(root,small)){
            var result=await reader.ReadAsync(Query(trace),CancellationToken.None);Assert.Null(result.Trace);Assert.True(result.ScanLimited);Assert.Equal("ReadMemoryBudget",result.UnavailableReason);Assert.Equal(0,result.InvalidPeriods);
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,small.MemoryBytes);
    }
    internal static ResourceHeatQuery Query(ResourceHeatTrace trace)=>new(trace.DefinitionId,trace.ObjectId,trace.ExecutionId,trace.Version,trace.ArtifactHash);
    private static ResourceHeatTrace Trace(int count){
        var id=Guid.NewGuid();var at=Stopwatch.GetTimestamp();
        return new(id,"history-fixture","Value",1,new('A',64),true,new("synthetic-test-clock",Stopwatch.Frequency,at,DateTimeOffset.UnixEpoch),[new("fixture","Synthetic memory fixture","Memory","Process","WorkingSet","Bytes")],Enumerable.Range(0,count).Select(index=>new ResourceSample("fixture",at+index,at+index,index,null,Aggregation:"Instant")).ToArray(),TimeSpan.FromMilliseconds(100),0,[]);
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"resource-history-"+Guid.NewGuid().ToString("N"));
}
