using System.Diagnostics;
using MemoryPack;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ExecutionGateMeasurement{
    [Fact]public async Task InitializationTimeIsSeparatedFromTheActualGateWait(){
        var measurement=new ObjectExecutionMeasurement();
        // OSカウンター初期化など、DBゲートを待っていない既知の処理時間を作る。
        await Task.Delay(20);
        var beforeWait=Stopwatch.GetTimestamp();measurement.WaitingForGate();var afterWait=Stopwatch.GetTimestamp();
        var beforeEntered=Stopwatch.GetTimestamp();measurement.EnteredGate();var afterEntered=Stopwatch.GetTimestamp();
        var value=measurement.Complete(new("DB","Query",[]),"db",null,new(1,false,null,[],[],false),null);
        Assert.NotNull(value.GateWaitStartedOffset);
        Assert.InRange(value.GateWaitStartedOffset.Value,Stopwatch.GetElapsedTime(measurement.StartedTimestamp,beforeWait),Stopwatch.GetElapsedTime(measurement.StartedTimestamp,afterWait));
        Assert.InRange(value.DatabaseGateWait,Stopwatch.GetElapsedTime(afterWait,beforeEntered),Stopwatch.GetElapsedTime(beforeWait,afterEntered));
        Assert.True(value.GateWaitStartedOffset+value.DatabaseGateWait<=value.Elapsed);
    }
    [Fact]public void FailureBeforeAdmissionHasNoFabricatedGateWait(){
        var value=new ObjectExecutionMeasurement().Complete(new("DB","Query",[]),"db",null,null,new OperationCanceledException());
        Assert.Equal(TimeSpan.Zero,value.DatabaseGateWait);Assert.Null(value.GateWaitStartedOffset);Assert.Equal("Cancelled",value.Outcome);
    }
    [Fact]public void ExistingMemoryPackHistoryRemainsReadableAndNewOffsetRoundTrips(){
        var id=Guid.NewGuid();var old=new LegacyQueryExecutionObservation(id,"db","Query",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(10),TimeSpan.FromMilliseconds(10),TimeSpan.FromMilliseconds(2),TimeSpan.FromMilliseconds(5),1,1,false,"Succeeded","Execution",null);
        var value=MemoryPackSerializer.Deserialize<QueryExecutionObservation>(MemoryPackSerializer.Serialize(old))!;
        Assert.Equal(id,value.ExecutionId);Assert.Equal(old.Elapsed,value.Elapsed);Assert.Null(value.GateWaitStartedOffset);
        var current=value with{GateWaitStartedOffset=TimeSpan.FromMilliseconds(3)};
        Assert.Equal(current.GateWaitStartedOffset,MemoryPackSerializer.Deserialize<QueryExecutionObservation>(MemoryPackSerializer.Serialize(current))!.GateWaitStartedOffset);
    }
}

[MemoryPackable]public sealed partial record LegacyQueryExecutionObservation(Guid ExecutionId,string DefinitionId,string ObjectId,long? Version,string? ArtifactHash,bool IdentityResolved,
    DateTimeOffset StartedAtUtc,DateTimeOffset CompletedAtUtc,TimeSpan Elapsed,TimeSpan DatabaseGateWait,TimeSpan? TimeToFirstOutput,
    long ObservedOutputCount,long ReturnedOutputCount,bool IsPartial,string Outcome,string Phase,string? ErrorCode);
