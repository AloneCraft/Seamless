namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private ResourceHeatHistoryStore? resourceHistory;
    private readonly SemaphoreSlim resourceReads=new(2,2);
    private readonly CancellationTokenSource resourceReadStopping=new();
    private readonly object resourceDisposalGate=new();
    private Task? resourceDisposal;
    private void InitializeResourceHistory(){
        this.resourceHistory=new(Path.Combine(this.store.Root,"resource-heat-v1"),this.store.ResourceBudget);
        this.runtime.ResourceHeatObserved=this.resourceHistory.TryRecord;
    }
    public async Task<ResourceHeatReadResult> QueryResourceHeatAsync(UserAccount user,ResourceHeatQuery query,CancellationToken cancellation=default){
        query.Validate();using var reading=CancellationTokenSource.CreateLinkedTokenSource(cancellation,this.resourceReadStopping.Token);
        reading.Token.ThrowIfCancellationRequested();this.AuthorizeQueryTrace(user);await this.resourceReads.WaitAsync(reading.Token).ConfigureAwait(false);
        try{
            var result=await this.resourceHistory!.ReadAsync(query,reading.Token).ConfigureAwait(false);
            reading.Token.ThrowIfCancellationRequested();this.AuthorizeQueryTrace(user);return result;
        }finally{this.resourceReads.Release();}
    }
    private ValueTask DisposeResourceHistoryAsync(){lock(this.resourceDisposalGate){this.resourceDisposal??=this.DisposeResourceHistoryCoreAsync();return new(this.resourceDisposal);}}
    private async Task DisposeResourceHistoryCoreAsync(){
        this.resourceReadStopping.Cancel();
        for(var index=0;index<2;index++){await this.resourceReads.WaitAsync().ConfigureAwait(false);}
        if(this.resourceHistory is not null){await this.resourceHistory.DisposeAsync().ConfigureAwait(false);}
    }
}
internal sealed partial class NativeRuntime{
    internal Func<ResourceHeatTrace,ResourceHeatPersistence>? ResourceHeatObserved{get;set;}
    private ResourceHeatTrace PublishResourceHeat(ResourceHeatTrace trace){
        trace=trace with{Diagnostics=trace.Diagnostics.Where(message=>!message.StartsWith("ResponseOnly:",StringComparison.Ordinal)).ToArray()};
        ResourceHeatPersistence persistence;
        try{persistence=this.ResourceHeatObserved?.Invoke(trace)??new("Unavailable","RecorderUnavailable");}
        catch(Exception exception)when(exception is not OutOfMemoryException){persistence=new("Failed",exception.GetType().Name);}
        return trace with{Persistence=persistence};
    }
}
