namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private QueryExecutionHistoryStore? executionHistory;
    private void InitializeExecutionHistory(){
        this.InitializeQueryTrace();
        this.InitializeResourceHistory();
        this.executionHistory=new(Path.Combine(this.store.Root,"execution-history-tsavorite-v1"),this.store.ResourceBudget);
        this.runtime.ExecutionObserved=observation=>{_=this.executionHistory.TryRecord(observation);};
    }
    public async Task<QueryExecutionHistoryPage> QueryExecutionHistoryAsync(UserAccount user,QueryExecutionHistoryQuery query,CancellationToken cancellation=default){
        QueryExecutionHistoryStore.Validate(query);lock(this.store.Gate){Require(this.CurrentActor(user),"*",ManagementPermission.Manage);}
        var page=await this.executionHistory!.ReadAsync(query,cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        lock(this.store.Gate){Require(this.CurrentActor(user),"*",ManagementPermission.Manage);}
        var failures=this.runtime.ExecutionObservationFailureCount;
        if(failures==0){return page;}
        var dropped=page.Status.Dropped;if(failures>long.MaxValue-dropped){dropped=long.MaxValue;}else{dropped+=failures;}
        return page with{Status=page.Status with{ObservationFailures=failures,Dropped=dropped,ErrorCode=page.Status.ErrorCode??"ObservationFailed"}};
    }
    internal async ValueTask DisposeExecutionHistoryAsync(){
        await this.DisposeQueryTraceAsync().ConfigureAwait(false);
        await this.DisposeResourceHistoryAsync().ConfigureAwait(false);
        if(this.executionHistory is not null){await this.executionHistory.DisposeAsync().ConfigureAwait(false);}
    }
}
internal sealed partial class NativeRuntime{
    private long executionObservationFailures;
    internal long ExecutionObservationFailureCount=>Volatile.Read(ref this.executionObservationFailures);
    internal Action<QueryExecutionObservation>? ExecutionObserved{get;set;}
}
