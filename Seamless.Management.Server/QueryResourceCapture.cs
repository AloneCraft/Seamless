using System.Diagnostics;
using System.Text.Json;
using Seamless.Execution;

namespace Seamless.Management.Server;

/// <summary>DBへ再入しないOS観測。timer callbackを直列化し、終了後のサンプル追加を防ぐ。</summary>
internal sealed class QueryResourceCapture:IDisposable{
    private const int MaximumActive=16;
    internal const int MaximumResponseBytes=1<<20;
    private static readonly string ClockId="stopwatch:"+Environment.ProcessId+":"+Guid.NewGuid().ToString("N");
    private static int active;
    private readonly object gate=new();
    private readonly ResourceHeatOptions options;
    private readonly ResourceReservation reservation;
    private readonly IResourceHeatProvider provider;
    private readonly Dictionary<string,ResourceLane> lanes=new(StringComparer.Ordinal);
    private readonly List<ResourceSample> samples=[];
    private readonly HashSet<string> diagnostics=new(StringComparer.Ordinal){"ResponseOnly: resource samples are not persisted in execution history.","Host and process counters include concurrent work; no query or node attribution is inferred.","Collection overhead is included in the observed execution duration. Actual counter windows may be shorter than the requested cadence and OS counter resolution."};
    private readonly CancellationToken cancellation;
    private Timer? timer;
    private bool stopped,disposed;
    private long dropped,skippedWindows;
    internal QueryResourceCapture(ResourceHeatOptions options,ResourceBudget budget,CancellationToken cancellation,Func<IResourceHeatProvider>? factory=null){
        options.Validate();this.options=options;this.cancellation=cancellation;
        if(Interlocked.Increment(ref active)>MaximumActive){Interlocked.Decrement(ref active);throw new ManagementException("Limit","同時資源計測の上限に達しました。");}
        ResourceReservation? memory=null;IResourceHeatProvider? created=null;
        try{
            // 結果・一時JSON・動的レーン・現在のOS counterを含む上限を事前に親予算へ計上する。
            memory=budget.ReserveMemory(checked((long)options.MaximumSamples*512+(2L<<20)));this.reservation=memory;
            created=(factory??(static()=>new OsResourceHeatProvider()))();this.provider=created;
            this.Read(cancellation);
            var due=options.EffectiveSampleInterval;if(this.samples.Count>=options.MaximumSamples){due=Timeout.InfiniteTimeSpan;}
            this.timer=new(static state=>((QueryResourceCapture)state!).Tick(),this,due,options.EffectiveSampleInterval);
        }catch(Exception error){
            created?.Dispose();memory?.Dispose();Interlocked.Decrement(ref active);
            if(error is ResourceLimitException){throw new ManagementException("Limit","資源計測のメモリ予算を確保できません。");}throw;
        }
    }
    private void Tick(){
        if(this.cancellation.IsCancellationRequested||Volatile.Read(ref this.stopped)){return;}
        if(!Monitor.TryEnter(this.gate)){Interlocked.Increment(ref this.skippedWindows);return;}
        try{
            if(this.stopped||this.cancellation.IsCancellationRequested){return;}
            this.Read(this.cancellation);
        }finally{Monitor.Exit(this.gate);}
    }
    private void Read(CancellationToken cancellation){
        try{
            var reading=this.provider.Read(cancellation);
            foreach(var lane in reading.Lanes.Take(ResourceHeatTrace.MaximumLanes)){
                if(this.lanes.Count<ResourceHeatTrace.MaximumLanes||this.lanes.ContainsKey(lane.Id)){this.lanes[lane.Id]=lane;}
            }
            foreach(var sample in reading.Samples){
                if(!this.lanes.ContainsKey(sample.LaneId)||this.samples.Count>=this.options.MaximumSamples){this.dropped++;continue;}
                this.samples.Add(sample);
            }
            foreach(var diagnostic in reading.Diagnostics.Take(128)){if(this.diagnostics.Count<126){this.diagnostics.Add(diagnostic);}}
            if(this.samples.Count>=this.options.MaximumSamples){this.diagnostics.Add("SampleLimit: collection capacity was reached; later observations are omitted.");this.timer?.Change(Timeout.InfiniteTimeSpan,Timeout.InfiniteTimeSpan);}
        }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){this.diagnostics.Add("CollectionCancelled: the current OS observation was cancelled.");}
        catch(Exception error)when(error is not OutOfMemoryException){if(this.diagnostics.Count<126){this.diagnostics.Add("ProviderFailure: "+error.GetType().Name);}}
    }
    internal ResourceHeatTrace Complete(ObjectExecutionMeasurement measurement,string definitionId,string objectId,DefinitionVersion? version){
        lock(this.gate){
            if(!this.stopped){this.stopped=true;this.timer?.Change(Timeout.InfiniteTimeSpan,Timeout.InfiniteTimeSpan);this.Read(CancellationToken.None);}
            var skipped=Interlocked.Read(ref this.skippedWindows);
            if(skipped>0){this.diagnostics.Add("SkippedTimerWindows: "+skipped.ToString(System.Globalization.CultureInfo.InvariantCulture)+" observations were omitted while the previous OS read was in progress; the number of missing resource samples is unknown.");}
            if(this.samples.Count==0){this.diagnostics.Add("NoData: the execution completed before a usable resource observation.");}
            var trace=new ResourceHeatTrace(measurement.ExecutionId,definitionId,objectId,version?.Version,version?.ArtifactHash,version is not null,
                new(ClockId,Stopwatch.Frequency,measurement.StartedTimestamp,measurement.StartedAtUtc),this.lanes.Values.ToArray(),this.samples.ToArray(),this.options.EffectiveSampleInterval,this.dropped,this.diagnostics.Take(128).ToArray());
            try{trace.Validate();}catch(InvalidDataException){trace=trace with{Lanes=[],Samples=[],DroppedSamples=this.dropped+this.samples.Count,Diagnostics=["ProviderInvalidData: OS observations failed resource trace validation."]};}
            // 通信の既存4MiB上限内に結果本体を残すため、資源資料だけの上限を明示する。
            while(JsonSerializer.SerializeToUtf8Bytes(trace,ManagementJson.Options).Length>MaximumResponseBytes&&trace.Samples.Length>0){
                var keep=trace.Samples.Length/2;this.dropped+=trace.Samples.Length-keep;this.diagnostics.Add("ResponseLimit: resource samples were truncated to the one MiB response budget.");
                trace=trace with{Samples=trace.Samples.Take(keep).ToArray(),DroppedSamples=this.dropped,Diagnostics=this.diagnostics.Take(128).ToArray()};
            }
            if(JsonSerializer.SerializeToUtf8Bytes(trace,ManagementJson.Options).Length>MaximumResponseBytes){
                trace=trace with{Lanes=[],Samples=[],DroppedSamples=this.dropped+trace.Samples.Length,Diagnostics=["ResponseLimit: resource lane metadata exceeded the one MiB response budget."]};
            }
            return trace;
        }
    }
    public void Dispose(){
        lock(this.gate){
            if(this.disposed){return;}this.disposed=true;this.stopped=true;this.timer?.Dispose();this.timer=null;
            try{this.provider.Dispose();}finally{this.reservation.Dispose();Interlocked.Decrement(ref active);}
        }
    }
}
