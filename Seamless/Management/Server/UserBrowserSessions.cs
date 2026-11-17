using System.Security.Cryptography;

namespace Seamless.Management.Server;

public sealed record UserBrowserSession(string Id,UserInfo User,DateTimeOffset ExpiresAt){
    public override string ToString()=>$"UserBrowserSession {{ ExpiresAt = {this.ExpiresAt:O} }}";
}

/// <summary>外部ManagerのCookieを、既存の利用者別認証セッションへ対応付ける。</summary>
public sealed class UserBrowserSessions:IManagerBrowserSessions{
    private sealed record Entry(string Token,DateTimeOffset ExpiresAt);
    private readonly ManagementService service;
    private readonly object gate=new();
    private readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
    private readonly int capacity;
    private bool disposed;
    public Guid InstanceId=>this.service.Info.InstanceId;
    public string PathBase=>"/manager/";
    public TimeSpan SessionLifetime=>TimeSpan.FromMinutes(30);
    public UserBrowserSessions(ManagementService service,int capacity=64){
        ArgumentNullException.ThrowIfNull(service);
        if(capacity is <1 or >1024){throw new ArgumentOutOfRangeException(nameof(capacity));}
        this.service=service;this.capacity=capacity;
    }
    public UserBrowserSession SignIn(LoginRequest request){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            foreach(var key in this.entries.Where(item=>item.Value.ExpiresAt<=DateTimeOffset.UtcNow).Select(item=>item.Key).ToArray()){this.entries.Remove(key);}
            if(this.entries.Count>=this.capacity){throw new ManagementException("Busy","ブラウザーセッションの上限です。");}
            var login=this.service.Login(request);
            var id=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            this.entries.Add(Key(id),new(login.Token,login.ExpiresAt));
            return new(id,login.User,login.ExpiresAt);
        }
    }
    internal UserAccount Account(string? id){
        Entry entry;
        lock(this.gate){
            if(this.disposed||id is null||id.Length!=64||!this.entries.TryGetValue(Key(id),out entry!)||entry.ExpiresAt<=DateTimeOffset.UtcNow){throw new ManagementException("Authentication","ログインが必要です。");}
        }
        return this.service.Authenticate(entry.Token);
    }
    public UserInfo Authenticate(string? id){var user=this.Account(id);return new(user.Name,user.Grants.ToArray(),user.Disabled);}
    public void SignOut(string? id){lock(this.gate){if(id is not null){this.entries.Remove(Key(id));}}}
    public void Dispose(){lock(this.gate){this.disposed=true;this.entries.Clear();}}
    private static string Key(string id)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)));
}
