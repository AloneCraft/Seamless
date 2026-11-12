using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text.Json;
using Seamless.Authentication;

namespace Management.Tests;

internal static class OidcSessionProbe{
    internal static Task<int> RunAsync(string[] args){if(!OperatingSystem.IsWindows()){return Task.FromResult(3);}return RunWindowsAsync(args);}
    [SupportedOSPlatform("windows")]
    private static async Task<int> RunWindowsAsync(string[] args){
        var key=JsonSerializer.Deserialize<OidcCredentialKey>(Convert.FromBase64String(args[0]))!;var count=int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture);if(count is <1 or >10){return 4;}
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await using var pipe=new NamedPipeClientStream(".",args[1],PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(timeout.Token);using var process=Process.GetCurrentProcess();
        using(var writer=new BinaryWriter(pipe,System.Text.Encoding.UTF8,true)){writer.Write(process.Id);writer.Write(process.StartTime.ToUniversalTime().Ticks);writer.Write(process.ProcessorAffinity.ToInt64());writer.Flush();}
        var start=new byte[1];if(await pipe.ReadAsync(start,timeout.Token)!=1||start[0]!=1){return 5;}
        var settings=new OidcProviderSettings(key.AuthorityId,key.Issuer,key.Audience,key.ClientId,DevelopmentLoopback:true);var store=new Seamless.Authentication.WindowsOidcCredentialStore();
        for(var i=0;i<count;i++){
            await using var session=await Seamless.Authentication.OidcClientSession.ResumeAsync(settings,key.InstanceId,key.Subject,store,cancellationToken:timeout.Token);if(session is null){return 6;}if(string.IsNullOrEmpty(await session.GetAccessTokenAsync(timeout.Token))){return 7;}
        }
        await pipe.WriteAsync(new byte[]{1},timeout.Token);await pipe.FlushAsync(timeout.Token);return 0;
    }
}
