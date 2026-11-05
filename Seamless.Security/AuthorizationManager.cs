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
    internal TimeProvider Time{get;}
    internal AuthorizationLimits Limits{get;}
    public AuthorizationPolicySnapshot Policy=>Volatile.Read(ref this.policy);
    internal void EnsureActive()=>ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed),this);
    public AuthorizationManager(AuthorizationPolicySnapshot policy,TimeProvider? time=null,AuthorizationLimits? limits=null){this.policy=policy??throw new ArgumentNullException(nameof(policy));this.Time=time??TimeProvider.System;this.Limits=limits??new();this.Limits.Validate();}
    public AuthorizationSession CreateSession(AuthenticatedIdentity identity){
        ArgumentNullException.ThrowIfNull(identity);lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);return new(this,identity,this.State(identity.Subject));}
    }
    private SubjectState State(SecuritySubjectId subject){
        subject.Validate();if(this.subjects.TryGetValue(subject,out var state)){return state;}
        if(this.subjects.Count>=this.Limits.MaximumSubjects){throw new AuthorizationException("Capacity","The authentication subject limit has been reached.");}
        state=new();this.subjects[subject]=state;return state;
    }
    public void PublishPolicy(AuthorizationPolicySnapshot policy){
        ArgumentNullException.ThrowIfNull(policy);lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);if(policy.ServerId!=this.policy.ServerId||policy.Version<=this.policy.Version){throw new AuthorizationException("Conflict","The authorization policy version did not advance for this server.");}Volatile.Write(ref this.policy,policy);}
    }
    public void SetDisabled(SecuritySubjectId subject,bool disabled){
        SubjectState state;lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);state=this.State(subject);}
        state.SetDisabled(disabled);
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
        internal void SetDisabled(bool disabled){
            CancellationTokenSource? previous=null;
            lock(this.Gate){
                if(this.Disabled==disabled){return;}this.Disabled=disabled;
                if(disabled){previous=this.Cancellation;}
                else{this.Cancellation=new();}
            }
            if(previous is not null){try{previous.Cancel();}finally{previous.Dispose();}}
        }
    }
}

public sealed class AuthorizationSession{
    private readonly object gate=new();
    private readonly AuthorizationManager manager;
    private readonly AuthorizationManager.SubjectState state;
    private AuthenticatedIdentity identity;
    private AuthorizationPolicySnapshot? cachedPolicy;
    private AuthorizationSnapshot? snapshot;
    private bool membershipUnavailable;
    internal AuthorizationSession(AuthorizationManager manager,AuthenticatedIdentity identity,AuthorizationManager.SubjectState state){this.manager=manager;this.identity=identity;this.state=state;}
    public SecuritySubjectId Subject=>this.identity.Subject;
    public void UpdateValidatedIdentity(AuthenticatedIdentity identity){
        ArgumentNullException.ThrowIfNull(identity);lock(this.gate){if(identity.Subject!=this.identity.Subject){throw new AuthorizationException("IdentityMismatch","Reauthentication must identify the same subject.");}this.identity=identity;this.cachedPolicy=null;this.snapshot=null;this.membershipUnavailable=false;}
    }
    public void MarkMembershipUnavailable(){lock(this.gate){this.membershipUnavailable=true;}}
    public AuthorizationLease BeginOperation(CancellationToken cancellationToken=default)=>this.Begin(false,cancellationToken);
    public AuthorizationLease BeginTransaction(CancellationToken cancellationToken=default)=>this.Begin(true,cancellationToken);
    private AuthorizationLease Begin(bool transaction,CancellationToken cancellationToken){
        lock(this.gate){lock(this.state.Gate){
            this.manager.EnsureActive();cancellationToken.ThrowIfCancellationRequested();if(this.state.Disabled){throw new AuthorizationException("AccountDisabled","The account is disabled.");}
            var now=this.manager.Time.GetUtcNow();if(now>=this.identity.ExpiresAt){throw new AuthorizationException("AuthenticationExpired","Reauthentication is required before a new operation.");}
            if(this.membershipUnavailable||this.identity.MembershipVerifiedAt>now||now-this.identity.MembershipVerifiedAt>=this.manager.Limits.MembershipFreshness){throw new AuthorizationException("MembershipRefreshRequired","Current group membership must be verified before a new operation.");}
            var policy=this.manager.Policy;
            if(!ReferenceEquals(policy,this.cachedPolicy)){this.snapshot=policy.Resolve(this.identity);this.cachedPolicy=policy;}
            TimeSpan? duration=null;if(transaction){duration=this.manager.Limits.MaximumTransactionDuration;}
            return new(this.manager,this.snapshot!,now,duration,this.state.Cancellation.Token,cancellationToken);
        }}
    }
}
