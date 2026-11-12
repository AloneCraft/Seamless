using System.Buffers.Binary;
using System.Security.Cryptography;
using MemoryPack;
using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class QueryTraceRecorderTests{
    [Fact]public async Task IdleAndUnusedCompleteReservationCreateNoDirectoryAndReleaseMemory(){
        var root=Root();var budget=new ResourceBudget(1L<<20,0);var hub=new QueryTraceNotifications(budget);
        await using var recorder=new QueryTraceRecorder(root,budget,hub);
        Assert.False(Directory.Exists(root));
        using(var recording=recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),CancellationToken.None)){
            Assert.Equal(1,recorder.Status().ActiveCount);Assert.True(budget.MemoryBytes>0);Assert.False(Directory.Exists(root));
        }
        Assert.Equal(0,recorder.Status().ActiveCount);Assert.Equal(0,budget.MemoryBytes);hub.Close();
    }
    [Fact]public async Task ParentMemoryShortageWithNoPendingWriterFailsWithoutWaiting(){
        var budget=new ResourceBudget(1024,0);var hub=new QueryTraceNotifications(budget);await using var recorder=new QueryTraceRecorder(Root(),budget,hub);
        var error=Assert.Throws<ManagementException>(()=>recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),CancellationToken.None));
        Assert.Equal("Limit",error.Code);Assert.Equal(0,recorder.WaitingCount);Assert.Equal(0,budget.MemoryBytes);hub.Close();
    }
    [Fact]public async Task CompleteWaitsForIndependentWriterAndBestEffortDropsWithoutBlocking(){
        var budget=new ResourceBudget(1L<<20,0);var hub=new QueryTraceNotifications(budget);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var recorder=new QueryTraceRecorder(Root(),budget,hub,new(MaximumPending:1),async(_,_)=>{entered.TrySetResult();await release.Task;});
        using var first=recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),CancellationToken.None);
        using var best=recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.BestEffort),CancellationToken.None);
        Assert.True(first.Record(Frame(Guid.NewGuid(),1,QueryTraceReliability.Complete),QueryTraceReliability.Complete,CancellationToken.None));await entered.Task;
        Assert.False(best.Record(Frame(Guid.NewGuid(),1,QueryTraceReliability.BestEffort),QueryTraceReliability.BestEffort,CancellationToken.None));
        var complete=Task.Run(()=>{var thread=Environment.CurrentManagedThreadId;using var next=recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),CancellationToken.None);return thread==Environment.CurrentManagedThreadId;});
        try{Assert.True(SpinWait.SpinUntil(()=>recorder.WaitingCount==1,TimeSpan.FromSeconds(10)));Assert.False(complete.IsCompleted);}
        finally{release.TrySetResult();}
        Assert.True(await complete.WaitAsync(TimeSpan.FromSeconds(10)));await recorder.DisposeAsync();hub.Close();
        Assert.Equal(1,recorder.Status().Saved);Assert.Equal(1,recorder.Status().Dropped);Assert.True(recorder.Status().BackpressureTicks>0);Assert.Equal(0,budget.MemoryBytes);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CompleteCapacityWaitIsReleasedByCancellationOrHostStop(bool stop){
        var budget=new ResourceBudget(1L<<20,0);var hub=new QueryTraceNotifications(budget);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var recorder=new QueryTraceRecorder(Root(),budget,hub,new(MaximumPending:1),async(_,_)=>{entered.TrySetResult();await release.Task;});
        using var first=recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),CancellationToken.None);
        Assert.True(first.Record(Frame(Guid.NewGuid(),1,QueryTraceReliability.Complete),QueryTraceReliability.Complete,CancellationToken.None));await entered.Task;
        using var cancellation=new CancellationTokenSource();var waiting=Task.Run(()=>recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),cancellation.Token));
        try{
            Assert.True(SpinWait.SpinUntil(()=>recorder.WaitingCount==1,TimeSpan.FromSeconds(10)));
            if(stop){_=recorder.DisposeAsync();Assert.Equal("Stopped",(await Assert.ThrowsAsync<ManagementException>(async()=>{using var ignored=await waiting;})).Code);}
            else{cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{using var ignored=await waiting;});}
        }finally{release.TrySetResult();await recorder.DisposeAsync();hub.Close();}
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,recorder.Status().ActiveCount);
    }
    [Fact]public async Task MultipleExecutionsWithSameFrameSequencePersistWithoutPhysicalKeyCollision(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var hub=new QueryTraceNotifications(budget);
        await using var recorder=new QueryTraceRecorder(root,budget,hub);
        var executions=new[]{Guid.NewGuid(),Guid.NewGuid()};
        foreach(var execution in executions){using var recording=recorder.Prepare(new(QueryTraceRecordMode.BoundaryEvents),CancellationToken.None);Assert.True(recording.Record(Frame(execution,1,QueryTraceReliability.BestEffort),QueryTraceReliability.BestEffort,CancellationToken.None));}
        await recorder.DisposeAsync();hub.Close();
        var page=await recorder.ReadAsync(new("definition","Value"),CancellationToken.None);
        Assert.Equal(2,page.Frames.Length);Assert.Equal(0,page.InvalidFiles);Assert.Equal(2,page.Status.Saved);Assert.Equal(0,page.Status.Dropped);
        Assert.Equal(executions.Order(),page.Frames.Select(frame=>frame.ExecutionId).Order());Assert.All(page.Frames,frame=>Assert.Equal(1,frame.Sequence));Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public void LegacyHistorySealRemainsReadableAndTracePayloadKindIsRejectedByHistoryReader(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var periods=new ExecutionHistoryPeriods(root,budget);
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        var entry=new QueryExecutionHistoryEntry(Guid.NewGuid(),1,observation);var period=periods.Write([entry]);var sealPath=Path.Combine(period,"period.seal");
        var current=MemoryPackSerializer.Deserialize<ExecutionPeriodSeal>(File.ReadAllBytes(sealPath).AsSpan(44))!;
        var legacy=MemoryPackSerializer.Serialize(new ExecutionHistoryPeriodSeal(1,current.SessionId,current.First,current.Last,current.Count,current.Checkpoint,current.Files));
        var bytes=new byte[44+legacy.Length];"SEHIST01"u8.CopyTo(bytes);BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8),legacy.Length);SHA256.HashData(legacy).CopyTo(bytes,12);legacy.CopyTo(bytes,44);File.WriteAllBytes(sealPath,bytes);
        Assert.Equal(entry,Assert.Single(periods.Read(period,CancellationToken.None)));
        var trace=new ExecutionHistoryPeriods(root,budget,"Trace");var tracePeriod=trace.WriteRows([ExecutionHistoryPeriods.EncodePayload(2,Guid.NewGuid(),MemoryPackSerializer.Serialize(Frame(Guid.NewGuid(),1,QueryTraceReliability.BestEffort)))]);
        Assert.Throws<InvalidDataException>(()=>periods.Read(tracePeriod,CancellationToken.None));Assert.Single(trace.ReadRows(tracePeriod,CancellationToken.None));Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task ExecutionPagingReturnsEveryFrameOnceAndTargetOverviewKeepsTheLatestCompletion(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var hub=new QueryTraceNotifications(budget);var execution=Guid.NewGuid();var session=Guid.NewGuid();
        var frames=Enumerable.Range(1,5).Select(index=>Frame(execution,index,QueryTraceReliability.BestEffort)).ToArray();frames[^1]=frames[^1] with{Kind="Completed",Outcome="Succeeded"};
        _=new ExecutionHistoryPeriods(root,budget,"Trace").WriteRows(frames.Select(frame=>ExecutionHistoryPeriods.EncodePayload(frame.Sequence,session,MemoryPackSerializer.Serialize(frame))).ToArray());
        await using var recorder=new QueryTraceRecorder(root,budget,hub);
        var first=await recorder.ReadAsync(new("definition","Value",execution,Limit:2),CancellationToken.None);
        var second=await recorder.ReadAsync(new("definition","Value",execution,AfterSequence:2,Limit:2),CancellationToken.None);
        var third=await recorder.ReadAsync(new("definition","Value",execution,AfterSequence:4,Limit:2),CancellationToken.None);
        Assert.Equal(new long[]{1,2,3,4,5},first.Frames.Concat(second.Frames).Concat(third.Frames).Select(frame=>frame.Sequence));Assert.True(first.HasMore);Assert.True(second.HasMore);Assert.False(third.HasMore);
        var overview=await recorder.ReadAsync(new("definition","Value",Limit:2),CancellationToken.None);
        Assert.Equal(new long[]{4,5},overview.Frames.Select(frame=>frame.Sequence));Assert.Equal("Completed",overview.Frames[^1].Kind);Assert.True(overview.HasMore);
        hub.Close();Assert.Equal(0,budget.MemoryBytes);
    }
    private static QueryTraceFrame Frame(Guid execution,long sequence,QueryTraceReliability reliability)=>new(execution,sequence,"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,TimeSpan.FromMilliseconds(sequence),"FirstOutput","Execution",1,null,QueryTraceRecordMode.BoundaryEvents,reliability);
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"trace-recorder-"+Guid.NewGuid().ToString("N"));
}
