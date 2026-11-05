namespace Seamless.Management.Server;

internal sealed class ReadAuditBuffer:IAsyncDisposable{
    private sealed record Pending(Guid Id,AuditReadDelta[] Rows,long Bytes,long Count);
    private readonly record struct Aggregate(long Count,long Sequence);
    private readonly object gate=new();
    private readonly ReadAuditOptions options;
    private readonly Func<Guid,AuditReadDelta[],CancellationToken,ValueTask> save;
    private readonly TimeProvider clock;
    private readonly Dictionary<AuditReadKey,Aggregate> rows=new();
    private readonly SemaphoreSlim flushGate=new(1,1);
    private readonly CancellationTokenSource stop=new();
    private readonly Task worker;
    private readonly Guid session=Guid.NewGuid();
    private readonly DateTimeOffset started;
    private Pending? pending;
    private Task? disposal;
    private long bytes,accepted,saved,dropped,failures,unconfirmed,sequence;
    private bool closed,saturated;
    private DateTimeOffset? lastSaved,lastFailure;
    private string? error;
    internal ReadAuditBuffer(ReadAuditOptions options,Func<Guid,AuditReadDelta[],CancellationToken,ValueTask> save,bool startWorker=true,TimeProvider? clock=null){options.Validate();this.options=options;this.save=save;this.clock=clock??TimeProvider.System;this.started=this.clock.GetUtcNow();if(startWorker){this.worker=Task.Run(this.WorkAsync);}else{this.worker=Task.CompletedTask;}}
    private void Add(ref long target,long count){if(target>long.MaxValue-count){target=long.MaxValue;this.saturated=true;}else{target+=count;}}
    private static long Size(AuditReadKey key)=>256L+2L*(key.SubjectId.Length+key.Database.Length+key.QueryId.Length+(key.CodeVersion?.Length??0));
    internal bool Record(AuditReadKey key){lock(this.gate){if(this.closed){return false;}if(this.rows.TryGetValue(key,out var item)){if(item.Count==long.MaxValue){this.Add(ref this.dropped,1);return false;}this.rows[key]=item with{Count=item.Count+1};this.Add(ref this.accepted,1);return true;}var size=Size(key);if(this.rows.Count+(this.pending?.Rows.Length??0)>=this.options.MaximumKeys||size>this.options.MaximumBytes-this.bytes){this.Add(ref this.dropped,1);return false;}if(this.sequence==long.MaxValue){this.saturated=true;this.Add(ref this.dropped,1);return false;}this.rows.Add(key,new(1,++this.sequence));this.bytes+=size;this.Add(ref this.accepted,1);return true;}}
    internal AuditReadBufferStatus Status(){lock(this.gate){return new(this.session,this.started,true,this.options.Window.TotalSeconds,this.options.Interval.TotalSeconds,this.rows.Count+(this.pending?.Rows.Length??0),this.bytes,this.accepted,this.saved,this.dropped,this.failures,this.lastSaved,this.lastFailure,this.error,this.closed,this.saturated,this.unconfirmed);}}
    private Pending? Seal(long cutoff){if(this.pending is not null){return this.pending;}if(this.rows.Count==0){return null;}var values=new List<AuditReadDelta>(Math.Min(this.rows.Count,this.options.MaximumBatch));long size=0,count=0;foreach(var pair in this.rows){if(values.Count==this.options.MaximumBatch){break;}if(pair.Value.Sequence>cutoff){continue;}values.Add(new(pair.Key,pair.Value.Count));size+=Size(pair.Key);count=checked(count+pair.Value.Count);}if(values.Count==0){return null;}foreach(var value in values){this.rows.Remove(value.Key);}this.pending=new(Guid.NewGuid(),values.ToArray(),size,count);return this.pending;}
    internal async Task<bool> FlushAsync(CancellationToken cancellation=default){
        await this.flushGate.WaitAsync(cancellation).ConfigureAwait(false);try{var remaining=this.options.MaximumKeys;long cutoff;lock(this.gate){cutoff=this.sequence;}while(remaining>0){Pending? item;lock(this.gate){item=this.Seal(cutoff);}if(item is null){return true;}try{await this.save(item.Id,item.Rows,cancellation).ConfigureAwait(false);}catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}catch(Exception failure){lock(this.gate){this.Add(ref this.failures,1);this.lastFailure=this.clock.GetUtcNow();this.error="SaveFailed";if(failure is ManagementException problem&&problem.Code is "Limit" or "Capacity" or "LegacyReplayUnverifiable"){this.error=problem.Code;}}return false;}lock(this.gate){this.pending=null;this.bytes-=item.Bytes;this.Add(ref this.saved,item.Count);this.lastSaved=this.clock.GetUtcNow();this.error=null;}remaining-=item.Rows.Length;}return true;}finally{this.flushGate.Release();}
    }
    private async Task WorkAsync(){using var timer=new PeriodicTimer(this.options.Interval,this.clock);try{while(await timer.WaitForNextTickAsync(this.stop.Token).ConfigureAwait(false)){await this.FlushAsync(this.stop.Token).ConfigureAwait(false);}}catch(OperationCanceledException)when(this.stop.IsCancellationRequested){}}
    public ValueTask DisposeAsync(){lock(this.gate){this.closed=true;this.disposal??=Task.Run(this.StopAsync);return new(this.disposal);}}
    private async Task StopAsync(){this.stop.Cancel();await this.worker.ConfigureAwait(false);await this.FlushAsync().ConfigureAwait(false);lock(this.gate){this.Add(ref this.unconfirmed,this.pending?.Count??0);foreach(var row in this.rows){this.Add(ref this.dropped,row.Value.Count);}this.pending=null;this.rows.Clear();this.bytes=0;}this.stop.Dispose();}
}
