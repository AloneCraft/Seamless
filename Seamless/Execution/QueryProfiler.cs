using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Seamless.Execution;

public sealed record QueryProfileCallSite(string Path,int Start,int Length,string Caller);
public sealed record QueryProfileTarget(string MethodKey,string SourceHash,string GeneratedBodyHash,QueryProfileCallSite CallSite,string NodeId);
public enum QueryProfileDiagnostic{WorkerNotCollected,HelperBodiesNotExpanded,UnsupportedQueryShape,InputCountNotCollected,ActiveRegionsAtComplete,DisposedBeforeComplete,RegionLimit,OutOfOrderDispose}
public sealed record QueryProfileRegion(long InstanceId,QueryProfileTarget Target,long StartTimestamp,long EndTimestamp,int ManagedThreadId,
    long InputCount,long OutputCount,bool InputCountKnown,bool OutputCountKnown,bool Complete,string Outcome,string Scope="FusedPipelineInclusive");
public sealed record QueryProfileSnapshot(Guid ExecutionId,long StopwatchFrequency,DateTimeOffset StartedAtUtc,long StartedTimestamp,
    ImmutableArray<QueryProfileRegion> Regions,long DroppedRegions,bool Complete,ImmutableArray<QueryProfileDiagnostic> Diagnostics);

/// <summary>呼出し入口で選ぶ計測セッション。Complete後の子ExecutionContextには閉じたholderが残り、別セッションへ転記しない。</summary>
public sealed class QueryProfiler:IDisposable{
    private sealed class Holder(QueryProfiler owner){internal readonly QueryProfiler Owner=owner;}
    private static readonly AsyncLocal<Holder?> ambient=new();
    private readonly object gate=new();
    private readonly Holder holder;
    private readonly Holder? previous;
    private readonly ResourceReservation memory;
    private readonly QueryProfileRegionScope?[] regions;
    private readonly HashSet<QueryProfileDiagnostic> diagnostics=[QueryProfileDiagnostic.WorkerNotCollected,QueryProfileDiagnostic.HelperBodiesNotExpanded];
    private readonly Guid executionId;
    private readonly long started=Stopwatch.GetTimestamp();
    private readonly DateTimeOffset startedAt=DateTimeOffset.UtcNow;
    private int count,closed,disposed;
    private long dropped;
    private QueryProfileSnapshot? snapshot;

    public static QueryProfiler? Current{get{var value=ambient.Value?.Owner;if(value is not null&&Volatile.Read(ref value.closed)==0){return value;}return null;}}
    internal bool Closed=>Volatile.Read(ref this.closed)!=0;
    private QueryProfiler(ResourceBudget budget,Guid executionId,int maximumRegions){
        this.executionId=executionId;this.memory=budget.ReserveMemory(checked(4096L+maximumRegions*512L));
        try{this.regions=new QueryProfileRegionScope[maximumRegions];this.holder=new(this);this.previous=ambient.Value;}
        catch{this.memory.Dispose();throw;}
    }
    public static QueryProfiler Begin(ResourceBudget? budget=null,Guid? executionId=null,int maximumRegions=1024){
        if(maximumRegions is <1 or >1024||executionId==Guid.Empty){throw new ArgumentOutOfRangeException(nameof(maximumRegions));}
        budget??=new(checked(4096L+maximumRegions*512L),0);
        var value=new QueryProfiler(budget,executionId??Guid.NewGuid(),maximumRegions);ambient.Value=value.holder;return value;
    }
    public QueryProfileRegionScope Enter(QueryProfileTarget target,bool countInputs,bool countOutputs=true){
        ArgumentNullException.ThrowIfNull(target);Validate(target);
        lock(this.gate){
            if(this.closed!=0){return new(null,target,0,false,false);}
            if(this.count==this.regions.Length){this.dropped++;this.diagnostics.Add(QueryProfileDiagnostic.RegionLimit);return new(null,target,0,false,false);}
            var region=new QueryProfileRegionScope(this,target,this.count+1,countInputs,countOutputs);this.regions[this.count++]=region;
            if(!countInputs){this.diagnostics.Add(QueryProfileDiagnostic.InputCountNotCollected);}return region;
        }
    }
    public void Report(QueryProfileDiagnostic diagnostic){lock(this.gate){if(this.closed==0){this.diagnostics.Add(diagnostic);}}}
    public QueryProfileSnapshot Complete(){
        lock(this.gate){
            if(this.snapshot is not null){return this.snapshot;}
            Volatile.Write(ref this.closed,1);var now=Stopwatch.GetTimestamp();var complete=true;var rows=ImmutableArray.CreateBuilder<QueryProfileRegion>(this.count);
            for(var index=0;index<this.count;index++){
                var region=this.regions[index]!;if(!region.Finished){complete=false;this.diagnostics.Add(QueryProfileDiagnostic.ActiveRegionsAtComplete);}
                rows.Add(region.Freeze(now));
            }
            if(this.dropped>0){complete=false;}
            this.snapshot=new(this.executionId,Stopwatch.Frequency,this.startedAt,this.started,rows.MoveToImmutable(),this.dropped,complete,this.diagnostics.Order().ToImmutableArray());return this.snapshot;
        }
    }
    public void Dispose(){
        if(Interlocked.Exchange(ref this.disposed,1)!=0){return;}
        try{
            if(ReferenceEquals(ambient.Value,this.holder)){ambient.Value=this.previous;}
            else{this.Report(QueryProfileDiagnostic.OutOfOrderDispose);}
            if(Volatile.Read(ref this.closed)==0){this.Report(QueryProfileDiagnostic.DisposedBeforeComplete);_=this.Complete();}
        }finally{this.memory.Dispose();}
    }
    private static void Validate(QueryProfileTarget target){
        if(string.IsNullOrWhiteSpace(target.MethodKey)||target.MethodKey.Length>4096||string.IsNullOrWhiteSpace(target.NodeId)||target.NodeId.Length>4096||target.SourceHash is not{Length:64}||!target.SourceHash.All(Uri.IsHexDigit)||target.GeneratedBodyHash is not{Length:64}||!target.GeneratedBodyHash.All(Uri.IsHexDigit)||target.CallSite is null||string.IsNullOrWhiteSpace(target.CallSite.Path)||target.CallSite.Path.Length>4096||string.IsNullOrWhiteSpace(target.CallSite.Caller)||target.CallSite.Caller.Length>4096||target.CallSite.Start<0||target.CallSite.Length<=0){throw new ArgumentException("The generated profile target is invalid.",nameof(target));}
    }
}

/// <summary>一つの同期消費区間。既に選ばれた計測経路はセッション終了後も切り替えず、閉じたscopeへの加算を止める。</summary>
public sealed class QueryProfileRegionScope:IDisposable{
    private readonly object gate=new();
    private readonly QueryProfiler? owner;
    private readonly QueryProfileTarget target;
    private readonly long instance;
    private readonly bool inputKnown,outputKnown;
    private readonly long started=Stopwatch.GetTimestamp();
    private readonly int thread=Environment.CurrentManagedThreadId;
    private long input,output,ended;
    private int finished,succeeded;
    private string outcome="Incomplete";
    internal QueryProfileRegionScope(QueryProfiler? owner,QueryProfileTarget target,long instance,bool inputKnown,bool outputKnown){this.owner=owner;this.target=target;this.instance=instance;this.inputKnown=inputKnown;this.outputKnown=outputKnown;}
    internal bool Finished=>Volatile.Read(ref this.finished)!=0;
    public void Input(){if(this.owner is not null&&!this.owner.Closed&&!this.Finished){Interlocked.Increment(ref this.input);}}
    public void Output(){if(this.owner is not null&&!this.owner.Closed&&!this.Finished){Interlocked.Increment(ref this.output);}}
    public void Succeeded(){Volatile.Write(ref this.succeeded,1);this.outcome="Succeeded";}
    public void Fail(Exception error){Volatile.Write(ref this.succeeded,0);this.outcome="Failed";if(error is OperationCanceledException){this.outcome="Cancelled";}}
    public void Dispose(){lock(this.gate){if(this.finished!=0){return;}this.ended=Stopwatch.GetTimestamp();Volatile.Write(ref this.finished,1);}}
    internal QueryProfileRegion Freeze(long now){
        lock(this.gate){
            var end=this.ended;var complete=this.Finished&&end<=now&&Volatile.Read(ref this.succeeded)!=0;
            var outcome=this.outcome;if(!this.Finished||end>now){end=now;outcome="Incomplete";complete=false;}
            return new(this.instance,this.target,this.started,end,this.thread,Interlocked.Read(ref this.input),Interlocked.Read(ref this.output),this.inputKnown,this.outputKnown,complete,outcome);
        }
    }
}
