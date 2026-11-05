using System.Net;

namespace Seamless.Authentication;

public sealed record OidcProviderSettings(Guid Id,string Authority,string Audience,string ClientId,string Scope="openid profile offline_access seamless.database",string RequiredApiScope="seamless.database",string GroupClaim="groups",bool DevelopmentLoopback=false){
    public void Validate(){
        if(this.Id==Guid.Empty||string.IsNullOrWhiteSpace(this.Audience)||string.IsNullOrWhiteSpace(this.ClientId)||this.Audience==this.ClientId||string.IsNullOrWhiteSpace(this.RequiredApiScope)||string.IsNullOrWhiteSpace(this.GroupClaim)){throw new ArgumentException("Distinct API and native-client identities and an authority are required.");}
        var authority=new Uri(this.Authority,UriKind.Absolute);this.ValidateEndpoint(authority);if(!string.IsNullOrEmpty(authority.Query)){throw new ArgumentException("The issuer authority cannot include a query.");}
        if(!this.Scope.Split(' ',StringSplitOptions.RemoveEmptyEntries).Contains("openid",StringComparer.Ordinal)){throw new ArgumentException("The OpenID scope is required.");}
    }
    internal void ValidateEndpoint(Uri uri){
        if(!uri.IsAbsoluteUri||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)){throw new ArgumentException("Invalid identity provider endpoint.");}
        if(this.DevelopmentLoopback&&(!IPAddress.TryParse(uri.Host,out var developmentAddress)||!IPAddress.IsLoopback(developmentAddress))){throw new ArgumentException("A development identity provider must use literal loopback endpoints.");}
        if(uri.Scheme==Uri.UriSchemeHttps){return;}
        if(this.DevelopmentLoopback&&uri.Scheme==Uri.UriSchemeHttp&&IPAddress.TryParse(uri.Host,out var address)&&IPAddress.IsLoopback(address)){return;}
        throw new ArgumentException("OIDC requires HTTPS; development HTTP is restricted to a literal loopback address.");
    }
}
public sealed class OidcAuthenticationException(string code,string message):InvalidOperationException(message){public string Code{get;}=code;}

internal sealed class OidcEndpointHandler(OidcProviderSettings settings):DelegatingHandler(new HttpClientHandler{AllowAutoRedirect=false}){
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){settings.ValidateEndpoint(request.RequestUri??throw new ArgumentException("An endpoint is required."));return base.SendAsync(request,cancellationToken);}
}
