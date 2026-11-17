using System.Collections.Concurrent;

namespace Seamless.Security;

public sealed record AuthorizationLimits{
    public TimeSpan MembershipFreshness{get;init;}=TimeSpan.FromMinutes(5);
    public TimeSpan MaximumTransactionDuration{get;init;}=TimeSpan.FromMinutes(30);
    public int MaximumSubjects{get;init;}=100000;
    internal void Validate(){if(this.MembershipFreshness<=TimeSpan.Zero||this.MaximumTransactionDuration<=TimeSpan.Zero||this.MaximumTransactionDuration.TotalMilliseconds>uint.MaxValue-1||this.MaximumSubjects<=0){throw new ArgumentOutOfRangeException(nameof(AuthorizationLimits));}}
}

/// <summary>全接続経路で共有するポリシー版と緊急停止。本人確認は別のアダプターが行う。</summary>
public sealed partial class AuthorizationManager:IDisposable{
    private readonly object gate=new();
    private readonly ConcurrentDictionary<SecuritySubjectId,SubjectState> subjects=new();
    private AuthorizationPolicySnapshot policy;
    private bool disposed;
    private string? failSecureReason;
    internal TimeProvider Time{get;}
    internal AuthorizationLimits Limits{get;}
    public AuthorizationPolicySnapshot Policy=>Volatile.Read(ref this.policy);
    public bool IsFailSecure=>Volatile.Read(ref this.failSecureReason) is not null;
    public string? FailSecureReason=>Volatile.Read(ref this.failSecureReason);

    public void TriggerFailSecure(string reason){
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Volatile.Write(ref this.failSecureReason,reason);
        foreach(var state in this.subjects.Values){try{state.SetDisabled(true);}catch{}}
    }

    internal void EnsureActive(){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed),this);
        var reason=Volatile.Read(ref this.failSecureReason);
        if(reason is not null){throw new AuthorizationException("FailSecureLocked",$"Security store integrity check failed: {reason}");}
    }
    public AuthorizationManager(AuthorizationPolicySnapshot policy,TimeProvider? time=null,AuthorizationLimits? limits=null){this.policy=policy??throw new ArgumentNullException(nameof(policy));this.Time=time??TimeProvider.System;this.Limits=limits??new();this.Limits.Validate();}
    public AuthorizationSession CreateSession(AuthenticatedIdentity identity)=>this.CreateSession(identity,null);
    internal AuthorizationSession CreateSession(AuthenticatedIdentity identity,Func<SecuritySubjectId,bool>? isPersistentlyDisabled){
        ArgumentNullException.ThrowIfNull(identity);this.EnsureActive();lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);this.EnsureActive();return new(this,identity,this.State(identity.Subject,isPersistentlyDisabled));}
    }
    private SubjectState State(SecuritySubjectId subject,Func<SecuritySubjectId,bool>? isPersistentlyDisabled=null){
        subject.Validate();if(this.subjects.TryGetValue(subject,out var state)){return state;}
        if(this.subjects.Count>=this.Limits.MaximumSubjects){throw new AuthorizationException("Capacity","The authentication subject limit has been reached.");}
        state=new();if(isPersistentlyDisabled?.Invoke(subject)==true){state.SetDisabled(true);}
        this.subjects[subject]=state;return state;
    }
    public void PublishPolicy(AuthorizationPolicySnapshot policy){
        ArgumentNullException.ThrowIfNull(policy);lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);if(policy.ServerId!=this.policy.ServerId||policy.Version<=this.policy.Version){throw new AuthorizationException("Conflict","The authorization policy version did not advance for this server.");}Volatile.Write(ref this.policy,policy);}
    }
    public void SetDisabled(SecuritySubjectId subject,bool disabled){
        SubjectState state;lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);state=this.State(subject);}
        state.SetDisabled(disabled);
    }
    internal Task SetKnownSubjectDisabledAsync(SecuritySubjectId subject,bool disabled){
        // 保存ホストは主体の作成・破棄と通知を直列化する。保存を待つポリシー編集とは別のロックを使う。
        this.EnsureActive();
        return this.subjects.TryGetValue(subject,out var state)?state.SetDisabledAsync(disabled):Task.CompletedTask;
    }
    public void Dispose(){
        SubjectState[] states;lock(this.gate){if(this.disposed){return;}this.disposed=true;states=this.subjects.Values.ToArray();}
        List<Exception>? failures=null;foreach(var state in states){try{state.SetDisabled(true);}catch(AggregateException error){failures??=[];failures.AddRange(error.InnerExceptions);}}
        if(failures is not null){throw new AggregateException("Some cancellation callbacks failed after all subjects were disabled.",failures);}
    }
    internal sealed class SubjectState{
        internal readonly object Gate=new();
        internal bool Disabled;
        internal CancellationTokenSource Cancellation=new();
        private CancellationTokenSource? ChangeDisabled(bool disabled){
            CancellationTokenSource? previous=null;
            lock(this.Gate){
                if(this.Disabled==disabled){return null;}this.Disabled=disabled;
                if(disabled){previous=this.Cancellation;}
                else{this.Cancellation=new();}
            }
            return previous;
        }
        internal void SetDisabled(bool disabled){using var previous=this.ChangeDisabled(disabled);previous?.Cancel();}
        internal async Task SetDisabledAsync(bool disabled){
            using var previous=this.ChangeDisabled(disabled);
            if(previous is not null){await previous.CancelAsync().ConfigureAwait(false);}
        }
    }
}

public sealed class AuthorizationSession:IAuthorizationSession{
    private readonly object gate=new();
    private readonly AuthorizationManager manager;
    private readonly AuthorizationManager.SubjectState state;
    private AuthenticatedIdentity identity;
    private AuthorizationPolicySnapshot? cachedPolicy;
    private AuthorizationSnapshot? snapshot;
    private bool membershipUnavailable;
    private bool disposed;
    internal AuthorizationSession(AuthorizationManager manager,AuthenticatedIdentity identity,AuthorizationManager.SubjectState state){this.manager=manager;this.identity=identity;this.state=state;}
    public SecuritySubjectId Subject=>this.identity.Subject;
    public void UpdateValidatedIdentity(AuthenticatedIdentity identity){
        ArgumentNullException.ThrowIfNull(identity);
        lock(this.gate){lock(this.state.Gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.manager.EnsureActive();
            if(this.state.Disabled){throw new AuthorizationException("AccountDisabled","The account is disabled.");}
            if(identity.Subject!=this.identity.Subject){throw new AuthorizationException("IdentityMismatch","Reauthentication must identify the same subject.");}
            this.identity=identity;this.cachedPolicy=null;this.snapshot=null;this.membershipUnavailable=false;
        }}
    }
    public void MarkMembershipUnavailable(){lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);this.membershipUnavailable=true;}}
    public AuthorizationLease BeginOperation(CancellationToken cancellationToken=default)=>this.Begin(false,cancellationToken);
    public AuthorizationLease BeginTransaction(CancellationToken cancellationToken=default)=>this.Begin(true,cancellationToken);
    private AuthorizationLease Begin(bool transaction,CancellationToken cancellationToken){
        lock(this.gate){lock(this.state.Gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.manager.EnsureActive();cancellationToken.ThrowIfCancellationRequested();if(this.state.Disabled){throw new AuthorizationException("AccountDisabled","The account is disabled.");}
            var now=this.manager.Time.GetUtcNow();if(now>=this.identity.ExpiresAt){throw new AuthorizationException("AuthenticationExpired","Reauthentication is required before a new operation.");}
            if(this.membershipUnavailable||this.identity.MembershipVerifiedAt>now||now-this.identity.MembershipVerifiedAt>=this.manager.Limits.MembershipFreshness){throw new AuthorizationException("MembershipRefreshRequired","Current group membership must be verified before a new operation.");}
            var policy=this.manager.Policy;
            if(!ReferenceEquals(policy,this.cachedPolicy)){this.snapshot=policy.Resolve(this.identity);this.cachedPolicy=policy;}
            TimeSpan? duration=null;if(transaction){duration=this.manager.Limits.MaximumTransactionDuration;}
            return new(this.manager,this.snapshot!,now,duration,this.state.Cancellation.Token,cancellationToken);
        }}
    }
    // 開始済みleaseと他の接続は独立して存続する。共有managerや主体の取消元を解放しない。
    public void Dispose(){lock(this.gate){this.disposed=true;}}
}
