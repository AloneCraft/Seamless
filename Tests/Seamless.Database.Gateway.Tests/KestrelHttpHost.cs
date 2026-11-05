using System.Net;
using Host=Seamless.Transport.Kestrel.KestrelHttpHost;

namespace Seamless.Database.Gateway.Tests.Transport;

public sealed class KestrelHttpHost{
    [Fact,Trait("Category","Integration")]
    public async Task StopWaitsForRequestFinallyBeyondTheTransportGracePeriod(){
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var ended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host=new Host(new(IPAddress.Loopback,0),async _=>{entered.TrySetResult();try{await release.Task;}finally{ended.TrySetResult();}});await host.StartAsync();using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(60)};var request=client.GetAsync($"http://127.0.0.1:{host.Port}/");await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));var stopping=host.DisposeAsync().AsTask();
        try{var winner=await Task.WhenAny(stopping,Task.Delay(TimeSpan.FromSeconds(35)));Assert.NotSame(stopping,winner);Assert.False(ended.Task.IsCompleted);}
        finally{release.TrySetResult();await stopping.WaitAsync(TimeSpan.FromSeconds(10));await ended.Task.WaitAsync(TimeSpan.FromSeconds(10));try{using var response=await request;}catch(HttpRequestException){}catch(OperationCanceledException){}}
    }
}
