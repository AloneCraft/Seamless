using System.Net;
using System.Security.Cryptography;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Results;
using Microsoft.IdentityModel.Tokens;

namespace Seamless.Authentication;

/// <summary>ブラウザー操作とOAuthの検証を分離する、一度だけ使うサインイン要求。</summary>
public sealed class OidcSignInRequest:IDisposable{
    private readonly OidcClient client;
    private readonly AuthorizeState state;
    private readonly OidcTokenVerifier verifier;
    private readonly OidcEndpointHandler backchannel;
    private readonly OidcProviderSettings settings;
    private readonly Uri redirect;
    private int completed;
    public Uri StartUrl{get;}
    private OidcSignInRequest(OidcClient client,AuthorizeState state,OidcTokenVerifier verifier,OidcEndpointHandler backchannel,Uri redirect,OidcProviderSettings settings){this.client=client;this.state=state;this.verifier=verifier;this.backchannel=backchannel;this.redirect=redirect;this.settings=settings;this.StartUrl=new(state.StartUrl);}
    public static async Task<OidcSignInRequest> PrepareAsync(OidcProviderSettings settings,Uri redirect,CancellationToken cancellationToken=default){
        settings.Validate();
        if(redirect.Scheme!=Uri.UriSchemeHttp||!IPAddress.TryParse(redirect.Host,out var address)||!IPAddress.IsLoopback(address)||!string.IsNullOrEmpty(redirect.UserInfo)||!string.IsNullOrEmpty(redirect.Query)||!string.IsNullOrEmpty(redirect.Fragment)){throw new ArgumentException("The desktop callback must use an HTTP loopback address.");}
        var verifier=new OidcTokenVerifier(settings);var backchannel=new OidcEndpointHandler(settings);var nonce=Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        try{
            var options=new OidcClientOptions{Authority=settings.Authority,ClientId=settings.ClientId,RedirectUri=redirect.AbsoluteUri,Scope=settings.Scope,LoadProfile=false,FilterClaims=false,BackchannelHandler=backchannel,IdentityTokenValidator=new IdentityValidator(verifier,settings.ClientId,nonce)};
            options.Policy.RequireIdentityTokenSignature=true;options.Policy.Discovery.RequireHttps=!settings.DevelopmentLoopback;
            var client=new OidcClient(options);var state=await client.PrepareLoginAsync(new Parameters{{"nonce",nonce}},cancellationToken).ConfigureAwait(false);
            settings.ValidateEndpoint(new Uri(state.StartUrl));return new(client,state,verifier,backchannel,redirect,settings);
        }catch{verifier.Dispose();backchannel.Dispose();throw;}
    }
    public async Task<OidcSignInResult> CompleteAsync(Uri response,CancellationToken cancellationToken=default){
        if(Interlocked.Exchange(ref this.completed,1)!=0){throw new OidcAuthenticationException("SignInUsed","The sign-in response has already been processed.");}
        if(Uri.Compare(response,this.redirect,UriComponents.SchemeAndServer|UriComponents.Path,UriFormat.UriEscaped,StringComparison.Ordinal)!=0||response.AbsoluteUri.Length>16384){throw new OidcAuthenticationException("SignInRejected","The sign-in callback does not match the registered request.");}
        var result=await this.client.ProcessResponseAsync(response.AbsoluteUri,this.state,cancellationToken:cancellationToken).ConfigureAwait(false);
        if(result.IsError||string.IsNullOrEmpty(result.IdentityToken)||string.IsNullOrEmpty(result.AccessToken)){throw new OidcAuthenticationException("SignInRejected","The organization sign-in could not be validated.");}
        return new(result.AccessToken,result.RefreshToken,result.AccessTokenExpiration,this.settings);
    }
    public void Dispose(){this.verifier.Dispose();this.backchannel.Dispose();}
    private sealed class IdentityValidator(OidcTokenVerifier verifier,string audience,string nonce):IIdentityTokenValidator{
        public async Task<IdentityTokenValidationResult> ValidateAsync(string identityToken,OidcClientOptions options,CancellationToken cancellationToken=default){
            try{
                var validated=await verifier.ValidateAsync(identityToken,audience,cancellationToken).ConfigureAwait(false);
                if(validated.Principal.FindFirst("nonce")?.Value!=nonce){return new(){Error="invalid_nonce"};}
                return new(){User=validated.Principal,SignatureAlgorithm=validated.Token.Alg};
            }catch(OidcAuthenticationException){return new(){Error="invalid_identity_token"};}
        }
    }
}
public sealed class OidcSignInResult{
    public string AccessToken{get;}
    public string? RefreshToken{get;}
    public DateTimeOffset ExpiresAt{get;}
    internal OidcProviderSettings Provider{get;}
    internal OidcSignInResult(string accessToken,string? refreshToken,DateTimeOffset expiresAt,OidcProviderSettings provider){this.AccessToken=accessToken;this.RefreshToken=refreshToken;this.ExpiresAt=expiresAt;this.Provider=provider;}
    public override string ToString()=>"OIDC sign-in result (credentials omitted)";
}
