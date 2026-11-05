using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Seamless.Transport.Kestrel;

/// <summary>独自TCPプロトコル用のサーバー基盤。認証・要求・取引は呼び出し元が所有する。</summary>
public sealed class KestrelTcpHost:IAsyncDisposable{
    private readonly IHost host;
    private readonly CancellationTokenSource closing=new();
    private readonly ApplicationLifetime applications=new();
    private bool started;
    private bool disposed;
    public int Port{get;private set;}
    public KestrelTcpHost(IPEndPoint endpoint,Func<ConnectionContext,Task> connection,X509Certificate2? certificate=null,ConnectionQuota? quota=null,Action<Exception>? failed=null,int backlog=int.MaxValue,long readBufferBytes=65536,long writeBufferBytes=65536){
        ArgumentNullException.ThrowIfNull(endpoint);ArgumentNullException.ThrowIfNull(connection);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backlog);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(readBufferBytes);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(writeBufferBytes);
        this.Port=endpoint.Port;
        this.host=new HostBuilder().ConfigureLogging(logging=>logging.ClearProviders()).ConfigureWebHost(web=>{
            web.ConfigureServices(services=>services.AddSingleton(new Handler(connection,this.applications)));
            web.UseKestrel(options=>{
                if(quota is not null){options.Limits.MaxConcurrentConnections=quota.Maximum;}
                options.Listen(endpoint,listen=>{
                    listen.Protocols=HttpProtocols.None;
                    listen.Use(next=>async context=>{using var closing=this.closing.Token.Register(()=>context.Abort());IDisposable? permit=null;try{if(quota is not null){permit=quota.TryEnter();if(permit is null){context.Abort();return;}}await next(context).ConfigureAwait(false);}catch(Exception error){failed?.Invoke(error);}finally{permit?.Dispose();}});
                    if(certificate is not null){listen.UseHttps(certificate,https=>https.HandshakeTimeout=TimeSpan.FromSeconds(15));}
                    listen.UseConnectionHandler<Handler>();
                });
            }).UseSockets(options=>{options.NoDelay=true;options.Backlog=backlog;options.MaxReadBufferSize=readBufferBytes;options.MaxWriteBufferSize=writeBufferBytes;});
            web.Configure(app=>app.Run(_=>Task.CompletedTask));
        }).Build();
    }
    public async Task StartAsync(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.started){throw new InvalidOperationException("The listener has already started.");}
        try{await this.host.StartAsync(cancellationToken).ConfigureAwait(false);}catch(IOException error){
            for(Exception? cause=error.InnerException;cause is not null;cause=cause.InnerException){if(cause is System.Net.Sockets.SocketException){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cause).Throw();}}
            throw;
        }
        var addresses=this.host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;var address=addresses?.SingleOrDefault()??throw new IOException("The server did not publish its bound endpoint.");this.Port=new Uri(address).Port;this.started=true;
    }
    public async ValueTask DisposeAsync(){if(this.disposed){return;}this.disposed=true;var pending=this.applications.Stop();this.closing.Cancel();try{if(this.started){await this.host.StopAsync().ConfigureAwait(false);}}finally{try{await pending.ConfigureAwait(false);}finally{this.host.Dispose();this.closing.Dispose();}}}
    private sealed class Handler(Func<ConnectionContext,Task> connection,ApplicationLifetime applications):ConnectionHandler{
        public override async Task OnConnectedAsync(ConnectionContext context){if(!applications.TryEnter()){context.Abort();return;}try{await connection(context).ConfigureAwait(false);}finally{applications.Exit();}}
    }
}
