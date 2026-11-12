using System.Net;
using System.Security.Cryptography;

namespace Seamless.Management.Server;

public sealed record LocalBrowserSession(string Id,UserInfo User,DateTimeOffset ExpiresAt){
    public override string ToString()=>$"LocalBrowserSession {{ ExpiresAt = {this.ExpiresAt:O} }}";
}

/// <summary>OS所有者に結び付けた起動能力とセッションを、ホストの寿命内で有界に管理する。</summary>
public sealed class LocalBrowserSessions:IManagerBrowserSessions{
    private readonly object gate=new();
    private readonly LocalOwnerIdentity owner=LocalOwnerIdentity.Capture();
    private readonly Dictionary<string,DateTimeOffset> launches=new(StringComparer.Ordinal);
    private readonly Dictionary<string,DateTimeOffset> sessions=new(StringComparer.Ordinal);
    private readonly TimeSpan launchLifetime;
    private readonly TimeSpan sessionLifetime;
    private readonly int capacity;
    private readonly TimeProvider timeProvider;
    private Uri? origin;
    private bool disposed;
    public Guid InstanceId{get;}
    public TimeSpan SessionLifetime=>this.sessionLifetime;
    /// <summary>再起動後も共通のWebパス。アクセス権はOS所有者の一回限りの起動能力と有期限Cookieで検証する。</summary>
    public string PathBase=>"/manager/";

    public LocalBrowserSessions(Guid instanceId,TimeSpan? launchLifetime=null,TimeSpan? sessionLifetime=null,int capacity=16,TimeProvider? timeProvider=null){
        if(instanceId==Guid.Empty){throw new ArgumentException("インスタンスIDが必要です。",nameof(instanceId));}
        this.launchLifetime=launchLifetime??TimeSpan.FromMinutes(2);
        this.sessionLifetime=sessionLifetime??TimeSpan.FromHours(8);
        if(this.launchLifetime<=TimeSpan.Zero||this.launchLifetime>TimeSpan.FromMinutes(10)){throw new ArgumentOutOfRangeException(nameof(launchLifetime));}
        if(this.sessionLifetime<=TimeSpan.Zero||this.sessionLifetime>TimeSpan.FromDays(1)){throw new ArgumentOutOfRangeException(nameof(sessionLifetime));}
        if(capacity is <1 or >1024){throw new ArgumentOutOfRangeException(nameof(capacity));}
        this.InstanceId=instanceId;this.capacity=capacity;this.timeProvider=timeProvider??TimeProvider.System;
    }

    public void BindOrigin(Uri origin){
        ArgumentNullException.ThrowIfNull(origin);
        if(!origin.IsAbsoluteUri||origin.Scheme is not("http" or "https")||!IPAddress.TryParse(origin.DnsSafeHost,out var address)||!IPAddress.IsLoopback(address)
            ||origin.UserInfo.Length!=0||origin.AbsolutePath!="/"||origin.Query.Length!=0||origin.Fragment.Length!=0){
            throw new ArgumentException("数値のループバックアドレスとポートを持つWeb起点が必要です。",nameof(origin));
        }
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.origin is not null){throw new InvalidOperationException("Web起点は既に設定されています。");}
            this.origin=new(origin.GetLeftPart(UriPartial.Authority)+"/");
        }
    }

    public BrowserLaunchInfo CreateLaunch(){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.origin is null){throw new ManagementException("Unsupported","ローカルWeb画面は起動していません。");}
            var now=this.timeProvider.GetUtcNow();
            RemoveExpired(this.launches,now);RemoveExpired(this.sessions,now);
            if(this.launches.Count+this.sessions.Count>=this.capacity){throw new ManagementException("Busy","ブラウザーセッションの上限です。不要なセッションを失効させてください。");}
            var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var expires=now+this.launchLifetime;
            this.launches.Add(Key(token)!,expires);
            return new(this.origin.AbsoluteUri+this.PathBase.TrimStart('/')+"launch#"+token,expires);
        }
    }

    public LocalBrowserSession Redeem(string token,string origin){
        var key=Key(token);
        lock(this.gate){
            if(this.disposed||this.origin is null||origin!=this.origin.GetLeftPart(UriPartial.Authority)||key is null){throw Denied();}
            if(!this.launches.Remove(key,out var expires)||expires<=this.timeProvider.GetUtcNow()){throw Denied();}
            var id=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var sessionExpires=this.timeProvider.GetUtcNow()+this.sessionLifetime;
            this.sessions.Add(Key(id)!,sessionExpires);
            return new(id,this.Owner(),sessionExpires);
        }
    }

    public UserInfo Authenticate(string? id){
        var key=Key(id);
        lock(this.gate){
            if(this.disposed||key is null||!this.sessions.TryGetValue(key,out var expires)){throw Denied();}
            if(expires<=this.timeProvider.GetUtcNow()){this.sessions.Remove(key);throw Denied();}
            return this.Owner();
        }
    }

    public void RevokeAll(){lock(this.gate){this.launches.Clear();this.sessions.Clear();}}
    public void Dispose(){lock(this.gate){this.disposed=true;this.launches.Clear();this.sessions.Clear();}}
    private UserInfo Owner(){var account=this.owner.Account();return new(account.Name,account.Grants,false);}
    private static ManagementException Denied()=>new("Authentication","ブラウザーセッションが無効か期限切れです。所有者の画面から開き直してください。");
    private static string? Key(string? credential){
        if(credential is null||credential.Length!=64){return null;}
        try{return Convert.ToHexString(SHA256.HashData(Convert.FromHexString(credential)));}
        catch(FormatException){return null;}
    }
    private static void RemoveExpired(Dictionary<string,DateTimeOffset> entries,DateTimeOffset now){
        foreach(var key in entries.Where(item=>item.Value<=now).Select(item=>item.Key).ToArray()){entries.Remove(key);}
    }
}
