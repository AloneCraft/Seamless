namespace Seamless.Management.Server;

/// <summary>クエリ全体の観測状態。保存先やDBオブジェクトを保持しない。</summary>
internal sealed class QueryTraceCapture{
    private readonly object gate=new();
    private readonly Guid executionId;
    private readonly string definitionId,objectId;
    private readonly QueryTraceOptions options;
    private readonly DateTimeOffset startedUtc;
    private readonly long started;
    private readonly TimeProvider clock;
    private readonly Func<QueryTraceFrame,QueryTraceReliability,CancellationToken,bool> record;
    private readonly Action<long> lateSamples;
    private readonly Action failure;
    private readonly CancellationToken cancellation;
    private long sequence,observed,nextSampleTicks;
    private long? version;
    private string? artifact;
    private string phase="DatabaseGate";
    private bool firstOutput,stopped,terminal;
    internal QueryTraceCapture(Guid executionId,string definitionId,string objectId,QueryTraceOptions options,DateTimeOffset startedUtc,long started,
        Func<QueryTraceFrame,QueryTraceReliability,CancellationToken,bool> record,Action<long> lateSamples,Action failure,CancellationToken cancellation,TimeProvider? clock=null){
        options.Validate();this.executionId=executionId;this.definitionId=definitionId;this.objectId=objectId;this.options=options;this.startedUtc=startedUtc;this.started=started;
        this.record=record;this.lateSamples=lateSamples;this.failure=failure;this.cancellation=cancellation;this.clock=clock??TimeProvider.System;
        this.nextSampleTicks=options.EffectiveSampleInterval.Ticks;
    }
    internal void Started()=>this.Boundary("Started","DatabaseGate");
    internal void EnteredGate()=>this.Boundary("GateAcquired","VersionBinding");
    internal void Bound(long version,string artifact){
        lock(this.gate){if(this.stopped){return;}this.version=version;this.artifact=artifact;this.phase="Preparation";this.Record(this.Frame("IdentityBound"),this.cancellation,false);}
    }
    internal void Invoking(){lock(this.gate){if(!this.stopped){this.phase="Execution";}}}
    internal void Output(){
        lock(this.gate){
            if(this.stopped){return;}this.observed++;
            if(!this.firstOutput){this.firstOutput=true;this.Record(this.Frame("FirstOutput"),this.cancellation,false);}
        }
    }
    private void Boundary(string kind,string phase){
        lock(this.gate){if(this.stopped){return;}this.phase=phase;this.Record(this.Frame(kind),this.cancellation,false);}
    }
    internal void Sample(){
        if(this.options.RecordMode!=QueryTraceRecordMode.TimeSampled){return;}
        lock(this.gate){
            if(this.stopped){return;}
            var elapsed=this.clock.GetElapsedTime(this.started).Ticks;
            if(elapsed<this.nextSampleTicks){return;}
            var skipped=(elapsed-this.nextSampleTicks)/this.options.EffectiveSampleInterval.Ticks;
            var untilNext=this.options.EffectiveSampleInterval.Ticks-elapsed%this.options.EffectiveSampleInterval.Ticks;
            this.nextSampleTicks=elapsed>long.MaxValue-untilNext?long.MaxValue:elapsed+untilNext;
            if(skipped>0){this.lateSamples(skipped);}this.Record(this.Frame("Sample",TimeSpan.FromTicks(elapsed)),CancellationToken.None,false);
        }
    }
    // 実行中の投入も同じgateで完了させてから終端時計を採取する。
    internal void StopSampling(){lock(this.gate){this.stopped=true;}}
    internal void End(QueryExecutionObservation observation,CancellationToken recorderStopping){
        lock(this.gate){
            if(this.terminal){return;}this.terminal=true;this.stopped=true;
            var frame=new QueryTraceFrame(this.executionId,++this.sequence,this.definitionId,this.objectId,observation.Version,observation.ArtifactHash,observation.IdentityResolved,
                this.startedUtc,observation.Elapsed,"Completed",observation.Phase,observation.ObservedOutputCount,observation.Outcome,this.options.RecordMode,this.options.Reliability);
            // 終了したクエリ自身の取消で、取消終端の記録まで取り消さない。
            this.Record(frame,recorderStopping,true);
        }
    }
    private QueryTraceFrame Frame(string kind,TimeSpan? elapsed=null)=>new(this.executionId,++this.sequence,this.definitionId,this.objectId,this.version,this.artifact,this.version is not null,
        this.startedUtc,elapsed??this.clock.GetElapsedTime(this.started),kind,this.phase,this.observed,null,this.options.RecordMode,this.options.Reliability);
    private void Record(QueryTraceFrame frame,CancellationToken token,bool ending){
        // sinkはDB・captureへ再入しない独立writerへ投入する。Completeは呼出スレッドで容量待機し、TimeSampledは非待機で投入する。
        try{_=this.record(frame,this.options.Reliability,token);}
        catch(OperationCanceledException)when(!ending&&this.cancellation.IsCancellationRequested){throw;}
        catch(Exception){this.failure();}
    }
}
