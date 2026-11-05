using Seamless.Security;

namespace Seamless.Authentication;

/// <summary>検証済みの本人確認と操作ごとの認可。期限内の操作開始には認証元への通信を行わない。</summary>
public sealed class OidcAuthorizationSession:IDisposable{
    private readonly OidcTokenVerifier verifier;
    private readonly AuthorizationSession session;
    private readonly SemaphoreSlim refresh=new(1,1);
    private string? accessToken;
    private int disposed;
    public SecuritySubjectId Subject=>this.session.Subject;
    private OidcAuthorizationSession(OidcTokenVerifier verifier,AuthorizationSession session,string accessToken){this.verifier=verifier;this.session=session;this.accessToken=accessToken;}
    public static async Task<OidcAuthorizationSession> AuthenticateAsync(OidcTokenVerifier verifier,AuthorizationManager manager,string accessToken,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(verifier);ArgumentNullException.ThrowIfNull(manager);
        var identity=await verifier.AuthenticateAsync(accessToken,cancellationToken).ConfigureAwait(false);
        var session=manager.CreateSession(identity);using var admission=session.BeginOperation(cancellationToken);return new(verifier,session,accessToken);
    }
    public ValueTask<AuthorizationLease> BeginOperationAsync(CancellationToken cancellationToken=default)=>this.Begin(false,cancellationToken);
    public ValueTask<AuthorizationLease> BeginTransactionAsync(CancellationToken cancellationToken=default)=>this.Begin(true,cancellationToken);
    private ValueTask<AuthorizationLease> Begin(bool transaction,CancellationToken cancellationToken){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
        try{return new(this.BeginCore(transaction,cancellationToken));}
        catch(AuthorizationException error)when(error.Code=="MembershipRefreshRequired"){return this.RefreshAndBeginAsync(transaction,cancellationToken);}
    }
    private AuthorizationLease BeginCore(bool transaction,CancellationToken cancellationToken){if(transaction){return this.session.BeginTransaction(cancellationToken);}return this.session.BeginOperation(cancellationToken);}
    private async ValueTask<AuthorizationLease> RefreshAndBeginAsync(bool transaction,CancellationToken cancellationToken){
        await this.refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
            // 待機中に別の要求が再確認していた場合、二重に認証元へ問い合わせない。
            try{return this.BeginCore(transaction,cancellationToken);}
            catch(AuthorizationException error)when(error.Code=="MembershipRefreshRequired"){}
            try{
                var identity=await this.verifier.AuthenticateAsync(this.accessToken!,cancellationToken).ConfigureAwait(false);
                ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);this.session.UpdateValidatedIdentity(identity);
            }catch(OidcAuthenticationException){this.session.MarkMembershipUnavailable();throw;}
            return this.BeginCore(transaction,cancellationToken);
        }finally{this.refresh.Release();}
    }
    public async Task ReauthenticateAsync(string accessToken,CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);await this.refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
            var identity=await this.verifier.AuthenticateAsync(accessToken,cancellationToken).ConfigureAwait(false);
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);this.session.UpdateValidatedIdentity(identity);this.accessToken=accessToken;
        }finally{this.refresh.Release();}
    }
    public void Dispose(){if(Interlocked.Exchange(ref this.disposed,1)==0){Interlocked.Exchange(ref this.accessToken,null);}}
}
