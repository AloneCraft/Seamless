using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Seamless.Transport.Kestrel;

/// <summary>HTTPの解析と接続寿命をKestrelへ委譲する。要求の認証・実行は呼び出し元が所有する。</summary>
public sealed class KestrelHttpHost:IAsyncDisposable{
    private readonly IHost host;
    private readonly ApplicationLifetime applications=new();
    private bool started;
    private bool disposed;
    public int Port{get;private set;}
    public KestrelHttpHost(IPEndPoint endpoint,RequestDelegate request,X509Certificate2? certificate=null){
        ArgumentNullException.ThrowIfNull(endpoint);ArgumentNullException.ThrowIfNull(request);this.Port=endpoint.Port;
        this.host=new HostBuilder().ConfigureLogging(logging=>logging.ClearProviders()).ConfigureWebHost(web=>{
            web.UseKestrel(options=>{
                // 旧HTTP APIには本文長の上限がなかった。新たな契約上限は別途設定・検証する。
                options.Limits.MaxRequestBodySize=null;
                options.Listen(endpoint,listen=>{if(certificate is not null){listen.UseHttps(certificate,https=>https.HandshakeTimeout=TimeSpan.FromSeconds(15));}});
            }).UseSockets(options=>{options.NoDelay=true;options.MaxReadBufferSize=65536;options.MaxWriteBufferSize=65536;});
            web.Configure(app=>app.Run(async context=>{if(!this.applications.TryEnter()){context.Abort();return;}try{await request(context).ConfigureAwait(false);}finally{this.applications.Exit();}}));
        }).Build();
    }
    public async Task StartAsync(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.started){throw new InvalidOperationException("The listener has already started.");}
        await this.host.StartAsync(cancellationToken).ConfigureAwait(false);this.Port=new Uri(this.host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()).Port;this.started=true;
    }
    public async ValueTask DisposeAsync(){if(this.disposed){return;}this.disposed=true;var pending=this.applications.Stop();try{if(this.started){await this.host.StopAsync().ConfigureAwait(false);}}finally{try{await pending.ConfigureAwait(false);}finally{this.host.Dispose();}}}
}
