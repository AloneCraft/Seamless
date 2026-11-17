using System.Diagnostics;

namespace Seamless.Management.Server;

internal sealed class ObjectExecutionMeasurement{
    private readonly Guid executionId=Guid.NewGuid();
    private readonly DateTimeOffset startedAt=DateTimeOffset.UtcNow;
    private readonly long started=Stopwatch.GetTimestamp();
    private long gateEntered,gateWaitStarted,observed;
    private long? firstOutput;
    internal Guid ExecutionId=>this.executionId;
    internal DateTimeOffset StartedAtUtc=>this.startedAt;
    internal long StartedTimestamp=>this.started;

    internal void EnteredGate()=>this.gateEntered=Stopwatch.GetTimestamp();
    internal void WaitingForGate()=>this.gateWaitStarted=Stopwatch.GetTimestamp();
    internal void ObservedOutput(){
        this.firstOutput??=Stopwatch.GetTimestamp();this.observed++;
    }
    internal QueryExecutionObservation Complete(ObjectExecutionRequest request,string definitionId,DefinitionVersion? version,ObjectExecutionResult? result,Exception? failure){
        var completed=Stopwatch.GetTimestamp();
        TimeSpan? first=null;
        if(this.firstOutput is{} timestamp){first=Stopwatch.GetElapsedTime(this.started,timestamp);}
        long returned=0;
        if(failure is null&&result is not null){returned=result.IsSequence?result.Rows.LongLength:1;}
        var wait=TimeSpan.Zero;TimeSpan? waitOffset=null;
        if(this.gateWaitStarted!=0){var end=this.gateEntered;if(end==0){end=completed;}wait=Stopwatch.GetElapsedTime(this.gateWaitStarted,end);waitOffset=Stopwatch.GetElapsedTime(this.started,this.gateWaitStarted);}
        var outcome="Succeeded";string? error=null;
        if(failure is OperationCanceledException){outcome="Cancelled";error="Cancelled";}
        else if(failure is not null){outcome="Failed";error=failure is ManagementException management?management.Code:failure.GetType().Name;}
        var phase="Execution";if(this.gateEntered==0){phase="DatabaseGate";}else if(version is null){phase="VersionBinding";}
        return new(this.executionId,definitionId,request.ObjectId,version?.Version,version?.ArtifactHash,version is not null,this.startedAt,DateTimeOffset.UtcNow,
            Stopwatch.GetElapsedTime(this.started,completed),wait,first,this.observed,returned,failure is not null||result?.HasMore==true,outcome,phase,error){GateWaitStartedOffset=waitOffset};
    }
    internal static ObjectExecutionStatistics Statistics(QueryExecutionObservation value)=>new(value.ExecutionId,value.DefinitionId,value.ObjectId,value.Version!.Value,value.ArtifactHash!,value.StartedAtUtc,value.CompletedAtUtc,value.Elapsed,value.DatabaseGateWait,value.TimeToFirstOutput,value.ObservedOutputCount,value.ReturnedOutputCount,value.IsPartial){GateWaitStartedOffset=value.GateWaitStartedOffset};
}
