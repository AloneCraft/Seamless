using System.Security.Cryptography;
using System.Text;

namespace Seamless.Authentication;

/// <summary>保存した更新用資格情報を別のサーバー・発行元・アプリ・主体へ送らないためのキー。</summary>
public sealed record OidcCredentialKey(Guid InstanceId,Guid AuthorityId,string Issuer,string Audience,string ClientId,string Subject){
    internal string Target(){
        if(this.InstanceId==Guid.Empty||this.AuthorityId==Guid.Empty){throw new ArgumentException("The server instance and authority IDs are required.");}
        foreach(var item in new[]{this.Issuer,this.Audience,this.ClientId,this.Subject}){if(string.IsNullOrWhiteSpace(item)||item.Any(char.IsControl)){throw new ArgumentException("A bounded opaque credential identity is required.");}}
        if(this.Issuer.Length>4096||this.Audience.Length>4096||this.ClientId.Length>4096||this.Subject.Length>1024){throw new ArgumentException("The credential identity is too large.");}
        var hash=SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0',this.Issuer,this.Audience,this.ClientId,this.Subject)));
        return "Seamless/OIDC/v1/"+this.InstanceId.ToString("N")+"/"+this.AuthorityId.ToString("N")+"/"+Convert.ToHexString(hash);
    }
}
public interface IOidcCredentialStore{
    string? Read(OidcCredentialKey key);
    void Save(OidcCredentialKey key,string refreshToken);
    void Delete(OidcCredentialKey key);
}
/// <summary>読取からローテーション後の保存までを同じ保存キーについてプロセス間で直列化する。</summary>
public interface IOidcSessionCredentialStore:IOidcCredentialStore{
    Task<T> WithLockAsync<T>(OidcCredentialKey key,Func<CancellationToken,Task<T>> action,CancellationToken cancellationToken=default);
}
