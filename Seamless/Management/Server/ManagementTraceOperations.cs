namespace Seamless.Management.Server;

public sealed partial class ConcurrentManagementOperations{
    public Task<ResourceHeatReadResult> QueryResourceHeatAsync(ResourceHeatQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.QueryResourceHeatAsync(this.owner,query,cancellationToken),cancellationToken).Unwrap();
    public Task<QueryTracePage> QueryTraceAsync(QueryTraceQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.QueryTraceAsync(this.owner,query,cancellationToken),cancellationToken).Unwrap();
    public Task<QueryTraceWatchResult> WatchQueryTraceAsync(QueryTraceWatchQuery query,CancellationToken cancellationToken=default)=>
        this.WatchAuthenticatedTraceAsync(()=>this.owner,query,cancellationToken);
    internal async Task<QueryTraceWatchResult> WatchAuthenticatedTraceAsync(Func<UserAccount> authenticate,QueryTraceWatchQuery query,CancellationToken cancellationToken){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        using var watch=await this.InvokeAsync(()=>this.service.BeginQueryTraceWatch(authenticate(),query),cancellation.Token).ConfigureAwait(false);
        // 通知待ちには通常のDB実行枠を保持しない。
        var result=await watch.WaitAsync(cancellation.Token).ConfigureAwait(false);
        return await this.InvokeAsync(()=>{this.service.AuthorizeQueryTrace(authenticate());return result;},cancellation.Token).ConfigureAwait(false);
    }
}
public sealed partial class InProcessManagementHost{
    public Task<ResourceHeatReadResult> QueryResourceHeatAsync(ResourceHeatQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.QueryResourceHeatAsync(user,query,cancellationToken),cancellationToken).AsTask().Unwrap();
    public Task<QueryTracePage> QueryTraceAsync(QueryTraceQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.QueryTraceAsync(user,query,cancellationToken),cancellationToken).AsTask().Unwrap();
    public async Task<QueryTraceWatchResult> WatchQueryTraceAsync(QueryTraceWatchQuery query,CancellationToken cancellationToken=default){
        using var watch=await this.InvokeAsync((service,user)=>service.BeginQueryTraceWatch(user,query),cancellationToken).ConfigureAwait(false);
        var result=await watch.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await this.InvokeAsync((service,user)=>{service.AuthorizeQueryTrace(user);return result;},cancellationToken).ConfigureAwait(false);
    }
}
