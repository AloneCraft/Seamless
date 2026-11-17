using System.Diagnostics;
using System.Net;

namespace Seamless.Management.Server;

/// <summary>同じOS所有者のIPCを使い、既存サーバーのManagerをブラウザーで開く。</summary>
public static class ManagerBrowserLauncher{
    public static async Task<BrowserLaunchInfo> CreateLaunchAsync(string root,CancellationToken cancellationToken=default){
        var manager=LocalManagerConnection.Read(root);
        if(manager is not null){return await CreateLaunchAsync(manager.InstanceId,manager.GetPipeEndpoint(),cancellationToken).ConfigureAwait(false);}
        var connection=LocalServerConnection.Read(root)??throw new ManagementException("NotFound","稼働中のローカルサーバーが見つかりません。");
        if(connection.PipeName is null){throw new ManagementException("Unsupported","このサーバーは所有者IPCを提供していません。");}
        return await CreateLaunchAsync(connection.InstanceId,connection.GetPipeEndpoint()!,cancellationToken).ConfigureAwait(false);
    }
    private static async Task<BrowserLaunchInfo> CreateLaunchAsync(Guid instanceId,string pipeName,CancellationToken cancellationToken){
        using var client=ManagementClient.ForLocalPipe(pipeName);
        var info=await client.InfoAsync(cancellationToken).ConfigureAwait(false);
        if(info.InstanceId!=instanceId||!info.Capabilities.Contains("OsOwnerPipeV1",StringComparer.Ordinal)||!info.Capabilities.Contains("LocalBrowserV1",StringComparer.Ordinal)){
            throw new ManagementException("Unsupported","OS所有者のブラウザー起動に対応したサーバーが必要です。");
        }
        return await client.CreateLocalBrowserLaunchAsync(cancellationToken).ConfigureAwait(false);
    }
    public static void Open(BrowserLaunchInfo launch){
        if(!Uri.TryCreate(launch.Url,UriKind.Absolute,out var url)||url.Scheme is not("http" or "https")||!IPAddress.TryParse(url.DnsSafeHost,out var address)||!IPAddress.IsLoopback(address)||url.UserInfo.Length>0){
            throw new ManagementException("InvalidRequest","ローカルManagerの起動URLが不正です。");
        }
        Process.Start(new ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});
    }
}
