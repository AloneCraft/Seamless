using System.Text.Json;

namespace Seamless.Management;

public sealed partial class ManagementClient{
    public Task<BrowserLaunchInfo> CreateLocalBrowserLaunchAsync(CancellationToken cancellationToken=default)=>
        this.CallAsync<BrowserLaunchInfo>("browserLaunch","",new{},cancellationToken);
    public Task RevokeLocalBrowserSessionsAsync(CancellationToken cancellationToken=default)=>
        this.CallAsync<JsonElement>("browserRevoke","",new{},cancellationToken);
}
