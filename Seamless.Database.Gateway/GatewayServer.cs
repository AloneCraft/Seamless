using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Client;
using Seamless.Transport.Kestrel;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Net;
using System.Collections.Frozen;

namespace Seamless.Database.Gateway;

public sealed class GatewayServerOptions{
    public int TdsPort{get;set;}=TdsConstants.DefaultPort;
    public bool TdsEnabled{get;set;}=true;
    [JsonIgnore]public TdsGatewayAuthorization? TdsAuthorization{get;set;}
    public int? TdsMaximumConnections{get;set;}
    public TimeSpan? TdsLoginTimeout{get;set;}
    public TimeSpan? TdsPacketWriteTimeout{get;set;}
    public TimeSpan? TdsIncompleteMessageTimeout{get;set;}
    public int HttpPort{get;set;}=8088;
    public bool HttpEnabled{get;set;}=true;
    public HttpServerOptions Http{get;set;}=new();
    public bool NativeEnabled{get;set;}=true;
    public int NativePort{get;set;}=14335;
    public NativeConnectionPolicy? NativeRecommendedConnectionPolicy{get;set;}
    public Dictionary<string,NativeConnectionPolicy>? NativeConnectionPolicyProfiles{get;set;}
    public IPAddress NativeListenAddress{get;set;}=IPAddress.Loopback;
    [JsonIgnore]public X509Certificate2? NativeCertificate{get;set;}
    [JsonIgnore]public NativeGatewayAuthorization? NativeAuthorization{get;set;}
    [JsonIgnore]public NativeServerLimits? NativeAuthenticatedLimits{get;set;}
    public int? NativeMaximumConnections{get;set;}
    public int? NativeMaximumConcurrentRequests{get;set;}
    public TimeSpan? NativeIncompleteFrameTimeout{get;set;}
    public TimeSpan? NativeFrameWriteTimeout{get;set;}
    public long? NativeMaximumReceiveBufferBytes{get;set;}
    public int? MaximumConnections{get;set;}
    public int? MaximumConcurrentTlsHandshakes{get;set;}
    public long? MaximumReceiveBufferBytes{get;set;}
    public long? MaximumBufferedRequestBytes{get;set;}
    public long? MaximumPendingWriteBytes{get;set;}
    // 指定するとTDSで受けた要求(SQL本文・引数の名前と型・成否)を1行JSONで追記する。互換対応の調査用で、既定は無効。
    public string? RequestLogPath{get;set;}
    /// <summary>宣言した認証構成の事前検査。本人確認は行わず、サーバーも実認証器で再検査する。</summary>
    public void ValidateNativeTransport(bool authenticated){
        if(this.NativeConnectionPolicyProfiles is{} profiles){
            if(profiles.Count>128){throw new ArgumentException("At most 128 native connection policy profiles are supported.",nameof(this.NativeConnectionPolicyProfiles));}
            foreach(var (name,policy) in profiles){
                NativeConnectionPolicyWire.ValidateProfile(name);
                if(policy is null){throw new ArgumentException("Each native connection profile requires a policy.",nameof(this.NativeConnectionPolicyProfiles));}
            }
        }
        if(this.MaximumConnections is{} maximum){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum,nameof(this.MaximumConnections));}
        if(this.MaximumConcurrentTlsHandshakes is{} handshakes){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(handshakes,nameof(this.MaximumConcurrentTlsHandshakes));}
        if(!this.NativeEnabled){
            if(authenticated||this.NativeAuthenticatedLimits is not null){throw new ArgumentException("Native authorization requires an enabled native endpoint.",nameof(this.NativeEnabled));}
            return;
        }
        ArgumentNullException.ThrowIfNull(this.NativeListenAddress);
        ArgumentOutOfRangeException.ThrowIfNegative(this.NativePort);ArgumentOutOfRangeException.ThrowIfGreaterThan(this.NativePort,65535);
        if(!this.NativeListenAddress.Equals(IPAddress.Loopback)&&!authenticated){throw new ArgumentException("NativeListenAddress requires authenticated native TLS.",nameof(this.NativeListenAddress));}
        if(!authenticated){if(this.NativeAuthenticatedLimits is not null){throw new ArgumentException("NativeAuthenticatedLimits requires NativeAuthorization.");}return;}
        var limits=this.NativeAuthenticatedLimits??throw new ArgumentException("Authenticated native RPC requires explicit finite limits.");limits.Validate();
        if(this.NativeCertificate?.HasPrivateKey!=true){throw new ArgumentException("Authenticated native RPC requires a TLS certificate with a private key.");}
        if(this.NativeMaximumConnections is not null||this.NativeMaximumConcurrentRequests is not null||this.NativeIncompleteFrameTimeout is not null||this.NativeFrameWriteTimeout is not null||this.NativeMaximumReceiveBufferBytes is not null){throw new ArgumentException("Choose NativeAuthenticatedLimits instead of the legacy native limits.");}
        if(this.MaximumReceiveBufferBytes is <=0||this.MaximumBufferedRequestBytes is <=0||this.MaximumPendingWriteBytes is <=0){throw new ArgumentException("Shared buffer limits must be positive.");}
        if(this.MaximumReceiveBufferBytes>limits.MaximumReceiveBufferBytes||this.MaximumBufferedRequestBytes>limits.MaximumBufferedRequestBytes||this.MaximumPendingWriteBytes>limits.MaximumPendingWriteBytes){throw new ArgumentException("Shared buffer limits must not exceed the authenticated native limits.");}
    }
}

public sealed class GatewayServer:IDisposable{
    private readonly GatewayServerOptions options;
    private readonly SqlExecutionEngine sqlEngine;
    private readonly TdsServer? tdsServer;
    private readonly HttpServer? httpServer;
    private readonly NativeQueryServer? nativeServer;
    private readonly TdsRequestLog? requestLog;
    private readonly ConnectionQuota? sharedConnections;
    private readonly ConnectionQuota? sharedHandshakes;
    private readonly FrameBufferQuota? sharedPendingWrites;
    private bool isStarted;

    public GatewayServer(IDatabaseAdapter adapter,GatewayServerOptions? options=null):this(CreateRegistry(adapter),options){}
    private static DatabaseRegistry CreateRegistry(IDatabaseAdapter adapter){var registry=new DatabaseRegistry();registry.Register(adapter.DatabaseName,adapter);return registry;}
    public GatewayServer(DatabaseRegistry databases,GatewayServerOptions? options=null):this(databases,options,null){}
    public GatewayServer(DatabaseRegistry databases,GatewayServerOptions? options,HttpGatewayAuthorization? httpAuthorization){
        var adapter=databases.Resolve(databases.DefaultDatabaseName).Adapter;
        ArgumentNullException.ThrowIfNull(adapter);
        this.options=options??new GatewayServerOptions();
        var nativeAuthorization=this.options.NativeAuthorization;
        var tdsAuthorization=this.options.TdsAuthorization;
        this.options.ValidateNativeTransport(nativeAuthorization is not null);
        var nativeProfiles=this.options.NativeConnectionPolicyProfiles?.ToFrozenDictionary(StringComparer.Ordinal);
        if(tdsAuthorization is not null&&(!this.options.TdsEnabled||this.options.RequestLogPath is not null)){
            throw new ArgumentException("Authenticated TDS requires an enabled endpoint and prohibits request logging.",nameof(options));
        }
        var unprotectedTds=this.options.TdsEnabled&&tdsAuthorization is null;
        if(this.options.HttpEnabled&&httpAuthorization is not null&&
            (unprotectedTds||this.options.NativeEnabled&&nativeAuthorization is null)){
            throw new ArgumentException("Authenticated HTTP cannot expose an unauthenticated TDS or native endpoint.",nameof(options));
        }
        NativeServerLimits? nativeLimits=null;
        if(nativeAuthorization is not null){
            if(unprotectedTds||this.options.HttpEnabled&&httpAuthorization is null){throw new ArgumentException("Authenticated native RPC cannot expose an unauthenticated TDS or HTTP endpoint.",nameof(options));}
            nativeLimits=this.options.NativeAuthenticatedLimits??throw new ArgumentException("Authenticated native RPC requires explicit finite limits.",nameof(options));
        }
        if(tdsAuthorization is not null&&(this.options.HttpEnabled&&httpAuthorization is null||this.options.NativeEnabled&&nativeAuthorization is null)){
            throw new ArgumentException("Authenticated TDS cannot expose an unauthenticated HTTP or native endpoint.",nameof(options));
        }
        FrameBufferQuota? receiveBuffers=null;
        FrameBufferQuota? bufferedRequests=null;
        FrameBufferQuota? pendingWrites=null;
        var connections=this.sharedConnections=this.options.MaximumConnections is{} maximum?new(maximum):null;
        var handshakes=this.sharedHandshakes=this.options.MaximumConcurrentTlsHandshakes is{} handshakeLimit?new(handshakeLimit):null;
        if(this.options.MaximumBufferedRequestBytes is{} requestBytes){bufferedRequests=new(requestBytes);}
        if(this.options.MaximumPendingWriteBytes is{} writeBytes){pendingWrites=this.sharedPendingWrites=new(writeBytes);}
        if(this.options.MaximumReceiveBufferBytes is{} sharedBytes){
            if(this.options.NativeMaximumReceiveBufferBytes.HasValue){throw new ArgumentException("Choose the shared receive buffer limit or the native-only limit.",nameof(options));}
            receiveBuffers=new(sharedBytes);
        }

        this.sqlEngine=new SqlExecutionEngine(databases);
        this.requestLog=!this.options.TdsEnabled||this.options.RequestLogPath is null?null:new TdsRequestLog(this.options.RequestLogPath);
        if(tdsAuthorization is not null){
            this.tdsServer=TdsServer.CreateAuthenticated(this.options,this.sqlEngine,tdsAuthorization,receiveBuffers,bufferedRequests,pendingWrites,connections,handshakes);
        }else if(this.options.TdsEnabled){
            this.tdsServer=new TdsServer(this.options.TdsPort,adapter,this.sqlEngine,this.requestLog,this.options.TdsMaximumConnections,this.options.TdsLoginTimeout,this.options.TdsPacketWriteTimeout,this.options.TdsIncompleteMessageTimeout,receiveBuffers,bufferedRequests,pendingWrites,connections);
        }
        if(this.options.HttpEnabled){this.httpServer=new HttpServer(this.options.HttpPort,adapter,this.sqlEngine,this.options.Http,httpAuthorization,pendingWrites,connections,bufferedRequests,handshakes);}
        var nativeBuffers=receiveBuffers;
        if(this.options.NativeMaximumReceiveBufferBytes is{} nativeBytes){nativeBuffers=new(nativeBytes);}
        if(!this.options.NativeEnabled){return;}
        if(nativeLimits is null){
            this.nativeServer=new NativeQueryServer(databases,this.options.NativePort,this.options.NativeMaximumConnections,this.options.NativeMaximumConcurrentRequests,this.options.NativeIncompleteFrameTimeout,this.options.NativeFrameWriteTimeout,nativeBuffers,bufferedRequests,pendingWrites,this.options.NativeCertificate,listenAddress:this.options.NativeListenAddress,sharedConnections:connections,sharedHandshakes:handshakes){
                RecommendedConnectionPolicy=this.options.NativeRecommendedConnectionPolicy,ConnectionPolicyProfiles=nativeProfiles
            };
        }else{
            this.nativeServer=new NativeQueryServer(databases,this.options.NativePort,nativeLimits.MaximumConnections,nativeLimits.MaximumConcurrentRequests,nativeLimits.IncompleteFrameTimeout,nativeLimits.FrameWriteTimeout,nativeBuffers??new(nativeLimits.MaximumReceiveBufferBytes),bufferedRequests??new(nativeLimits.MaximumBufferedRequestBytes),pendingWrites??new(nativeLimits.MaximumPendingWriteBytes),this.options.NativeCertificate,nativeAuthorization,this.options.NativeListenAddress,nativeLimits.MaximumConcurrentHandshakes,connections,handshakes){
                RecommendedConnectionPolicy=this.options.NativeRecommendedConnectionPolicy,ConnectionPolicyProfiles=nativeProfiles
            };
        }
    }

    public int TdsPort=>this.tdsServer?.Port??0;
    public int HttpPort=>this.httpServer?.Port??0;
    public int NativePort=>this.nativeServer?.Port??0;
    public NativeQueryServer Native=>this.nativeServer??throw new InvalidOperationException("The native endpoint is disabled.");
    internal int? SharedConnectionCount=>this.sharedConnections?.Count;
    internal int? SharedTlsHandshakeCount=>this.sharedHandshakes?.Count;
    internal long SharedPendingWriteBytes=>this.sharedPendingWrites?.UsedBytes??0;

    public void Start(){
        if(this.isStarted){
            return;
        }else{
        }

        this.sqlEngine.Databases.GatewayTransactions?.Recover();this.isStarted=true;
        try{this.tdsServer?.Start();this.httpServer?.Start();this.nativeServer?.Start();}
        catch{this.StopAsync().GetAwaiter().GetResult();throw;}
    }

    public async Task StopAsync(){
        if(!this.isStarted){
            return;
        }else{
        }

        if(this.nativeServer is not null){await this.nativeServer.StopAsync();}
        if(this.tdsServer is not null){await this.tdsServer.StopAsync();}
        if(this.httpServer is not null){await this.httpServer.StopAsync();}
        this.isStarted=false;
    }

    public void Dispose(){
        try{this.StopAsync().GetAwaiter().GetResult();}
        finally{this.requestLog?.Dispose();}
    }
}
