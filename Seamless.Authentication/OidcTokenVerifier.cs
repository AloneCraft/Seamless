using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Seamless.Security;

namespace Seamless.Authentication;

/// <summary>信頼する設定へ束縛したJWT検証と、現在の所属の再確認。</summary>
public sealed class OidcTokenVerifier:IDisposable{
    private readonly OidcProviderSettings settings;
    private readonly HttpClient http;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> configuration;
    private readonly JsonWebTokenHandler tokens=new(){MaximumTokenSizeInBytes=65536};
    private readonly TimeProvider time;
    private readonly SemaphoreSlim? verifications;
    public OidcTokenVerifier(OidcProviderSettings settings,TimeProvider? time=null):this(settings,time,null){}
    private OidcTokenVerifier(OidcProviderSettings settings,TimeProvider? time,SemaphoreSlim? verifications){
        settings.Validate();this.settings=settings;this.time=time??TimeProvider.System;this.http=new(new OidcEndpointHandler(settings)){Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=262144};
        this.verifications=verifications;
        this.configuration=new(settings.Authority.TrimEnd('/')+"/.well-known/openid-configuration",new OpenIdConnectConfigurationRetriever(),new HttpDocumentRetriever(this.http){RequireHttps=!settings.DevelopmentLoopback});
    }
    /// <summary>初回認証と所属再確認で共有する、検証器単位の同時実行枠を設ける。</summary>
    public static OidcTokenVerifier ForServer(OidcProviderSettings settings,int maximumConcurrentVerifications,TimeProvider? time=null){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentVerifications);
        // Wait(0)だけを使い、待機列やOS待機ハンドルを作らない。
        return new(settings,time,new(maximumConcurrentVerifications,maximumConcurrentVerifications));
    }
    internal async Task<(ClaimsPrincipal Principal,JsonWebToken Token,OpenIdConnectConfiguration Configuration)> ValidateAsync(string encoded,string audience,CancellationToken cancellationToken){
        if(string.IsNullOrWhiteSpace(encoded)||encoded.Length>65536||encoded.Any(char.IsWhiteSpace)){throw Rejected();}
        var metadata=await this.MetadataAsync(cancellationToken).ConfigureAwait(false);
        var parameters=new TokenValidationParameters{
            ValidIssuer=this.settings.Authority,ValidAudience=audience,IssuerSigningKeys=metadata.SigningKeys,
            ValidateIssuer=true,ValidateAudience=true,ValidateIssuerSigningKey=true,RequireSignedTokens=true,RequireExpirationTime=true,ValidateLifetime=true,
            ClockSkew=TimeSpan.FromSeconds(30),ValidAlgorithms=[SecurityAlgorithms.RsaSha256,SecurityAlgorithms.EcdsaSha256],ValidTypes=["JWT","at+jwt"],NameClaimType="name",RoleClaimType="roles"
        };
        var result=await this.tokens.ValidateTokenAsync(encoded,parameters).ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();
        if(!result.IsValid||result.SecurityToken is not JsonWebToken token||string.IsNullOrWhiteSpace(token.Subject)||token.Subject.Length>1024||token.Subject.Any(char.IsControl)||token.ValidTo<=this.time.GetUtcNow().UtcDateTime){if(result.Exception is SecurityTokenSignatureKeyNotFoundException){this.configuration.RequestRefresh();}throw Rejected();}
        return(new(result.ClaimsIdentity),token,metadata);
    }
    internal async Task<OpenIdConnectConfiguration> MetadataAsync(CancellationToken cancellationToken){
        OpenIdConnectConfiguration metadata;
        try{metadata=await this.configuration.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);}catch(Exception error)when(error is not OperationCanceledException){throw new OidcAuthenticationException("IdentityProviderUnavailable","The configured identity provider metadata is unavailable.");}
        if(metadata.Issuer!=this.settings.Authority){throw Rejected();}
        foreach(var endpoint in new[]{metadata.AuthorizationEndpoint,metadata.TokenEndpoint,metadata.JwksUri}){this.settings.ValidateEndpoint(new Uri(endpoint,UriKind.Absolute));}return metadata;
    }
    internal async Task<(ClaimsPrincipal Principal,JsonWebToken Token,OpenIdConnectConfiguration Configuration)> ValidateAccessAsync(string accessToken,CancellationToken cancellationToken){
        var verified=await this.ValidateAsync(accessToken,this.settings.Audience,cancellationToken).ConfigureAwait(false);
        var scopes=verified.Principal.FindAll("scope").Concat(verified.Principal.FindAll("scp")).SelectMany(static claim=>claim.Value.Split(' ',StringSplitOptions.RemoveEmptyEntries));
        if(!scopes.Contains(this.settings.RequiredApiScope,StringComparer.Ordinal)){throw Rejected();}return verified;
    }
    public async Task<AuthenticatedIdentity> AuthenticateAsync(string accessToken,CancellationToken cancellationToken=default){
        var slots=this.verifications;
        if(slots is not null&&!slots.Wait(0,cancellationToken)){
            throw new OidcAuthenticationException(OidcAuthenticationException.BusyCode,"The identity verification limit has been reached.");
        }
        try{
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(15));
            return await this.AuthenticateCoreAsync(accessToken,deadline.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){throw new OidcAuthenticationException("IdentityProviderUnavailable","The identity provider did not respond within the authentication deadline.");}
        catch(JsonException){throw new OidcAuthenticationException("MembershipUnavailable","The current membership response is invalid.");}
        finally{slots?.Release();}
    }
    private async Task<AuthenticatedIdentity> AuthenticateCoreAsync(string accessToken,CancellationToken cancellationToken){
        var verified=await this.ValidateAccessAsync(accessToken,cancellationToken).ConfigureAwait(false);
        if(string.IsNullOrEmpty(verified.Configuration.UserInfoEndpoint)){throw new OidcAuthenticationException("MembershipUnavailable","A configured current-membership endpoint is required.");}
        var endpoint=new Uri(verified.Configuration.UserInfoEndpoint,UriKind.Absolute);this.settings.ValidateEndpoint(endpoint);
        using var request=new HttpRequestMessage(HttpMethod.Get,endpoint);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",accessToken);
        HttpResponseMessage response;
        try{response=await this.http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken).ConfigureAwait(false);}catch(HttpRequestException){throw new OidcAuthenticationException("MembershipUnavailable","Current membership could not be verified.");}
        using(response){
            if(!response.IsSuccessStatusCode){throw new OidcAuthenticationException("MembershipUnavailable","Current membership could not be verified.");}
            await using var stream=await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);using var buffer=new MemoryStream();var chunk=new byte[4096];
            while(true){var count=await stream.ReadAsync(chunk,cancellationToken).ConfigureAwait(false);if(count==0){break;}if(buffer.Length+count>65536){throw Rejected();}buffer.Write(chunk,0,count);}
            using var json=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=8});var root=json.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("sub",out var subject)||subject.ValueKind!=JsonValueKind.String||subject.GetString()!=verified.Token.Subject||!root.TryGetProperty(this.settings.GroupClaim,out var groups)||groups.ValueKind!=JsonValueKind.Array){throw new OidcAuthenticationException("MembershipUnavailable","The current subject and groups could not be verified.");}
            var identities=new List<SecuritySubjectId>();foreach(var group in groups.EnumerateArray()){if(identities.Count>=4096||group.ValueKind!=JsonValueKind.String){throw Rejected();}var id=group.GetString();if(string.IsNullOrWhiteSpace(id)||id.Length>1024||id.Any(char.IsControl)){throw Rejected();}identities.Add(new(this.settings.Id,id));}
            var display=verified.Token.Subject;if(root.TryGetProperty("name",out var name)&&name.ValueKind==JsonValueKind.String){display=name.GetString()!;}
            return new(new(this.settings.Id,verified.Token.Subject),display,identities,new DateTimeOffset(verified.Token.ValidTo,TimeSpan.Zero),this.time.GetUtcNow());
        }
    }
    public void Dispose()=>this.http.Dispose();
    private static OidcAuthenticationException Rejected()=>new("TokenRejected","The token is not valid for this configured identity provider and API.");
}
