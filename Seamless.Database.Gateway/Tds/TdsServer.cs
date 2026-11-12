using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Connections;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Security;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tds;

public sealed record TdsGatewayAuthorization(X509Certificate2 Certificate,HttpGatewayAuthorization Policy,
    Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> SessionFactory);

public sealed class TdsServer:IDisposable{
    private readonly int port;
    private IDatabaseAdapter adapter=>this.sqlEngine.Databases.Resolve(this.sqlEngine.Databases.DefaultDatabaseName).Adapter;
    private readonly SqlExecutionEngine sqlEngine;
    private readonly TdsRequestLog? requestLog;
    private readonly ConnectionQuota? connectionQuota;
    private readonly ConnectionQuota? sharedConnections;
    private readonly TimeSpan? loginTimeout;
    private readonly TimeSpan? packetWriteTimeout;
    private readonly TimeSpan? incompleteMessageTimeout;
    private readonly FrameBufferQuota? receiveBuffers;
    private readonly FrameBufferQuota? bufferedRequests;
    private readonly FrameBufferQuota? pendingWrites;
    private X509Certificate2? certificate;
    private TdsProtectedAccess? protection;
    private ConnectionQuota? handshakes;
    private ConnectionQuota? sharedHandshakes;
    private KestrelTcpHost? listener;
    private KestrelTcpHost? ipv6Listener;
    private CancellationTokenSource? cts;
    private long nextSessionId;
    public TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,TdsRequestLog? requestLog=null):this(port,adapter,sqlEngine,requestLog,null){}
    public TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,TdsRequestLog? requestLog,int? maximumConnections):this(port,adapter,sqlEngine,requestLog,maximumConnections,null){}
    public TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,TdsRequestLog? requestLog,int? maximumConnections,TimeSpan? loginTimeout):this(port,adapter,sqlEngine,requestLog,maximumConnections,loginTimeout,null){}
    public TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,TdsRequestLog? requestLog,int? maximumConnections,TimeSpan? loginTimeout,TimeSpan? packetWriteTimeout):this(port,adapter,sqlEngine,requestLog,maximumConnections,loginTimeout,packetWriteTimeout,null){}
    public TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,TdsRequestLog? requestLog,int? maximumConnections,TimeSpan? loginTimeout,TimeSpan? packetWriteTimeout,TimeSpan? incompleteMessageTimeout):this(port,adapter,sqlEngine,requestLog,maximumConnections,loginTimeout,packetWriteTimeout,incompleteMessageTimeout,null){}
    internal TdsServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,TdsRequestLog? requestLog,int? maximumConnections,TimeSpan? loginTimeout,TimeSpan? packetWriteTimeout,TimeSpan? incompleteMessageTimeout,FrameBufferQuota? receiveBuffers,FrameBufferQuota? bufferedRequests=null,FrameBufferQuota? pendingWrites=null,ConnectionQuota? sharedConnections=null){
        this.port=port;this.sqlEngine=sqlEngine;this.requestLog=requestLog;
        if(maximumConnections is{} limit){this.connectionQuota=new(limit);}
        this.sharedConnections=sharedConnections;
        if(loginTimeout is{} timeout&&(timeout<=TimeSpan.Zero||timeout.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(loginTimeout));}
        this.loginTimeout=loginTimeout;
        if(packetWriteTimeout is{} output&&(output<=TimeSpan.Zero||output.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(packetWriteTimeout));}
        this.packetWriteTimeout=packetWriteTimeout;
        if(incompleteMessageTimeout is{} input&&(input<=TimeSpan.Zero||input.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(incompleteMessageTimeout));}
        this.incompleteMessageTimeout=incompleteMessageTimeout;
        this.receiveBuffers=receiveBuffers;
        this.bufferedRequests=bufferedRequests;
        this.pendingWrites=pendingWrites;
    }
    public int Port=>this.listener?.Port??this.port;

    /// <summary>明示アクセストークンを使うループバックTDS 8.0。ホストが証明書と共通認可の寿命を管理する。</summary>
    public static TdsServer CreateAuthenticated(int port,SqlExecutionEngine sqlEngine,X509Certificate2 certificate,HttpGatewayAuthorization authorization,
        Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> sessionFactory)
        =>CreateAuthenticated(new(){TdsPort=port},sqlEngine,new(certificate,authorization,sessionFactory),null,null,null,null,null);

    internal static TdsServer CreateAuthenticated(GatewayServerOptions options,SqlExecutionEngine sqlEngine,TdsGatewayAuthorization authorization,
        FrameBufferQuota? receiveBuffers,FrameBufferQuota? bufferedRequests,FrameBufferQuota? pendingWrites,ConnectionQuota? connections,ConnectionQuota? sharedHandshakes){
        ArgumentNullException.ThrowIfNull(authorization.Policy);
        ArgumentNullException.ThrowIfNull(authorization.SessionFactory);
        if(authorization.Certificate?.HasPrivateKey!=true){throw new ArgumentException("Authenticated TDS requires a TLS certificate with a private key.",nameof(authorization));}
        var adapter=sqlEngine.Databases.Resolve(sqlEngine.Databases.DefaultDatabaseName).Adapter;
        var timeout=TimeSpan.FromSeconds(15);
        return new(options.TdsPort,adapter,sqlEngine,null,options.TdsMaximumConnections??128,options.TdsLoginTimeout??timeout,
            options.TdsPacketWriteTimeout??timeout,options.TdsIncompleteMessageTimeout??timeout,
            receiveBuffers??new(32*1024*1024),bufferedRequests??new(32*1024*1024),pendingWrites??new(32*1024*1024),connections){
            certificate=authorization.Certificate,protection=new(authorization.Policy,authorization.SessionFactory),handshakes=new(32),
            sharedHandshakes=sharedHandshakes
        };
    }

    public void Start(){
        if(this.listener is not null){return;}
        this.sqlEngine.Databases.GatewayTransactions?.Recover();
        var cancellation=new CancellationTokenSource();this.cts=cancellation;
        var ipv4=this.CreateListener(IPAddress.Loopback,this.port);KestrelTcpHost? ipv6=null;
        try{
            ipv4.StartAsync().GetAwaiter().GetResult();
            if(Socket.OSSupportsIPv6){ipv6=this.CreateListener(IPAddress.IPv6Loopback,ipv4.Port);ipv6.StartAsync().GetAwaiter().GetResult();}
            this.listener=ipv4;this.ipv6Listener=ipv6;
        }catch{cancellation.Cancel();if(ipv6 is not null){ipv6.DisposeAsync().AsTask().GetAwaiter().GetResult();}ipv4.DisposeAsync().AsTask().GetAwaiter().GetResult();cancellation.Dispose();this.cts=null;throw;}
    }
    public async Task StopAsync(){
        if(this.listener is null){return;}
        this.cts!.Cancel();
        try{await this.listener.DisposeAsync();}finally{try{if(this.ipv6Listener is not null){await this.ipv6Listener.DisposeAsync();}}finally{this.listener=null;this.ipv6Listener=null;this.cts.Dispose();this.cts=null;}}
    }
    public void Dispose()=>this.StopAsync().GetAwaiter().GetResult();
    private KestrelTcpHost CreateListener(IPAddress address,int port)=>new(new(address,port),this.HandleAsync,this.certificate,quota:this.connectionQuota,backlog:1024){
        SharedConnectionQuota=this.sharedConnections,TlsHandshakeQuota=this.handshakes,SharedTlsHandshakeQuota=this.sharedHandshakes
    };
    private async Task HandleAsync(ConnectionContext connection){
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(this.cts!.Token,connection.ConnectionClosed);
        try{await new TdsSession(Interlocked.Increment(ref this.nextSessionId),this.adapter,this.sqlEngine,this.requestLog){Protection=this.protection}.ProcessConnectionAsync(connection.Transport.Input,connection.Transport.Output,lifetime.Token,this.loginTimeout,this.packetWriteTimeout,this.incompleteMessageTimeout,this.receiveBuffers,this.bufferedRequests,this.pendingWrites);}
        catch(TdsPacketWriter.WriteAborted){connection.Abort();}
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}catch(IOException){}catch(SocketException){}
    }
}
