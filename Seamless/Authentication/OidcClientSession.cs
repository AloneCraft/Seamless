using Seamless.Security;
using Duende.IdentityModel.Client;

namespace Seamless.Authentication;

public sealed record OidcClientIdentity(SecuritySubjectId Subject,string DisplayName,DateTimeOffset ExpiresAt);

public sealed class OidcClientSession:IAsyncDisposable{
    private readonly OidcProviderSettings settings;
    private readonly OidcCredentialKey key;
    private readonly IOidcSessionCredentialStore store;
    private readonly OidcTokenVerifier verifier;
    private readonly HttpClient http;
    private readonly TimeProvider time;
    private readonly object gate=new();
    private Task<string>? renewing;
    private State? state;
    private int disposed;
    public OidcClientIdentity? Current{get{if(Volatile.Read(ref this.disposed)!=0){return null;}return Volatile.Read(ref this.state)?.Identity;}}
    private OidcClientSession(OidcProviderSettings settings,OidcCredentialKey key,IOidcSessionCredentialStore store,OidcTokenVerifier verifier,TimeProvider time){
        key.Target();this.settings=settings;this.key=key;this.store=store;this.verifier=verifier;this.time=time;this.http=new(new OidcEndpointHandler(settings)){Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=262144};
    }
    public static async Task<OidcClientSession> SignInAsync(OidcProviderSettings settings,Guid instanceId,IOidcSessionCredentialStore store,OidcSignInResult result,TimeProvider? time=null,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(settings);ArgumentNullException.ThrowIfNull(store);ArgumentNullException.ThrowIfNull(result);
        var provider=result.Provider;if(provider.Id!=settings.Id||provider.Authority!=settings.Authority||provider.Audience!=settings.Audience||provider.ClientId!=settings.ClientId){throw new OidcAuthenticationException("IdentityProviderMismatch","The sign-in result belongs to different identity provider or client settings.");}
        var clock=time??TimeProvider.System;var verifier=new OidcTokenVerifier(settings,clock);OidcClientSession? session=null;
        try{
            using var deadline=Deadline(cancellationToken);var identity=await IdentityAsync(verifier,settings,result.AccessToken,deadline.Token).ConfigureAwait(false);
            session=new(settings,new(instanceId,settings.Id,settings.Authority,settings.Audience,settings.ClientId,identity.Subject.Value),store,verifier,clock);
            await store.WithLockAsync(session.key,_=>{if(string.IsNullOrEmpty(result.RefreshToken)){store.Delete(session.key);}else{ValidateRefresh(result.RefreshToken);store.Save(session.key,result.RefreshToken);}session.state=new(identity,result.AccessToken);return Task.FromResult(true);},cancellationToken).ConfigureAwait(false);return session;
        }catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){session?.http.Dispose();verifier.Dispose();throw new OidcAuthenticationException("IdentityProviderUnavailable","The sign-in validation did not complete within its deadline.");}
        catch{session?.http.Dispose();verifier.Dispose();throw;}
    }
    public static async Task<OidcClientSession?> ResumeAsync(OidcProviderSettings settings,Guid instanceId,string subject,IOidcSessionCredentialStore store,TimeProvider? time=null,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(store);var clock=time??TimeProvider.System;var verifier=new OidcTokenVerifier(settings,clock);OidcClientSession? session=null;
        try{
            session=new(settings,new(instanceId,settings.Id,settings.Authority,settings.Audience,settings.ClientId,subject),store,verifier,clock);
            var restored=await store.WithLockAsync(session.key,session.RefreshCoreAsync,cancellationToken).ConfigureAwait(false);
            if(restored is null){session.Release();return null;}session.state=restored;return session;
        }catch{session?.http.Dispose();verifier.Dispose();throw;}
    }
    public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);cancellationToken.ThrowIfCancellationRequested();var current=Volatile.Read(ref this.state)??throw SignedOut();
        if(current.Identity.ExpiresAt-this.time.GetUtcNow()>TimeSpan.FromSeconds(30)){return new(current.AccessToken);}
        lock(this.gate){if(this.renewing is null||this.renewing.IsCompleted){this.renewing=this.RenewAsync();}return new(this.renewing.WaitAsync(cancellationToken));}
    }
    private Task<string> RenewAsync()=>this.store.WithLockAsync(this.key,async cancellationToken=>{
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);var current=Volatile.Read(ref this.state)??throw SignedOut();
        if(current.Identity.ExpiresAt-this.time.GetUtcNow()>TimeSpan.FromSeconds(30)){return current.AccessToken;}
        var refreshed=await this.RefreshCoreAsync(cancellationToken).ConfigureAwait(false);if(refreshed is null){Volatile.Write(ref this.state,null);throw SignedOut();}
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);if(Volatile.Read(ref this.state) is null){throw SignedOut();}Volatile.Write(ref this.state,refreshed);return refreshed.AccessToken;
    });
    private async Task<State?> RefreshCoreAsync(CancellationToken cancellationToken){
        var refreshToken=this.store.Read(this.key);if(refreshToken is null){return null;}ValidateRefresh(refreshToken);using var deadline=Deadline(cancellationToken);
        try{
            var metadata=await this.verifier.MetadataAsync(deadline.Token).ConfigureAwait(false);
            using var response=await this.http.RequestRefreshTokenAsync(new RefreshTokenRequest{Address=metadata.TokenEndpoint,ClientId=this.settings.ClientId,ClientCredentialStyle=ClientCredentialStyle.PostBody,RefreshToken=refreshToken},deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if(response.IsError){
                if(response.Error=="invalid_grant"){this.store.Delete(this.key);return null;}
                throw new OidcAuthenticationException("IdentityProviderUnavailable","The identity provider could not renew the sign-in.");
            }
            State state;
            try{
                if(!string.Equals(response.TokenType,"Bearer",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(response.AccessToken)){throw new OidcAuthenticationException("TokenRejected","The renewed token is not supported.");}
                var identity=await IdentityAsync(this.verifier,this.settings,response.AccessToken,deadline.Token).ConfigureAwait(false);
                if(identity.Subject.Value!=this.key.Subject){throw new OidcAuthenticationException("IdentityMismatch","A renewed sign-in must identify the same subject.");}
                if(!string.IsNullOrEmpty(response.IdentityToken)){var id=await this.verifier.ValidateAsync(response.IdentityToken,this.settings.ClientId,deadline.Token).ConfigureAwait(false);if(id.Token.Subject!=this.key.Subject){throw new OidcAuthenticationException("IdentityMismatch","The renewed identity token changed the subject.");}}
                state=new(identity,response.AccessToken);
            }catch(OidcAuthenticationException){this.store.Delete(this.key);Volatile.Write(ref this.state,null);throw;}
            var updated=response.RefreshToken;if(string.IsNullOrEmpty(updated)){updated=refreshToken;}ValidateRefresh(updated);this.store.Save(this.key,updated);return state;
        }catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){throw new OidcAuthenticationException("IdentityProviderUnavailable","The sign-in renewal did not complete within its deadline.");}
        catch(HttpRequestException){throw new OidcAuthenticationException("IdentityProviderUnavailable","The identity provider could not be reached.");}
    }
    public async Task SignOutAsync(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);Volatile.Write(ref this.state,null);
        await this.store.WithLockAsync(this.key,_=>{this.store.Delete(this.key);return Task.FromResult(true);},cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync(){
        if(Interlocked.Exchange(ref this.disposed,1)!=0){return;}
        try{await this.store.WithLockAsync(this.key,_=>Task.FromResult(true)).ConfigureAwait(false);}finally{this.Release();}
    }
    private void Release(){Volatile.Write(ref this.state,null);this.verifier.Dispose();this.http.Dispose();}
    private static async Task<OidcClientIdentity> IdentityAsync(OidcTokenVerifier verifier,OidcProviderSettings settings,string accessToken,CancellationToken cancellationToken){
        var verified=await verifier.ValidateAccessAsync(accessToken,cancellationToken).ConfigureAwait(false);var display=verified.Token.Subject;var name=verified.Principal.FindFirst("name");if(name is not null){display=name.Value;}
        return new(new(settings.Id,verified.Token.Subject),display,new DateTimeOffset(verified.Token.ValidTo,TimeSpan.Zero));
    }
    private static CancellationTokenSource Deadline(CancellationToken token){var source=CancellationTokenSource.CreateLinkedTokenSource(token);source.CancelAfter(TimeSpan.FromSeconds(15));return source;}
    private static void ValidateRefresh(string token){if(string.IsNullOrWhiteSpace(token)||token.Length>65536||token.Any(char.IsControl)){throw new OidcAuthenticationException("CredentialStoreInvalid","The refresh credential does not have a supported format.");}}
    private static OidcAuthenticationException SignedOut()=>new("SignInRequired","A new organization sign-in is required.");
    private sealed class State(OidcClientIdentity identity,string accessToken){
        public OidcClientIdentity Identity{get;}=identity;
        public string AccessToken{get;}=accessToken;
        public override string ToString()=>"OIDC client session (credentials omitted)";
    }
}
