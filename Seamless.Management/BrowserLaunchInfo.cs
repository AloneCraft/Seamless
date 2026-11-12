namespace Seamless.Management;

/// <summary>Urlのフラグメントは短命の起動資格情報。ログや履歴へ保存しない。</summary>
public sealed record BrowserLaunchInfo(string Url,DateTimeOffset ExpiresAt){
    public override string ToString()=>$"BrowserLaunchInfo {{ ExpiresAt = {this.ExpiresAt:O} }}";
}
