using System.Net;
using System.Security.Cryptography;
using System.Web;
using Microsoft.IdentityModel.Tokens;
using Seamless.Authentication;
using Seamless.Security;
using SignIn=Seamless.Authentication.OidcSignInRequest;

namespace Management.Tests;

public sealed class OidcSignInRequest{
    private static readonly Uri Redirect=new("http://127.0.0.1:49151/callback");
    private static Task<Uri> BrowserAsync(SignIn request,bool changeState=false,bool changeChallenge=false)=>BrowserAsync(request.StartUrl,changeState,changeChallenge);
    internal static async Task<Uri> BrowserAsync(Uri startUrl,bool changeState=false,bool changeChallenge=false){
        using var browser=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        var page=await browser.GetStringAsync(startUrl);Assert.Contains("開発用の認証確認",page);
        var fields=HttpUtility.ParseQueryString(startUrl.Query).AllKeys.ToDictionary(static name=>name!,name=>HttpUtility.ParseQueryString(startUrl.Query)[name]!);
        fields["subject"]="dev-user";Assert.Equal("S256",fields["code_challenge_method"]);Assert.True(fields.ContainsKey("nonce"));
        if(changeChallenge){fields["code_challenge"]=Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));}
        using var response=await browser.PostAsync(new Uri(startUrl,"/authorize"),new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var callback=response.Headers.Location!;
        if(changeState){var query=HttpUtility.ParseQueryString(callback.Query);query["state"]="wrong";callback=new UriBuilder(callback){Query=query.ToString()}.Uri;}
        return callback;
    }
    [Fact]public async Task BrowserCallbackAcceptsOnlyMatchingLoopbackResponseAndCloses(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var browser=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false});
        Uri? redirect=null;
        Task? navigation=null;
        var signIn=OidcBrowserSignIn.SignInAsync(issuer.Settings,(url,token)=>{
            navigation=FollowRedirectAsync(url,token);
            return Task.CompletedTask;
        },cancellationToken:deadline.Token);
        var result=await signIn;
        await navigation!;
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        Assert.Equal("dev-user",(await verifier.AuthenticateAsync(result.AccessToken,deadline.Token)).Subject.Value);
        await Assert.ThrowsAsync<HttpRequestException>(()=>browser.GetAsync(redirect!,deadline.Token));

        async Task FollowRedirectAsync(Uri url,CancellationToken cancellationToken){
            redirect=new Uri(HttpUtility.ParseQueryString(url.Query)["redirect_uri"]!);
            Assert.Equal("127.0.0.1",redirect.Host);
            Assert.True(redirect.Port>0);
            var callback=await BrowserAsync(url);
            using(var duplicate=await browser.GetAsync(callback.AbsoluteUri+"&state=wrong",cancellationToken)){
                Assert.Equal(HttpStatusCode.BadRequest,duplicate.StatusCode);
            }
            using(var foreignHost=new HttpRequestMessage(HttpMethod.Get,callback)){
                foreignHost.Headers.Host="foreign.invalid";
                using var response=await browser.SendAsync(foreignHost,cancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            }
            using var success=await browser.GetAsync(callback,cancellationToken);
            Assert.Equal(HttpStatusCode.OK,success.StatusCode);
            Assert.DoesNotContain(HttpUtility.ParseQueryString(callback.Query)["code"]!,await success.Content.ReadAsStringAsync(cancellationToken));
        }
    }
    [Fact]public async Task BrowserCallbackCancellationReleasesThePort(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Uri? redirect=null;
        var pending=OidcBrowserSignIn.SignInAsync(issuer.Settings,(url,_)=>{
            redirect=new Uri(HttpUtility.ParseQueryString(url.Query)["redirect_uri"]!);
            cancel.Cancel();
            return Task.CompletedTask;
        },cancellationToken:cancel.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
        Assert.NotNull(redirect);
        using var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,redirect.Port){ExclusiveAddressUse=true};
        listener.Start();
    }
    [Fact]public async Task SignInAndApiVerificationUseRealCodePkceAndCurrentMembership(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var request=await SignIn.PrepareAsync(issuer.Settings,Redirect);var callback=await BrowserAsync(request);var result=await request.CompleteAsync(callback);
        Assert.DoesNotContain(result.AccessToken,result.ToString());using var verifier=new OidcTokenVerifier(issuer.Settings);var identity=await verifier.AuthenticateAsync(result.AccessToken);
        Assert.Equal(new SecuritySubjectId(issuer.Id,"dev-user"),identity.Subject);Assert.Equal("developers",Assert.Single(identity.Groups).Value);
        issuer.Groups=["readers"];var refreshed=await verifier.AuthenticateAsync(result.AccessToken);Assert.Equal("readers",Assert.Single(refreshed.Groups).Value);Assert.Equal(2,issuer.MembershipChecks);
        issuer.MembershipUnavailable=true;Assert.Equal("MembershipUnavailable",(await Assert.ThrowsAsync<OidcAuthenticationException>(()=>verifier.AuthenticateAsync(result.AccessToken))).Code);
        await Assert.ThrowsAsync<OidcAuthenticationException>(()=>request.CompleteAsync(callback));
    }
    [Fact]public async Task InvalidStatePkceAndNonceAreRejected(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using(var request=await SignIn.PrepareAsync(issuer.Settings,Redirect)){var callback=await BrowserAsync(request,changeState:true);await Assert.ThrowsAsync<OidcAuthenticationException>(()=>request.CompleteAsync(callback));}
        using(var request=await SignIn.PrepareAsync(issuer.Settings,Redirect)){var callback=await BrowserAsync(request,changeChallenge:true);await Assert.ThrowsAsync<OidcAuthenticationException>(()=>request.CompleteAsync(callback));}
        issuer.WrongNonce=true;using(var request=await SignIn.PrepareAsync(issuer.Settings,Redirect)){var callback=await BrowserAsync(request);await Assert.ThrowsAsync<OidcAuthenticationException>(()=>request.CompleteAsync(callback));}
    }
    [Fact]public async Task WrongTokenAudienceIssuerSignatureExpiryAndScopeAreRejected(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var other=RSA.Create(2048);
        foreach(var token in new[]{
            issuer.CreateToken(OidcDevelopmentIssuer.ClientId,scope:"seamless.database"),
            issuer.CreateToken(OidcDevelopmentIssuer.Audience,issuer:"https://other.invalid",scope:"seamless.database"),
            issuer.CreateToken(OidcDevelopmentIssuer.Audience,lifetime:TimeSpan.FromMinutes(-5),scope:"seamless.database"),
            issuer.CreateToken(OidcDevelopmentIssuer.Audience,lifetime:TimeSpan.FromSeconds(-1),scope:"seamless.database"),
            issuer.CreateToken(OidcDevelopmentIssuer.Audience,signingKey:new RsaSecurityKey(other){KeyId="other"},scope:"seamless.database"),
            issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"other")
        }){Assert.Equal("TokenRejected",(await Assert.ThrowsAsync<OidcAuthenticationException>(()=>verifier.AuthenticateAsync(token))).Code);}
        Assert.Equal(0,issuer.MembershipChecks);
    }
    [Fact]public void DevelopmentHttpCannotBeUsedOutsideLoopback(){
        var settings=new OidcProviderSettings(Guid.NewGuid(),"http://127.0.0.1:49151","api","client",DevelopmentLoopback:true);settings.Validate();
        Assert.Throws<ArgumentException>(()=>(settings with{DevelopmentLoopback=false}).Validate());Assert.Throws<ArgumentException>(()=>(settings with{Authority="http://192.0.2.1"}).Validate());Assert.Throws<ArgumentException>(()=>(settings with{Authority="http://localhost:49151"}).Validate());Assert.Throws<ArgumentException>(()=>(settings with{Audience="client"}).Validate());
        Assert.Throws<ArgumentException>(()=>(settings with{Authority="https://example.invalid"}).Validate());Assert.Throws<ArgumentException>(()=>(settings with{Authority="http://127.0.0.1:49151?issuer=other"}).Validate());
    }
}
