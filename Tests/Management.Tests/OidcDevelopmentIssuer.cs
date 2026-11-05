using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Seamless.Authentication;

namespace Management.Tests;

/// <summary>Loopbackの統合試験専用発行元。実アカウント・永続秘密鍵は使わない。</summary>
internal sealed class OidcDevelopmentIssuer:IAsyncDisposable{
    private readonly RSA rsa=RSA.Create(2048);
    private readonly JsonWebTokenHandler tokens=new();
    private readonly ConcurrentDictionary<string,Code> codes=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string,Grant> refreshTokens=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string,Guid> consumed=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid,byte> revoked=new();
    private int refreshCount;
    private WebApplication application=null!;
    private RsaSecurityKey key=null!;
    public string Authority{get;private set;}="";
    public string[] Groups{get;set;}=["developers"];
    public bool MembershipUnavailable{get;set;}
    public bool WrongNonce{get;set;}
    public bool RefreshUnavailable{get;set;}
    public string? RefreshSubject{get;set;}
    public string? RefreshAudience{get;set;}
    public string? RefreshIdentitySubject{get;set;}
    public bool IssueRefreshToken{get;set;}=true;
    public TaskCompletionSource? RefreshGate{get;set;}
    public TaskCompletionSource RefreshEntered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TimeSpan AccessTokenLifetime{get;set;}=TimeSpan.FromMinutes(30);
    public int RefreshCount=>Volatile.Read(ref this.refreshCount);
    public int MembershipChecks{get;private set;}
    public const string ClientId="seamless-manager-dev",Audience="seamless-dev-api";
    public OidcProviderSettings Settings=>new(this.Id,this.Authority,Audience,ClientId,DevelopmentLoopback:true);
    public Guid Id{get;}=Guid.NewGuid();

    public static async Task<OidcDevelopmentIssuer> StartAsync(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("A test root is required.");
        if(!Path.IsPathFullyQualified(root)||!(root.StartsWith("D:\\",StringComparison.OrdinalIgnoreCase)||root.StartsWith("F:\\",StringComparison.OrdinalIgnoreCase))){throw new InvalidOperationException("A D: or F: test root is required.");}
        var directory=Path.Combine(root,"oidc-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var issuer=new OidcDevelopmentIssuer();issuer.key=new(issuer.rsa){KeyId=Guid.NewGuid().ToString("N")};
        var builder=WebApplication.CreateSlimBuilder(new WebApplicationOptions{ContentRootPath=directory,ApplicationName=typeof(OidcDevelopmentIssuer).Assembly.FullName,EnvironmentName="Development",Args=[]});builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(static server=>server.Listen(IPAddress.Loopback,0));
        issuer.application=builder.Build();
        issuer.application.MapGet("/.well-known/openid-configuration",()=>Results.Json(new{issuer=issuer.Authority,authorization_endpoint=issuer.Authority+"/authorize",token_endpoint=issuer.Authority+"/token",userinfo_endpoint=issuer.Authority+"/userinfo",jwks_uri=issuer.Authority+"/keys",response_types_supported=new[]{"code"},grant_types_supported=new[]{"authorization_code","refresh_token"},subject_types_supported=new[]{"public"},id_token_signing_alg_values_supported=new[]{"RS256"},token_endpoint_auth_methods_supported=new[]{"none"},code_challenge_methods_supported=new[]{"S256"},scopes_supported=new[]{"openid","profile","offline_access","seamless.database"}}));
        issuer.application.MapGet("/keys",()=>{var key=issuer.rsa.ExportParameters(false);return Results.Json(new{keys=new[]{new{kty="RSA",kid=issuer.key.KeyId,use="sig",alg="RS256",n=Base64UrlEncoder.Encode(key.Modulus!),e=Base64UrlEncoder.Encode(key.Exponent!)}}});});
        issuer.application.MapGet("/authorize",(HttpContext context)=>{
            var fields=string.Concat(context.Request.Query.Select(item=>"<input type=\"hidden\" name=\""+WebUtility.HtmlEncode(item.Key)+"\" value=\""+WebUtility.HtmlEncode(item.Value.ToString())+"\">"));
            return Results.Content("<!doctype html><html lang=\"ja\"><meta charset=\"utf-8\"><title>Seamless 開発用サインイン</title><h1>開発用の認証確認</h1><p>テスト専用の利用者です。実運用では使用しません。</p><form method=\"post\" action=\"/authorize\">"+fields+"<button name=\"subject\" value=\"dev-user\">開発用利用者で続ける</button></form></html>","text/html; charset=utf-8");
        });
        issuer.application.MapPost("/authorize",async(HttpContext context)=>{
            var form=await context.Request.ReadFormAsync(context.RequestAborted);var redirect=form["redirect_uri"].ToString();
            if(form["client_id"]!=ClientId||form["response_type"]!="code"||form["code_challenge_method"]!="S256"||form["code_challenge"].ToString().Length!=43||form["state"].ToString().Length is 0 or >4096||form["subject"]!="dev-user"||!Uri.TryCreate(redirect,UriKind.Absolute,out var uri)||uri.Scheme!="http"||!IPAddress.TryParse(uri.Host,out var address)||!IPAddress.IsLoopback(address)||uri.AbsolutePath!="/callback"){return Results.BadRequest(new{error="invalid_request"});}
            var code=Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));issuer.codes[code]=new(redirect,form["code_challenge"]!,form["nonce"]!,"dev-user",form["scope"]!,DateTimeOffset.UtcNow.AddMinutes(1));
            return Results.Redirect(redirect+"?code="+Uri.EscapeDataString(code)+"&state="+Uri.EscapeDataString(form["state"]!));
        });
        issuer.application.MapPost("/token",async(HttpContext context)=>{
            var form=await context.Request.ReadFormAsync(context.RequestAborted);
            if(form["grant_type"]=="refresh_token"){
                Interlocked.Increment(ref issuer.refreshCount);if(issuer.RefreshUnavailable){return Results.StatusCode(503);}
                issuer.RefreshEntered.TrySetResult();if(issuer.RefreshGate is{} barrier){await barrier.Task.WaitAsync(context.RequestAborted);}
                var previous=form["refresh_token"].ToString();
                if(form["client_id"]!=ClientId){return Results.BadRequest(new{error="invalid_client"});}
                if(!issuer.refreshTokens.TryRemove(previous,out var grant)){
                    if(issuer.consumed.TryGetValue(previous,out var family)){issuer.revoked.TryAdd(family,0);}return Results.BadRequest(new{error="invalid_grant"});
                }
                issuer.consumed[previous]=grant.Family;if(grant.ExpiresAt<=DateTimeOffset.UtcNow||issuer.revoked.ContainsKey(grant.Family)){return Results.BadRequest(new{error="invalid_grant"});}
                var updated=issuer.Refresh(grant);var access=issuer.CreateToken(issuer.RefreshAudience??Audience,issuer.RefreshSubject??grant.Subject,scope:grant.Scope,lifetime:issuer.AccessTokenLifetime);
                string? identity=null;if(issuer.RefreshIdentitySubject is not null){identity=issuer.CreateToken(ClientId,issuer.RefreshIdentitySubject);}
                return Results.Json(new{access_token=access,id_token=identity,refresh_token=updated,token_type="Bearer",expires_in=1800});
            }
            if(form["grant_type"]!="authorization_code"||form["client_id"]!=ClientId||!issuer.codes.TryRemove(form["code"]!,out var code)||code.ExpiresAt<=DateTimeOffset.UtcNow||form["redirect_uri"]!=code.Redirect){return Results.BadRequest(new{error="invalid_grant"});}
            var challenge=Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]!)));
            if(!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(challenge),Encoding.ASCII.GetBytes(code.Challenge))){return Results.BadRequest(new{error="invalid_grant"});}
            var nonce=code.Nonce;if(issuer.WrongNonce){nonce="wrong";}
            var accessToken=issuer.CreateToken(Audience,code.Subject,scope:code.Scope,lifetime:issuer.AccessTokenLifetime);var identityToken=issuer.CreateToken(ClientId,code.Subject,nonce:nonce);
            string? refresh=null;if(issuer.IssueRefreshToken&&code.Scope.Split(' ',StringSplitOptions.RemoveEmptyEntries).Contains("offline_access",StringComparer.Ordinal)){refresh=issuer.Refresh(new(code.Subject,code.Scope,Guid.NewGuid(),DateTimeOffset.UtcNow.AddDays(1)));}
            return Results.Json(new{access_token=accessToken,id_token=identityToken,refresh_token=refresh,token_type="Bearer",expires_in=1800});
        });
        issuer.application.MapGet("/userinfo",async(HttpContext context)=>{
            issuer.MembershipChecks++;if(issuer.MembershipUnavailable){return Results.StatusCode(503);}
            var header=context.Request.Headers.Authorization.ToString();if(!header.StartsWith("Bearer ",StringComparison.Ordinal)){return Results.Unauthorized();}
            var result=await issuer.tokens.ValidateTokenAsync(header[7..],new TokenValidationParameters{ValidIssuer=issuer.Authority,ValidAudience=Audience,IssuerSigningKey=issuer.key,ClockSkew=TimeSpan.Zero,RequireSignedTokens=true,RequireExpirationTime=true,ValidateLifetime=true,ValidateIssuer=true,ValidateAudience=true,ValidAlgorithms=[SecurityAlgorithms.RsaSha256]});
            if(!result.IsValid||result.SecurityToken is not JsonWebToken token){return Results.Unauthorized();}
            return Results.Json(new{sub=token.Subject,name="開発用利用者",groups=issuer.Groups});
        });
        try{await issuer.application.StartAsync();issuer.Authority=issuer.application.Urls.Single().TrimEnd('/');return issuer;}catch{await issuer.DisposeAsync();throw;}
    }
    public void InvalidateRefreshTokens()=>this.refreshTokens.Clear();
    private string Refresh(Grant grant){var value=Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));this.refreshTokens[value]=grant;return value;}
    public async Task<OidcSignInResult> SignInAsync(){
        var redirect=new Uri("http://127.0.0.1:49151/callback");using var request=await Seamless.Authentication.OidcSignInRequest.PrepareAsync(this.Settings,redirect);using var browser=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        await browser.GetStringAsync(request.StartUrl);var query=HttpUtility.ParseQueryString(request.StartUrl.Query);var form=query.AllKeys.ToDictionary(static name=>name!,name=>query[name]!);form["subject"]="dev-user";
        using var response=await browser.PostAsync(new Uri(this.Authority+"/authorize"),new FormUrlEncodedContent(form));if(response.StatusCode!=HttpStatusCode.Redirect){throw new InvalidOperationException("The development sign-in did not redirect.");}return await request.CompleteAsync(response.Headers.Location!);
    }
    public string CreateToken(string audience,string subject="dev-user",string? scope=null,string? nonce=null,string? issuer=null,TimeSpan? lifetime=null,SecurityKey? signingKey=null){
        var now=DateTime.UtcNow;var claims=new Dictionary<string,object>{{"sub",subject},{"name","開発用利用者"},{"groups",this.Groups}};
        if(scope is not null){claims["scope"]=scope;}if(nonce is not null){claims["nonce"]=nonce;}
        return this.tokens.CreateToken(new SecurityTokenDescriptor{Issuer=issuer??this.Authority,Audience=audience,Claims=claims,IssuedAt=now.AddMinutes(-10),NotBefore=now.AddMinutes(-10),Expires=now+(lifetime??TimeSpan.FromMinutes(30)),SigningCredentials=new(signingKey??this.key,SecurityAlgorithms.RsaSha256),TokenType="JWT"});
    }
    public async ValueTask DisposeAsync(){if(this.application is not null){await this.application.StopAsync();await this.application.DisposeAsync();}this.rsa.Dispose();}
    private sealed record Code(string Redirect,string Challenge,string Nonce,string Subject,string Scope,DateTimeOffset ExpiresAt);
    private sealed record Grant(string Subject,string Scope,Guid Family,DateTimeOffset ExpiresAt);
}
