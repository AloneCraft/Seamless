using System.Diagnostics;
using MemoryPack;
using Seamless.Execution;

namespace Seamless.Management.Server;

internal sealed record QueryTraceRecorderOptions(int MaximumPending=256,long MaximumPendingBytes=1L<<20,int MaximumFiles=4096,long MaximumDiskBytes=32L<<20,int MaximumActive=32);

/// <summary>境界とサンプルの記録。容量待ちとwriterはDB・captureへ再入しない。</summary>
internal sealed class QueryTraceRecorder:IAsyncDisposable{
    private const long FrameBytes=65536;
    private readonly object gate=new(),periodGate=new();
    private readonly Queue<Pending> queue=new();
    private readonly HashSet<QueryTraceRecording> recordings=[];
    private readonly SemaphoreSlim signal=new(0);
    private readonly CancellationTokenSource stopping=new();
    private readonly ResourceBudget budget;
    private readonly QueryTraceNotifications notifications;
    private readonly QueryTraceRecorderOptions options;
    private readonly ExecutionHistoryPeriods periods;
    private readonly string directory;
    private readonly Func<QueryTraceFrame,CancellationToken,Task>? writeOverride;
    private readonly Task worker;
    private readonly Guid session=Guid.NewGuid();
    private long sequence,captured,saved,dropped,failures,bytes,backpressure,late;
    private int pending,waiting;
    private string? error;
    private bool closed;
    private Task? disposal;
    private sealed record Pending(long Key,QueryTraceFrame Frame,ResourceReservation Memory);
    private sealed class RetentionException(Exception inner):IOException("トレースの保持上限を確保できませんでした。",inner);
    internal QueryTraceRecorder(string directory,ResourceBudget budget,QueryTraceNotifications notifications,QueryTraceRecorderOptions? options=null,Func<QueryTraceFrame,CancellationToken,Task>? writeOverride=null){
        this.directory=directory;this.budget=budget;this.notifications=notifications;this.options=options??new();this.writeOverride=writeOverride;this.periods=new(directory,budget,"Trace");
        if(this.options.MaximumPending<1||this.options.MaximumPendingBytes<FrameBytes||this.options.MaximumFiles is <1 or >65536||this.options.MaximumDiskBytes<32768||this.options.MaximumActive<1){throw new ArgumentException("トレース記録の上限が不正です。");}
        this.worker=Task.Run(this.WorkAsync);
    }
    internal CancellationToken StoppingToken=>this.stopping.Token;
    internal int WaitingCount{get{lock(this.gate){return this.waiting;}}}
    internal QueryTraceRecording Prepare(QueryTraceOptions options,CancellationToken cancellation){
        options.Validate();
        lock(this.gate){
            this.CheckOpen(cancellation);
            if(this.recordings.Count>=this.options.MaximumActive){throw new ManagementException("Limit","同時トレースの上限に達しました。");}
            ResourceReservation? initial=null;
            if(options.Reliability==QueryTraceReliability.Complete){initial=this.Reserve(options.Reliability,cancellation);}
            if(this.recordings.Count>=this.options.MaximumActive){if(initial is not null){this.Release(initial);}throw new ManagementException("Limit","同時トレースの上限に達しました。");}
            var recording=new QueryTraceRecording(this,options,initial);this.recordings.Add(recording);return recording;
        }
    }
    internal bool Record(QueryTraceRecording recording,QueryTraceFrame frame,QueryTraceReliability reliability,CancellationToken cancellation){
        if(!Valid(frame)||frame.RecordMode!=recording.Options.RecordMode||frame.Reliability!=reliability||reliability!=recording.Options.Reliability){throw new ManagementException("InvalidRequest","トレースframeが不正です。");}
        lock(this.gate){
            this.CheckOpen(cancellation);if(recording.Disposed){throw new ManagementException("Stopped","トレース記録を終了しています。");}
            this.captured++;
            var reservation=recording.Initial;
            if(reservation is not null){recording.Initial=null;}
            else{reservation=this.Reserve(reliability,cancellation);}
            if(reservation is null){this.dropped++;this.error??="BufferFull";this.Pulse(frame);return false;}
            this.queue.Enqueue(new(++this.sequence,frame,reservation));this.signal.Release();return true;
        }
    }
    // pendingにはwriter処理中とCompleteの初回予約も含む。親だけが逼迫し、解放者がいない場合は待たない。
    private ResourceReservation? Reserve(QueryTraceReliability reliability,CancellationToken cancellation){
        var started=0L;var registration=cancellation.UnsafeRegister(static value=>{var owner=(QueryTraceRecorder)value!;lock(owner.gate){Monitor.PulseAll(owner.gate);}},this);
        try{
            while(true){
                this.CheckOpen(cancellation);
                if(this.pending<this.options.MaximumPending&&this.bytes<=this.options.MaximumPendingBytes-FrameBytes){
                    try{var memory=this.budget.ReserveMemory(FrameBytes);this.pending++;this.bytes+=FrameBytes;return memory;}
                    catch(ResourceLimitException){
                        if(reliability==QueryTraceReliability.BestEffort){this.error="MemoryBudget";return null;}
                        if(this.pending==0){throw new ManagementException("Limit","トレース記録の親メモリ予算がありません。");}
                    }
                }
                if(reliability==QueryTraceReliability.BestEffort){return null;}
                if(started==0){started=Stopwatch.GetTimestamp();}this.waiting++;
                try{Monitor.Wait(this.gate);}finally{this.waiting--;}
            }
        }finally{_=registration.Unregister();if(started!=0){var ticks=Stopwatch.GetElapsedTime(started).Ticks;this.backpressure+=Math.Min(ticks,long.MaxValue-this.backpressure);}}
    }
    private void CheckOpen(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();if(this.closed){throw new ManagementException("Stopped","トレース記録を停止しています。");}}
    internal void End(QueryTraceRecording recording){
        lock(this.gate){
            if(recording.Disposed){return;}recording.Disposed=true;this.recordings.Remove(recording);
            if(recording.Initial is{} initial){recording.Initial=null;this.Release(initial);}Monitor.PulseAll(this.gate);
        }
    }
    private void Release(ResourceReservation memory){this.pending--;this.bytes-=memory.Bytes;memory.Dispose();}
    internal QueryTraceStatus Status(){lock(this.gate){return new(this.session,this.captured,this.saved,this.dropped,this.failures,this.bytes,this.recordings.Count,this.backpressure,this.late,this.error,this.closed);}}
    internal void ReportLateSamples(long count){lock(this.gate){if(count>0){this.late+=Math.Min(count,long.MaxValue-this.late);}}}
    internal void RecordObservationFailure(){lock(this.gate){this.dropped++;this.error="ObservationFailed";}}
    private void Pulse(QueryTraceFrame frame){try{this.notifications.Pulse(frame.DefinitionId,frame.ObjectId);}catch(Exception){this.error??="NotificationFailed";}}
    private async Task WorkAsync(){
        while(true){
            await this.signal.WaitAsync().ConfigureAwait(false);var batch=new List<Pending>(64);
            lock(this.gate){
                if(!this.queue.TryDequeue(out var first)){if(this.closed){return;}continue;}batch.Add(first);
                while(batch.Count<64&&this.queue.TryDequeue(out var next)){batch.Add(next);_=this.signal.Wait(0);}
            }
            try{
                if(this.writeOverride is{} write){foreach(var item in batch){await write(item.Frame,CancellationToken.None).ConfigureAwait(false);}}
                else{
                    this.periods.EnsureWritable();try{lock(this.periodGate){this.Reclaim(1);}}catch(Exception exception){throw new RetentionException(exception);}
                    this.periods.WriteRows(batch.Select(item=>ExecutionHistoryPeriods.EncodePayload(item.Key,this.session,MemoryPackSerializer.Serialize(item.Frame))).ToArray());
                }
                lock(this.gate){this.saved+=batch.Count;this.error=null;}
                try{lock(this.periodGate){this.Reclaim(0);}}catch(Exception){lock(this.gate){this.error="RetentionFailed";}}
            }catch(IncompleteExecutionHistoryPeriodException){lock(this.gate){this.failures++;this.dropped+=batch.Count;this.error="IncompletePeriod";}}
            catch(RetentionException){lock(this.gate){this.failures++;this.dropped+=batch.Count;this.error="RetentionFailed";}}
            catch(Exception){lock(this.gate){this.failures++;this.dropped+=batch.Count;this.error="SaveFailed";}}
            finally{
                lock(this.gate){foreach(var item in batch){this.Release(item.Memory);this.Pulse(item.Frame);}Monitor.PulseAll(this.gate);}
            }
        }
    }
    private void Reclaim(int reserved){
        if(!Directory.Exists(this.directory)){return;}
        var directories=new DirectoryInfo(this.directory).EnumerateDirectories("period-*").Take(65538).ToArray();
        if(directories.Length>65537){throw new InvalidDataException("トレース期間の検査上限を超えています。");}
        var periods=directories.OrderBy(item=>item.Name,StringComparer.Ordinal).Select(item=>(Directory:item,Bytes:item.EnumerateFiles("*",SearchOption.AllDirectories).Sum(file=>file.Length))).ToArray();
        var bytes=periods.Sum(item=>item.Bytes);var count=periods.Length;
        foreach(var period in periods){
            if(count<=this.options.MaximumFiles-reserved&&bytes<=this.options.MaximumDiskBytes){break;}
            var prefix=Path.GetFullPath(this.directory)+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(period.Directory.FullName).StartsWith(prefix,StringComparison.Ordinal)||period.Directory.Attributes.HasFlag(FileAttributes.ReparsePoint)){throw new InvalidDataException("トレース期間の削除先が不正です。");}
            period.Directory.Delete(true);bytes-=period.Bytes;count--;
        }
    }
    internal Task<QueryTracePage> ReadAsync(QueryTraceQuery query,CancellationToken cancellation){Validate(query);return Task.Run(()=>{lock(this.periodGate){return this.Read(query,cancellation);}},cancellation);}
    private QueryTracePage Read(QueryTraceQuery query,CancellationToken cancellation){
        var frames=new List<QueryTraceFrame>();var invalid=0;var limited=false;using var memory=this.budget.ReserveMemory(0);
        try{if(Directory.Exists(this.directory)){
            var examined=0;
            foreach(var path in Directory.EnumerateDirectories(this.directory,"period-*").Take(this.options.MaximumFiles+1)){
                if(examined++==this.options.MaximumFiles){limited=true;break;}cancellation.ThrowIfCancellationRequested();var reserved=0L;
                try{
                    const long reading=8L<<20;memory.Resize(memory.Bytes+reading);reserved=reading;
                    var rows=this.periods.ReadRows(path,cancellation);
                    foreach(var row in rows){
                        var frame=MemoryPackSerializer.Deserialize<QueryTraceFrame>(row.Payload)??throw new InvalidDataException("トレースframeがありません。");
                        if(!Valid(frame)){throw new InvalidDataException("トレースframeが不正です。");}
                        if(frame.DefinitionId==query.DefinitionId&&frame.ObjectId==query.ObjectId&&(query.ExecutionId is null||frame.ExecutionId==query.ExecutionId)&&frame.Sequence>query.AfterSequence){
                            memory.Resize(memory.Bytes+FrameBytes);frames.Add(frame);
                            if(frames.Count>query.Limit+1){
                                QueryTraceFrame discarded;
                                if(query.ExecutionId is not null){discarded=frames.MaxBy(frame=>frame.Sequence)!;}
                                else{discarded=frames.MinBy(frame=>(frame.StartedAtUtc,frame.ExecutionId,frame.Sequence))!;}
                                frames.Remove(discarded);memory.Resize(memory.Bytes-FrameBytes);
                            }
                        }
                    }
                }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
                catch(ResourceLimitException){limited=true;lock(this.gate){this.error??="ReadMemoryBudget";}break;}
                catch(Exception){invalid++;}
                finally{if(reserved>0){memory.Resize(memory.Bytes-reserved);}}
            }
        }}catch(DirectoryNotFoundException){invalid++;}catch(IOException){invalid++;}catch(UnauthorizedAccessException){invalid++;}
        var ordered=frames.OrderBy(frame=>frame.StartedAtUtc).ThenBy(frame=>frame.ExecutionId).ThenBy(frame=>frame.Sequence).ToArray();
        QueryTraceFrame[] visible;
        if(query.ExecutionId is not null){visible=ordered.Take(query.Limit).ToArray();}
        else{visible=ordered.TakeLast(query.Limit).ToArray();}
        return new(visible,this.Status(),ordered.Length>query.Limit,invalid,limited);
    }
    internal static void Validate(QueryTraceQuery query){
        if(string.IsNullOrWhiteSpace(query.DefinitionId)||query.DefinitionId.Length>4096||string.IsNullOrWhiteSpace(query.ObjectId)||query.ObjectId.Length>4096||query.Limit is <1 or >1024||query.AfterSequence<0||query.AfterSequence>0&&query.ExecutionId is null){throw new ManagementException("InvalidRequest","トレース取得の対象または範囲が不正です。");}
    }
    private static bool Valid(QueryTraceFrame frame)=>frame.ExecutionId!=Guid.Empty&&frame.Sequence>0&&!string.IsNullOrWhiteSpace(frame.DefinitionId)&&frame.DefinitionId.Length<=4096&&!string.IsNullOrWhiteSpace(frame.ObjectId)&&frame.ObjectId.Length<=4096&&frame.Elapsed>=TimeSpan.Zero&&frame.ObservedOutputCount>=0&&Enum.IsDefined(frame.RecordMode)&&Enum.IsDefined(frame.Reliability)&&frame.Kind is "Started" or "GateAcquired" or "IdentityBound" or "FirstOutput" or "Sample" or "Completed"&&frame.Phase is "DatabaseGate" or "VersionBinding" or "Preparation" or "Execution"&&frame.Outcome is null or "Succeeded" or "Failed" or "Cancelled"&&(!frame.IdentityResolved||frame.Version>0&&frame.ArtifactHash is{Length:64} hash&&hash.All(Uri.IsHexDigit));
    public ValueTask DisposeAsync(){
        Task task;
        lock(this.gate){
            if(this.disposal is not null){return new(this.disposal);}this.closed=true;
            foreach(var recording in this.recordings.ToArray()){this.End(recording);}Monitor.PulseAll(this.gate);this.signal.Release();this.disposal=this.DisposeCoreAsync();task=this.disposal;
        }
        this.stopping.Cancel();return new(task);
    }
    private async Task DisposeCoreAsync(){await this.worker.ConfigureAwait(false);this.signal.Dispose();}
}

internal sealed class QueryTraceRecording(QueryTraceRecorder owner,QueryTraceOptions options,ResourceReservation? initial):IDisposable{
    internal readonly QueryTraceOptions Options=options;
    internal ResourceReservation? Initial=initial;
    internal bool Disposed;
    internal bool Record(QueryTraceFrame frame,QueryTraceReliability reliability,CancellationToken cancellation)=>owner.Record(this,frame,reliability,cancellation);
    public void Dispose()=>owner.End(this);
}
