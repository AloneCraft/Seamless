namespace Seamless.Security;

public sealed record AuthorizationAudit(Guid OperationId,SecuritySubjectId Subject,long PolicyVersion,DateTimeOffset MembershipVerifiedAt,DateTimeOffset StartedAt,DateTimeOffset? Deadline);

/// <summary>開始時の権限。途中の通常失効とサインイン期限切れでは変更しない。</summary>
public sealed class AuthorizationLease:IDisposable{
    private readonly AuthorizationManager manager;
    private readonly CancellationTokenSource cancellation;
    private readonly CancellationTokenSource? deadlineCancellation;
    private bool disposed;
    public AuthorizationSnapshot Snapshot{get;}
    public AuthorizationAudit Audit{get;}
    public CancellationToken CancellationToken{get;}
    internal AuthorizationLease(AuthorizationManager manager,AuthorizationSnapshot snapshot,DateTimeOffset startedAt,TimeSpan? duration,CancellationToken disabled,CancellationToken requested){
        this.manager=manager;this.Snapshot=snapshot;DateTimeOffset? deadline=null;
        if(duration is{} timeout){deadline=startedAt+timeout;this.deadlineCancellation=new(timeout,manager.Time);}
        this.cancellation=CancellationTokenSource.CreateLinkedTokenSource(disabled,requested,this.deadlineCancellation?.Token??CancellationToken.None);this.CancellationToken=this.cancellation.Token;
        this.Audit=new(Guid.NewGuid(),snapshot.Subject,snapshot.PolicyVersion,snapshot.MembershipVerifiedAt,startedAt,deadline);
    }
    public bool BelongsTo(AuthorizationManager manager)=>ReferenceEquals(this.manager,manager);
    public void ThrowIfCancellationRequested(){ObjectDisposedException.ThrowIf(this.disposed,this);if(this.Audit.Deadline is{} deadline&&this.manager.Time.GetUtcNow()>=deadline){this.cancellation.Cancel();}this.CancellationToken.ThrowIfCancellationRequested();}
    public void Demand(Guid resource,SecurityPermission permission){this.ThrowIfCancellationRequested();this.Snapshot.Demand(resource,permission);}
    public void Dispose(){if(this.disposed){return;}this.disposed=true;this.cancellation.Dispose();this.deadlineCancellation?.Dispose();}
}
