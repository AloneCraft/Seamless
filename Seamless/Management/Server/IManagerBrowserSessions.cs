namespace Seamless.Management.Server;

public interface IManagerBrowserSessions:IDisposable{
    Guid InstanceId{get;}
    string PathBase{get;}
    TimeSpan SessionLifetime{get;}
    UserInfo Authenticate(string? id);
}
