namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private QueryTraceNotifications? traceNotifications;
    private QueryTraceRecorder? traceRecorder;
    private QueryTraceSampler? traceSampler;
    private readonly CancellationTokenSource traceReadStopping=new();
    private readonly SemaphoreSlim traceReads=new(4,4);
    private readonly object traceDisposalGate=new();
    private Task? traceDisposal;
    private void InitializeQueryTrace(){
        this.traceNotifications=new(this.store.ResourceBudget);
        this.traceRecorder=new(Path.Combine(this.store.Root,"query-trace-tsavorite-v1"),this.store.ResourceBudget,this.traceNotifications);
        this.traceSampler=new(this.store.ResourceBudget,this.traceRecorder.RecordObservationFailure);
        this.runtime.TraceRecorder=this.traceRecorder;this.runtime.TraceSampler=this.traceSampler;
    }
    public async Task<QueryTracePage> QueryTraceAsync(UserAccount user,QueryTraceQuery query,CancellationToken cancellation=default){
        using var reading=CancellationTokenSource.CreateLinkedTokenSource(cancellation,this.traceReadStopping.Token);
        reading.Token.ThrowIfCancellationRequested();this.AuthorizeQueryTrace(user);
        await this.traceReads.WaitAsync(reading.Token).ConfigureAwait(false);
        try{
            var result=await this.traceRecorder!.ReadAsync(query,reading.Token).ConfigureAwait(false);
            reading.Token.ThrowIfCancellationRequested();this.AuthorizeQueryTrace(user);return result;
        }finally{this.traceReads.Release();}
    }
    internal void AuthorizeQueryTrace(UserAccount user){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.store.Gate){Require(this.CurrentActor(user),"*",ManagementPermission.Manage);}
    }
    internal QueryTraceWatchTicket BeginQueryTraceWatch(UserAccount user,QueryTraceWatchQuery query){
        this.AuthorizeQueryTrace(user);return this.traceNotifications!.Watch(query);
    }
    public async Task<QueryTraceWatchResult> WatchQueryTraceAsync(UserAccount user,QueryTraceWatchQuery query,CancellationToken cancellation=default){
        using var watch=this.BeginQueryTraceWatch(user,query);
        var result=await watch.WaitAsync(cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();this.AuthorizeQueryTrace(user);return result;
    }
    private ValueTask DisposeQueryTraceAsync(){
        lock(this.traceDisposalGate){this.traceDisposal??=this.DisposeQueryTraceCoreAsync();return new(this.traceDisposal);}
    }
    private async Task DisposeQueryTraceCoreAsync(){
        this.traceReadStopping.Cancel();
        this.traceNotifications?.Close();
        for(var index=0;index<4;index++){await this.traceReads.WaitAsync().ConfigureAwait(false);}
        if(this.traceSampler is not null){await this.traceSampler.DisposeAsync().ConfigureAwait(false);}
        if(this.traceRecorder is not null){await this.traceRecorder.DisposeAsync().ConfigureAwait(false);}
    }
}
internal sealed partial class NativeRuntime{
    internal QueryTraceRecorder? TraceRecorder{get;set;}
    internal QueryTraceSampler? TraceSampler{get;set;}
}
