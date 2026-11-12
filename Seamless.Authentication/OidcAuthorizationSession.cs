using Seamless.Security;

namespace Seamless.Authentication;

/// <summary>検証済みの本人確認と操作ごとの認可。期限内の操作開始には認証元への通信を行わない。</summary>
public sealed class OidcAuthorizationSession:IDisposable{
    private readonly OidcTokenVerifier verifier;
    private readonly IAuthorizationSession session;
    private readonly SecuritySubjectId subject;
    private readonly SemaphoreSlim refresh=new(1,1);
    private string? accessToken;
    private int disposed;
    public SecuritySubjectId Subject=>this.subject;
    private OidcAuthorizationSession(OidcTokenVerifier verifier,IAuthorizationSession session,SecuritySubjectId subject,string accessToken){this.verifier=verifier;this.session=session;this.subject=subject;this.accessToken=accessToken;}
    public static async Task<OidcAuthorizationSession> AuthenticateAsync(OidcTokenVerifier verifier,AuthorizationManager manager,string accessToken,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(verifier);ArgumentNullException.ThrowIfNull(manager);
        return await AuthenticateWithSessionFactoryAsync(verifier,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(manager.CreateSession(identity)),accessToken,cancellationToken).ConfigureAwait(false);
    }
    /// <summary>factoryは呼出し専用のsessionの所有権を返す。返却前の失敗はfactory、返却後の解放は本体が担当する。</summary>
    public static async Task<OidcAuthorizationSession> AuthenticateWithSessionFactoryAsync(OidcTokenVerifier verifier,Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> sessionFactory,string accessToken,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(verifier);ArgumentNullException.ThrowIfNull(sessionFactory);
        var identity=await verifier.AuthenticateAsync(accessToken,cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();IAuthorizationSession? session=null;
        try{
            session=await sessionFactory(identity,cancellationToken).ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();
            if(session is null){throw new InvalidOperationException("The authorization session factory returned no session.");}
            if(session.Subject!=identity.Subject){throw new AuthorizationException("IdentityMismatch","The authorization session must identify the authenticated subject.");}
            using(ValidateLease(session.BeginOperation(cancellationToken),identity.Subject,false,cancellationToken)){}
            cancellationToken.ThrowIfCancellationRequested();var result=new OidcAuthorizationSession(verifier,session,identity.Subject,accessToken);session=null;return result;
        }finally{session?.Dispose();}
    }
    public ValueTask<AuthorizationLease> BeginOperationAsync(CancellationToken cancellationToken=default)=>this.Begin(false,cancellationToken);
    public ValueTask<AuthorizationLease> BeginTransactionAsync(CancellationToken cancellationToken=default)=>this.Begin(true,cancellationToken);
    private ValueTask<AuthorizationLease> Begin(bool transaction,CancellationToken cancellationToken){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
        try{return new(this.BeginCore(transaction,cancellationToken));}
        catch(AuthorizationException error)when(error.Code=="MembershipRefreshRequired"){return this.RefreshAndBeginAsync(transaction,cancellationToken);}
    }
    private AuthorizationLease BeginCore(bool transaction,CancellationToken cancellationToken){
        AuthorizationLease lease;if(transaction){lease=this.session.BeginTransaction(cancellationToken);}else{lease=this.session.BeginOperation(cancellationToken);}
        lease=ValidateLease(lease,this.Subject,transaction,cancellationToken);
        try{ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);return lease;}catch{lease.Dispose();throw;}
    }
    private static AuthorizationLease ValidateLease(AuthorizationLease lease,SecuritySubjectId subject,bool transaction,CancellationToken cancellationToken){
        ArgumentNullException.ThrowIfNull(lease);
        try{
            cancellationToken.ThrowIfCancellationRequested();
            if(lease.Audit.Subject!=subject){throw new AuthorizationException("IdentityMismatch","The authorization lease must identify the authenticated subject.");}
            if(transaction&&lease.Audit.Deadline is null){throw new AuthorizationException("InvalidTransactionLease","An explicit transaction requires a bounded authorization lease.");}
            lease.ThrowIfCancellationRequested();return lease;
        }catch{lease.Dispose();throw;}
    }
    private void UpdateValidatedIdentity(AuthenticatedIdentity identity){
        if(identity.Subject!=this.Subject){throw new AuthorizationException("IdentityMismatch","Reauthentication must identify the same subject.");}
        this.session.UpdateValidatedIdentity(identity);
    }
    private async ValueTask<AuthorizationLease> RefreshAndBeginAsync(bool transaction,CancellationToken cancellationToken){
        await this.refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
            // 待機中に別の要求が再確認していた場合、二重に認証元へ問い合わせない。
            try{return this.BeginCore(transaction,cancellationToken);}
            catch(AuthorizationException error)when(error.Code=="MembershipRefreshRequired"){}
            try{
                var identity=await this.verifier.AuthenticateAsync(this.accessToken!,cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);this.UpdateValidatedIdentity(identity);
            }catch(OidcAuthenticationException error)when(error.Code!=OidcAuthenticationException.BusyCode){this.session.MarkMembershipUnavailable();throw;}
            return this.BeginCore(transaction,cancellationToken);
        }finally{this.refresh.Release();}
    }
    public async Task ReauthenticateAsync(string accessToken,CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);await this.refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
            var identity=await this.verifier.AuthenticateAsync(accessToken,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);this.UpdateValidatedIdentity(identity);
            Interlocked.Exchange(ref this.accessToken,accessToken);
            if(Volatile.Read(ref this.disposed)!=0){Interlocked.Exchange(ref this.accessToken,null);throw new ObjectDisposedException(nameof(OidcAuthorizationSession));}
        }finally{this.refresh.Release();}
    }
    public void Dispose(){if(Interlocked.Exchange(ref this.disposed,1)==0){Interlocked.Exchange(ref this.accessToken,null);this.session.Dispose();}}
}
