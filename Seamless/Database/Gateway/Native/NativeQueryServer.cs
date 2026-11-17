using System.Net;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Execution;
using Seamless.Linq;
using Microsoft.AspNetCore.Connections;
using Seamless.Transport.Kestrel;
using Seamless.Authentication;
using Seamless.Security;

namespace Seamless.Database.Gateway.Native;

/// <summary>管理されたビルド成果物だけを読み込む型付きクエリ入口。</summary>
public sealed partial class NativeQueryServer:IDisposable{
    // バージョン付きの専用フレームからだけ生成する制御要求。
    private static readonly NativeQueryRequest DiscoverConnectionPolicy=new("","","","",[]);
    private sealed record ConnectionPolicyDiscovery(string Profile);
    private sealed record QueuedRequest(NativeQueryRequest Request,long Sequence);
    private sealed record CancellationBarrier(long Sequence);
    internal NativeConnectionPolicy? RecommendedConnectionPolicy{get;init;}
    internal IReadOnlyDictionary<string,NativeConnectionPolicy>? ConnectionPolicyProfiles{get;init;}
    private readonly DatabaseRegistry databases;
    private readonly Dictionary<string,(NativeModuleManifest Manifest,INativeQueryModule Module)> modules=new(StringComparer.Ordinal);
    private readonly int port;
    private readonly IPAddress listenAddress;
    private readonly X509Certificate2? certificate;
    private readonly NativeGatewayAuthorization? authorization;
    private readonly ConnectionQuota? connectionQuota;
    private readonly ConnectionQuota? sharedConnections;
    private readonly ConnectionQuota? sharedHandshakes;
    private readonly ConnectionQuota? handshakeQuota;
    private readonly ConnectionQuota? requestQuota;
    private readonly TimeSpan? incompleteFrameTimeout;
    private readonly TimeSpan? frameWriteTimeout;
    private readonly FrameBufferQuota? receiveBuffers;
    private readonly FrameBufferQuota? bufferedRequests;
    private readonly FrameBufferQuota? pendingWrites;
    private KestrelTcpHost? listener;
    private CancellationTokenSource? stop;
    private long nextConnection;
    public NativeQueryServer(DatabaseRegistry databases,int port=14335):this(databases,port,null){}
    public NativeQueryServer(DatabaseRegistry databases,int port,int? maximumConnections):this(databases,port,maximumConnections,null){}
    public NativeQueryServer(DatabaseRegistry databases,int port,int? maximumConnections,int? maximumConcurrentRequests):this(databases,port,maximumConnections,maximumConcurrentRequests,null){}
    public NativeQueryServer(DatabaseRegistry databases,int port,int? maximumConnections,int? maximumConcurrentRequests,TimeSpan? incompleteFrameTimeout):this(databases,port,maximumConnections,maximumConcurrentRequests,incompleteFrameTimeout,null){}
    public NativeQueryServer(DatabaseRegistry databases,int port,int? maximumConnections,int? maximumConcurrentRequests,TimeSpan? incompleteFrameTimeout,TimeSpan? frameWriteTimeout):this(databases,port,maximumConnections,maximumConcurrentRequests,incompleteFrameTimeout,frameWriteTimeout,(FrameBufferQuota?)null){}
    public NativeQueryServer(DatabaseRegistry databases,int port,int? maximumConnections,int? maximumConcurrentRequests,TimeSpan? incompleteFrameTimeout,TimeSpan? frameWriteTimeout,long? maximumReceiveBufferBytes):this(databases,port,maximumConnections,maximumConcurrentRequests,incompleteFrameTimeout,frameWriteTimeout,maximumReceiveBufferBytes is{} bytes?new FrameBufferQuota(bytes):null){}
    /// <summary>既存のネイティブフレームをTLS上で扱うLoopback入口。証明書の所有権は呼出し元に残す。</summary>
    public static NativeQueryServer CreateTls(DatabaseRegistry databases,X509Certificate2 certificate,int port=14335,int? maximumConnections=null,int? maximumConcurrentRequests=null,TimeSpan? incompleteFrameTimeout=null,TimeSpan? frameWriteTimeout=null,long? maximumReceiveBufferBytes=null){
        ArgumentNullException.ThrowIfNull(certificate);
        return new(databases,port,maximumConnections,maximumConcurrentRequests,incompleteFrameTimeout,frameWriteTimeout,maximumReceiveBufferBytes is{} bytes?new FrameBufferQuota(bytes):null,certificate:certificate);
    }
    public static NativeQueryServer CreateAuthenticated(DatabaseRegistry databases,X509Certificate2 certificate,NativeGatewayAuthorization authorization,NativeServerLimits limits,int port=14335){
        return CreateAuthenticated(databases,new IPEndPoint(IPAddress.Loopback,port),certificate,authorization,limits);
    }
    /// <summary>明示IPの認証付きTLS入口。endpointは複製し、証明書の所有権は呼出し元に残す。</summary>
    public static NativeQueryServer CreateAuthenticated(DatabaseRegistry databases,IPEndPoint endpoint,X509Certificate2 certificate,NativeGatewayAuthorization authorization,NativeServerLimits limits){
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(certificate);ArgumentNullException.ThrowIfNull(authorization);ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        return new(databases,endpoint.Port,limits.MaximumConnections,limits.MaximumConcurrentRequests,limits.IncompleteFrameTimeout,limits.FrameWriteTimeout,new FrameBufferQuota(limits.MaximumReceiveBufferBytes),new FrameBufferQuota(limits.MaximumBufferedRequestBytes),new FrameBufferQuota(limits.MaximumPendingWriteBytes),certificate,authorization,endpoint.Address,limits.MaximumConcurrentHandshakes);
    }
    internal NativeQueryServer(DatabaseRegistry databases,int port,int? maximumConnections,int? maximumConcurrentRequests,TimeSpan? incompleteFrameTimeout,TimeSpan? frameWriteTimeout,FrameBufferQuota? receiveBuffers,FrameBufferQuota? bufferedRequests=null,FrameBufferQuota? pendingWrites=null,X509Certificate2? certificate=null,NativeGatewayAuthorization? authorization=null,IPAddress? listenAddress=null,int? maximumConcurrentHandshakes=null,ConnectionQuota? sharedConnections=null,ConnectionQuota? sharedHandshakes=null){
        ArgumentOutOfRangeException.ThrowIfNegative(port);ArgumentOutOfRangeException.ThrowIfGreaterThan(port,65535);
        this.listenAddress=IPAddress.Parse((listenAddress??IPAddress.Loopback).ToString());
        if(certificate is not null&&!certificate.HasPrivateKey){throw new ArgumentException("The native TLS certificate must have a private key.",nameof(certificate));}
        if(!this.listenAddress.Equals(IPAddress.Loopback)&&(certificate is null||authorization is null||maximumConnections is not >0||maximumConcurrentRequests is not >0||maximumConcurrentHandshakes is not >0
            ||incompleteFrameTimeout is null||frameWriteTimeout is null||receiveBuffers is null||bufferedRequests is null||pendingWrites is null)){
            throw new ArgumentException("A non-default native listen address requires authenticated TLS and finite resource limits.",nameof(listenAddress));
        }
        this.certificate=certificate;
        this.authorization=authorization;
        this.databases=databases;this.port=port;
        if(maximumConnections is{} limit){this.connectionQuota=new(limit);}
        this.sharedConnections=sharedConnections;
        this.sharedHandshakes=sharedHandshakes;
        if(maximumConcurrentHandshakes is{} handshakeLimit){this.handshakeQuota=new(handshakeLimit);}
        if(maximumConcurrentRequests is{} requestLimit){this.requestQuota=new(requestLimit);}
        if(incompleteFrameTimeout is{} timeout&&(timeout<=TimeSpan.Zero||timeout.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(incompleteFrameTimeout));}
        this.incompleteFrameTimeout=incompleteFrameTimeout;
        if(frameWriteTimeout is{} output&&(output<=TimeSpan.Zero||output.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(frameWriteTimeout));}
        this.frameWriteTimeout=frameWriteTimeout;
        this.receiveBuffers=receiveBuffers;
        this.bufferedRequests=bufferedRequests;
        this.pendingWrites=pendingWrites;
    }
    public int Port=>this.listener?.Port??this.port;
    public IPAddress ListenAddress=>IPAddress.Parse(this.listenAddress.ToString());
    public void Register(NativeModuleManifest manifest,INativeQueryModule module,string productVersion){
        this.databases.GatewayTransactions?.Recover();
        if(this.databases.GatewayTransactions?.TypedDdlEnabled==true){CollectibleFormatterCache.Validate();}
        if(this.listener is not null||this.pipeAccept is not null){throw new InvalidOperationException("Register query modules before starting the server.");}
        var runtimeVersion=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??throw new InvalidDataException("Runtime package version is unavailable.");
        if(runtimeVersion!=productVersion){throw new InvalidDataException("The supplied product version does not match the installed runtime package.");}
        if(manifest.ProductVersion!=productVersion){throw new InvalidDataException("Query module product version does not match the server.");}
        foreach(var query in manifest.Queries){var database=this.databases.Resolve(query.Database);if(database.SchemaFingerprint!=query.SchemaFingerprint){throw new InvalidDataException($"Query module schema does not match '{query.Database}'.");}ValidateRecordDefinition(module,query);}
        if(!this.modules.TryAdd(manifest.ModuleId,(manifest,module))){throw new InvalidDataException("Duplicate native module ID.");}
    }
    public void Load(string manifestPath,string productVersion){
        var root=Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var manifest=JsonSerializer.Deserialize<NativeModuleManifest>(File.ReadAllText(manifestPath))??throw new InvalidDataException("Query manifest is empty.");
        var assemblyPath=Path.GetFullPath(Path.Combine(root,manifest.AssemblyPath));
        if(!assemblyPath.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Query module path escapes its deployment directory.");}
        using(var input=File.OpenRead(assemblyPath)){var actual=Convert.ToHexString(SHA256.HashData(input));if(!actual.Equals(manifest.AssemblySha256,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Query module hash does not match its manifest.");}}
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var module=Activator.CreateInstance(assembly.GetType(manifest.EntryType,true)!) as INativeQueryModule??throw new InvalidDataException("Query module entry does not implement INativeQueryModule.");
        this.Register(manifest,module,productVersion);
    }
    public void Start(){
        if(this.listener is not null||this.pipeAccept is not null){return;}
        this.databases.GatewayTransactions?.Recover();
        if(this.PipeName is not null){
            var pipe=this.NewPipe(true);this.stop=new();this.pipeAccept=this.AcceptPipesAsync(pipe);return;
        }
        this.stop=new();var host=new KestrelTcpHost(new(this.listenAddress,this.port),this.AcceptAsync,certificate:this.certificate,quota:this.connectionQuota){TlsHandshakeQuota=this.handshakeQuota,SharedConnectionQuota=this.sharedConnections,SharedTlsHandshakeQuota=this.sharedHandshakes};
        try{host.StartAsync().GetAwaiter().GetResult();this.listener=host;}catch{host.DisposeAsync().AsTask().GetAwaiter().GetResult();this.stop.Dispose();this.stop=null;throw;}
    }
    public async Task StopAsync(){
        if(this.pipeAccept is not null){
            this.stop!.Cancel();
            try{try{await this.pipeAccept.ConfigureAwait(false);}finally{await Task.WhenAll(this.pipeConnections.Values).ConfigureAwait(false);}}
            finally{this.pipeAccept=null;this.stop.Dispose();this.stop=null;}return;
        }
        if(this.listener is null){return;}
        this.stop!.Cancel();try{await this.listener.DisposeAsync().ConfigureAwait(false);}finally{this.listener=null;this.stop.Dispose();this.stop=null;}
    }
    private async Task AcceptAsync(ConnectionContext connection){
        var stream=new LengthPrefixedPipe(connection.Transport,NativeFrame.MaximumBytes,false,this.incompleteFrameTimeout,this.frameWriteTimeout,this.receiveBuffers,this.pendingWrites);
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(this.stop!.Token,connection.ConnectionClosed);
        try{
            using var identity=this.authorization is null?null:await this.authorization.AuthenticateAsync(stream,lifetime.Token).ConfigureAwait(false);
            if(this.authorization is not null&&identity is null){return;}
            await this.ConnectionAsync(stream,lifetime.Token,Interlocked.Increment(ref this.nextConnection),identity).ConfigureAwait(false);
        }
        finally{if(stream.OutputAborted){connection.Abort();}}
    }
    private async Task ConnectionAsync(LengthPrefixedPipe stream,CancellationToken stop,long connectionId,OidcAuthorizationSession? identity,Action? verifyOwner=null){
        using(var lifetime=CancellationTokenSource.CreateLinkedTokenSource(stop)){
            using var authorization=identity is null?null:new NativeConnectionAuthorization(this.authorization!,identity,lifetime);
            var input=Channel.CreateBounded<(object Request,IDisposable? Reservation)>(new BoundedChannelOptions(1){SingleReader=true,SingleWriter=true});
            var requestGate=new object();long receivedSequence=0,activeSequence=0,cancelledSequence=0;CancellationTokenSource? activeCancellation=null;
            using var gatewaySession=new SessionContext(-connectionId,this.databases.DefaultDatabaseName){CancellationToken=lifetime.Token};
            var receive=ReceiveAsync();DatabaseTransaction? transaction=null;string? transactionDatabase=null;Guid transactionId=default;
            try{
                await foreach(var entry in input.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false)){
                    using var reservation=entry.Reservation;
                    lifetime.Token.ThrowIfCancellationRequested();
                    if(entry.Request is CancellationBarrier barrier){
                        var acknowledgement=new byte[8];BinaryPrimitives.WriteInt64LittleEndian(acknowledgement,barrier.Sequence);
                        await stream.WriteAsync(NativeFrame.Cancelled,acknowledgement,lifetime.Token).ConfigureAwait(false);continue;
                    }
                    if(entry.Request is string refreshToken){
                        using var refreshPermit=this.requestQuota?.TryEnter();
                        if(transaction is not null||gatewaySession.IsInTransaction||this.requestQuota is not null&&refreshPermit is null){
                            await stream.WriteAsync(NativeAuthenticationWire.Error,NativeAuthenticationWire.Failure(NativeAuthenticationError.Busy),lifetime.Token).ConfigureAwait(false);
                        }else{
                            await this.authorization!.ReauthenticateAsync(identity!,stream,refreshToken,lifetime.Token).ConfigureAwait(false);
                        }
                        continue;
                    }
                    var discovery=entry.Request as ConnectionPolicyDiscovery;
                    var queued=entry.Request as QueuedRequest;
                    var request=queued?.Request??DiscoverConnectionPolicy;
                    using var requestCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    var requestToken=requestCancellation.Token;
                    lock(requestGate){activeSequence=queued?.Sequence??0;activeCancellation=requestCancellation;if(activeSequence!=0&&cancelledSequence==activeSequence){requestCancellation.Cancel();cancelledSequence=0;}}
                    try{
                        requestToken.ThrowIfCancellationRequested();
                        var gatewayTransactionId=gatewaySession.IsInTransaction
                            ?authorization?.WireTransactionId??gatewaySession.Transaction?.Id??Guid.Empty
                            :Guid.Empty;
                        var quota=this.requestQuota;
                        if(request.ModuleId=="$transaction"){
                            var existing=transaction is not null&&transactionId==request.TransactionId||gatewayTransactionId!=Guid.Empty&&gatewayTransactionId==request.TransactionId;
                            // 開始済みの同一取引は枠不足でも確定・取消等を進める。従来の開発入口の制御も維持する。
                            if(authorization is null||existing&&(request.QueryId is "begin" or "commit" or "rollback" or "save" or "rollback-to")){quota=null;}
                        }
                        using var permit=quota?.TryEnter();
                        if(quota is not null&&permit is null){
                            const string busy="The native request limit has been reached.";
                            
                            throw new NativeQueryException(busy,isRecoverable:true);
                        }
                        if(ReferenceEquals(request,DiscoverConnectionPolicy)){
                            if(transaction is not null||gatewaySession.IsInTransaction){throw new NativeQueryException("A native transaction already owns the connection.",isRecoverable:true);}
                            using var lease=identity is null?null:await identity.BeginOperationAsync(lifetime.Token).ConfigureAwait(false);
                            using var cancellation=lease?.CancellationToken.Register(static state=>((CancellationTokenSource)state!).Cancel(),lifetime);
                            var policy=this.RecommendedConnectionPolicy;
                            if(discovery is not null&&this.ConnectionPolicyProfiles?.TryGetValue(discovery.Profile,out var profilePolicy)==true){policy=profilePolicy;}
                            await stream.WriteAsync(NativeConnectionPolicyWire.Recommendation,NativeConnectionPolicyWire.WriteRecommendation(policy),lifetime.Token).ConfigureAwait(false);
                            continue;
                        }
                        // 新規操作の所属再確認も要求枠内で行い、照会中に別の新規操作を積み増さない。
                        using var operation=authorization is null?null:await authorization.BeginAsync(request).ConfigureAwait(false);
                        var database=this.databases.Resolve(request.Database);
                        if(database.Adapter is not InProcessDatabaseAdapter adapter){throw new NotSupportedException("The database does not expose typed native operations.");}
                        
                        if(this.databases.GatewayTransactions is not null){
                            await this.ExecuteGatewayAsync(request,database,adapter,gatewaySession,stream,requestToken,lifetime.Token,authorization,operation?.Lease).ConfigureAwait(false);
                            await stream.WriteAsync(NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,lifetime.Token).ConfigureAwait(false);continue;
                        }
                        if(request.ModuleId=="$transaction"){
                            if(operation is not null&&request.QueryId=="outcome"){throw new AuthorizationException("UnsupportedProtectedOperation","Native transaction outcomes require a Gateway WAL.");}
                            if(database.IsReadOnly){throw new InvalidOperationException("The comparison database is read only.");}
                            if(request.QueryId=="begin"){
                                if(transaction is not null||request.TransactionId==Guid.Empty){throw new InvalidOperationException("Invalid or nested native transaction.");}
                                transaction=adapter.Database.BeginTransaction();transactionDatabase=database.Name;transactionId=request.TransactionId;
                            }else{
                                if(transaction is null||transactionId!=request.TransactionId||transactionDatabase!=database.Name){throw new InvalidOperationException("The native transaction is not active on this connection.");}
                                if(request.QueryId=="commit"){transaction.Commit();}else if(request.QueryId!="rollback"){throw new NotSupportedException("Unknown transaction operation.");}
                                transaction.Dispose();transaction=null;transactionDatabase=null;transactionId=default;
                            }
                        }else{
                            if(request.TransactionId!=transactionId||transaction is not null&&transactionDatabase!=database.Name){throw new InvalidOperationException("The native request transaction does not match the connection.");}
                            var resolved=this.ResolveQuery(request,database);
                            if(operation is not null){authorization!.DemandQuery(operation.Lease,request,resolved.Module,resolved.Definition);}
                            var definition=resolved.Definition;
                            if(database.IsReadOnly&&!definition.IsReadOnly){throw new NativeQueryException("The comparison database is read only.");}
                            using var recordResources=new ExecutionScope(new(){ParentBudget=adapter.Database.ExecutionResources.Budget,CancellationToken=requestToken});
                            IDisposable? read=null;DatabaseTransaction? atomic=null;string? point=null;var statementEnded=false;
                            try{
                                if(transaction is null){if(definition.IsReadOnly){read=adapter.Database.BeginReadSession();}else{atomic=adapter.Database.BeginTransaction();}}
                                else if(resolved.Module is INativeRecordQueryModule&&!definition.IsReadOnly){point="native/"+Guid.NewGuid().ToString("N");transaction.CreateSavepoint(point);}
                                var context=new NativeContext(adapter.Database,database.Name,requestToken,Budget:recordResources.Budget);
                                var rows=ExecuteRows(resolved.Module,definition,request.Arguments,context);
                                if(atomic is not null||point is not null){
                                    using var buffered=recordResources.Query(rows).ToStorage();
                                    if(atomic is not null){atomic.Commit();atomic.Dispose();atomic=null;}
                                    if(point is not null){transaction!.ReleaseSavepoint(point);point=null;}
                                    statementEnded=true;
                                    foreach(var row in buffered){requestToken.ThrowIfCancellationRequested();await stream.WriteAsync(NativeFrame.Row,row,lifetime.Token).ConfigureAwait(false);}
                                }else{foreach(var row in rows){requestToken.ThrowIfCancellationRequested();await stream.WriteAsync(NativeFrame.Row,row,lifetime.Token).ConfigureAwait(false);}}
                            }catch(Exception error){
                                if(point is not null){transaction!.RollbackToSavepoint(point);transaction.ReleaseSavepoint(point);point=null;}
                                if(atomic is not null){atomic.Dispose();atomic=null;}
                                if(resolved.Module is INativeRecordQueryModule&&!statementEnded&&error is not OperationCanceledException and not IOException and not global::Seamless.Transactions.TransactionCommitException){throw new NativeQueryException(error.Message,true);}
                                throw;
                            }
                            finally{read?.Dispose();atomic?.Dispose();}
                        }
                        await stream.WriteAsync(NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,lifetime.Token).ConfigureAwait(false);
                    }catch(OperationCanceledException)when(requestCancellation.IsCancellationRequested&&!lifetime.IsCancellationRequested){
                    }catch(Exception ex) when(!lifetime.IsCancellationRequested&&!stream.OutputAborted){
                        
                        var message=Encoding.UTF8.GetBytes(ex.Message);if(message.Length>32768){message=message[..32768];}
                        var kind=NativeFrame.Error;
                        if(ex is AuthorizationException or OidcAuthenticationException{Code:OidcAuthenticationException.BusyCode}){kind=NativeFrame.RecoverableError;}
                        if(ex is NativeQueryException{IsRecoverable:true}){kind=NativeFrame.RecoverableError;}
                        if(this.databases.GatewayTransactions is not null&&ex is not global::Seamless.Transactions.TransactionCommitException&&gatewaySession.TransactionState>=0&&(request.TransactionId==Guid.Empty||gatewaySession.IsInTransaction&&(authorization?.WireTransactionId??gatewaySession.Transaction?.Id)==request.TransactionId)){kind=NativeFrame.RecoverableError;}
                        await stream.WriteAsync(kind,message,lifetime.Token).ConfigureAwait(false);
                    }finally{try{authorization?.CompleteRequest(transaction is not null||gatewaySession.IsInTransaction);}finally{lock(requestGate){activeCancellation=null;activeSequence=0;}}}
                }
            }catch(OperationCanceledException) when(lifetime.IsCancellationRequested){}catch(IOException){}catch(SocketException){}catch(InvalidDataException){}
            finally{
                lifetime.Cancel();
                try{transaction?.Dispose();}
                finally{
                    try{await receive.ConfigureAwait(false);}catch(OperationCanceledException){}catch(IOException){}catch(SocketException){}
                    finally{while(input.Reader.TryRead(out var abandoned)){abandoned.Reservation?.Dispose();}}
                }
            }
            async Task ReceiveAsync(){
                IDisposable? reservation=null;
                object Parse(ReadOnlySequence<byte> frame){verifyOwner?.Invoke();verifyOwner=null;return this.ReadConnectionMessage(frame);}
                Func<ReadOnlySequence<byte>,object> parse=Parse;
                if(this.bufferedRequests is{} budget){
                    parse=frame=>{
                        reservation=budget.TryReserve(frame.Length)??throw new IOException("The buffered request limit has been reached.");
                        return Parse(frame);
                    };
                }
                try{
                    while(!lifetime.IsCancellationRequested){
                        var request=await stream.ReadAsync(parse,lifetime.Token,MaximumIncomingPayload).ConfigureAwait(false);if(request is null){break;}
                        if(request is CancellationBarrier cancel){
                            lock(requestGate){
                                if(cancel.Sequence<=0||cancel.Sequence>receivedSequence){throw new InvalidDataException("The native cancellation sequence is invalid.");}
                                if(cancel.Sequence==activeSequence){activeCancellation!.Cancel();}
                                else if(cancel.Sequence==receivedSequence){cancelledSequence=cancel.Sequence;}
                            }
                            reservation?.Dispose();reservation=null;
                        }else if(request is NativeQueryRequest query&&!ReferenceEquals(query,DiscoverConnectionPolicy)){
                            lock(requestGate){request=new QueuedRequest(query,checked(++receivedSequence));}
                        }
                        await input.Writer.WriteAsync((request,reservation),lifetime.Token).ConfigureAwait(false);
                        reservation=null;
                    }
                    input.Writer.TryComplete();lifetime.Cancel();
                }catch(Exception ex){input.Writer.TryComplete(ex);lifetime.Cancel();}
                finally{reservation?.Dispose();}
            }
        }
    }
    private static int MaximumIncomingPayload(byte kind){
        if(kind==NativeFrame.Request){return NativeFrame.MaximumBytes-1;}
        if(kind==NativeFrame.Cancel){return 8;}
        if(kind==NativeAuthenticationWire.Reauthenticate){return NativeAuthenticationWire.MaximumRequestBytes;}
        if(kind==NativeConnectionPolicyWire.Discover){return NativeConnectionPolicyWire.MaximumDiscoveryBytes;}
        throw new InvalidDataException("Unknown native request kind.");
    }
    private (INativeQueryModule Module,NativeQueryDefinition Definition) ResolveQuery(NativeQueryRequest request,DatabaseRegistration database){
        if(!this.modules.TryGetValue(request.ModuleId,out var module)){throw new NativeQueryException("The native query module is not installed.");}
        var definition=module.Manifest.Queries.SingleOrDefault(query=>query.Id==request.QueryId&&query.Database==database.Name)
            ??throw new NativeQueryException("The native query is not registered for this database.");
        ValidateContracts(definition,request,database.SchemaFingerprint);
        return(module.Module,definition);
    }
    // 通常要求のペイロードと、認証・接続設定・取消の制御フレームを分けて読む。
    private object ReadConnectionMessage(ReadOnlySequence<byte> frame){
        var reader=new SequenceReader<byte>(frame);if(!reader.TryRead(out var kind)){throw new InvalidDataException("Missing native frame kind.");}var payload=frame.Slice(reader.Position);
        if(kind==NativeFrame.Cancel){
            if(payload.Length!=8){throw new InvalidDataException("Invalid native cancellation payload.");}
            Span<byte> sequence=stackalloc byte[8];payload.CopyTo(sequence);return new CancellationBarrier(BinaryPrimitives.ReadInt64LittleEndian(sequence));
        }
        if(kind==NativeAuthenticationWire.Reauthenticate&&this.authorization is not null){return NativeAuthenticationWire.ReadRequest(payload);}
        if(kind==NativeConnectionPolicyWire.Discover){
            if(NativeConnectionPolicyWire.ReadDiscovery(payload) is{} profile){return new ConnectionPolicyDiscovery(profile);}
            return DiscoverConnectionPolicy;
        }
        if(kind!=NativeFrame.Request){throw new InvalidDataException("Expected a native query request.");}
        if(this.authorization is not null){NativeQueryRequestWire.Validate(payload);}
        var request=MemoryPackSerializer.Deserialize<NativeQueryRequest>(payload)??throw new InvalidDataException("Native request is empty.");return request;
    }
    private async Task ExecuteGatewayAsync(NativeQueryRequest request,DatabaseRegistration database,InProcessDatabaseAdapter adapter,SessionContext session,LengthPrefixedPipe stream,CancellationToken cancellationToken,CancellationToken outputCancellationToken,NativeConnectionAuthorization? authorization,AuthorizationLease? lease){
        var manager=this.databases.GatewayTransactions!;session.DatabaseName=database.Name;
        var activeId=session.Transaction?.Id??Guid.Empty;
        var wireId=activeId==Guid.Empty?Guid.Empty:authorization?.WireTransactionId??activeId;
        Guid StorageId(Guid id)=>lease is null?id:this.authorization!.StorageTransactionId(manager.WalId,adapter.Database.Options.GatewayProfile!.DatabaseId,lease.Snapshot.Subject,id);
        if(request.ModuleId=="$transaction"){
            if(request.QueryId=="outcome"){
                var id=MemoryPackSerializer.Deserialize<Guid>(request.Arguments);
                if(lease is not null&&id==Guid.Empty){throw new InvalidDataException("A transaction ID is required.");}
                var outcome=manager.GetOutcome(StorageId(id));var kind=(NativeTransactionOutcomeKind)(int)outcome.Kind;
                await stream.WriteAsync(NativeFrame.Row,MemoryPackSerializer.Serialize(new NativeTransactionOutcome(id,kind,outcome.Lsn)),cancellationToken).ConfigureAwait(false);return;
            }
            session.Run(()=>{
                if(request.TransactionId==Guid.Empty){throw new InvalidOperationException("A transaction ID is required.");}
                if(request.QueryId=="begin"){
                    if(activeId!=Guid.Empty&&request.TransactionId!=wireId){throw new InvalidOperationException("The connection does not own the native transaction.");}
                    manager.Begin(session,activeId==Guid.Empty?StorageId(request.TransactionId):activeId);
                    return 0;
                }
                if(wireId!=request.TransactionId){throw new InvalidOperationException("The connection does not own the native transaction.");}
                switch(request.QueryId){
                    case "commit":manager.Commit(session);break;
                    case "rollback":manager.Rollback(session);break;
                    case "save":case "rollback-to":
                        var name=MemoryPackSerializer.Deserialize<string>(request.Arguments)??throw new InvalidDataException("A savepoint name is required.");
                        if(name.Length is 0 or >32){throw new ArgumentException("Savepoint names must contain 1 through 32 characters.");}
                        if(request.QueryId=="save"){session.CreateSavepoint(name);}else{session.RollbackToSavepoint(name);}break;
                    default:throw new NotSupportedException("Unknown transaction operation.");
                }
                return 0;
            });return;
        }
        if(request.TransactionId!=wireId){throw new InvalidOperationException("The native request transaction does not match the connection.");}
        NativeQueryExecution? execution=null;
        var module=session.Run(()=>{
            using var binding=DatabaseRegistry.EnterSqlSession(session);
            database=this.databases.Resolve(request.Database);adapter=(InProcessDatabaseAdapter)database.Adapter;
            var published=manager.TryResolveNativeVersion(session,request.ModuleId,out var version);
            var registered=published?(Manifest:version.Manifest,Module:version.Module):this.modules.TryGetValue(request.ModuleId,out var legacy)?legacy:throw new NativeQueryException("The native query module is not installed.");
            var definition=registered.Manifest.Queries.SingleOrDefault(query=>query.Id==request.QueryId&&query.Database==database.Name)??throw new NativeQueryException("The native query is not registered for this database.");
            if(published&&authorization?.RequiresPublishedExecution==true){execution=manager.DescribeNativeExecution(session,version,definition);}
            ValidateContracts(definition,request,database.SchemaFingerprint,execution);
            if(database.IsReadOnly&&!definition.IsReadOnly){throw new NativeQueryException("The database is read only.");}
            return(Module:registered.Module,Definition:definition);
        });
        if(lease is not null){authorization!.DemandQuery(lease,request,module.Module,module.Definition,execution);}
        using var recordResources=new ExecutionScope(manager.QueryExecutionOptions(cancellationToken));
        var implicitTransaction=!session.IsInTransaction&&!module.Definition.IsReadOnly;string? savepoint=null;
        try{
            session.Run(()=>{
                if(implicitTransaction){manager.Begin(session);}
                else if(!module.Definition.IsReadOnly){var point="native/"+Guid.NewGuid().ToString("N");session.CreateSavepoint(point);savepoint=point;}
                if(!module.Definition.IsReadOnly){adapter.PrepareGatewayWrite(session);}return 0;
            });
            IEnumerable<byte[]> Read(){
                using var binding=DatabaseRegistry.EnterSqlSession(session);using var read=adapter.AcquireReadSession(session);using var dependencies=manager.AcquireNativeQueryReads(session,request.ModuleId,module.Definition.IsReadOnly);IDisposable? readOnly=null;
                try{
                    if(manager.TryResolveNativeVersion(session,request.ModuleId,out var currentVersion)){
                        var definition=currentVersion.Manifest.Queries.SingleOrDefault(query=>query.Id==request.QueryId&&query.Database==database.Name)??throw new NativeQueryException("The native query is no longer registered.");
                        if(definition.IsReadOnly!=module.Definition.IsReadOnly){throw new NativeQueryException("The native query definition changed while waiting.");}module=(currentVersion.Module,definition);
                        if(authorization?.RequiresPublishedExecution==true){execution=manager.DescribeNativeExecution(session,currentVersion,definition);}
                        ValidateContracts(definition,request,database.SchemaFingerprint,execution);
                    }
                    if(lease is not null){authorization!.DemandQuery(lease,request,module.Module,module.Definition,execution);}
                    if(module.Definition.IsReadOnly){readOnly=adapter.Database.BeginGatewayReadOnlyExecution();}
                    var context=new NativeContext(adapter.Database,database.Name,cancellationToken,name=>manager.NativeDatabase(session,request.ModuleId,name,module.Definition.IsReadOnly),recordResources.Budget);
                    using var iterator=ExecuteRows(module.Module,module.Definition,request.Arguments,context).GetEnumerator();while(manager.ExecuteNativeOperation(session,iterator.MoveNext)){cancellationToken.ThrowIfCancellationRequested();yield return iterator.Current;}
                }finally{readOnly?.Dispose();}
            }
            var rows=session.Enumerate(Read());
            if(module.Definition.IsReadOnly){foreach(var row in rows){cancellationToken.ThrowIfCancellationRequested();await stream.WriteAsync(NativeFrame.Row,row,outputCancellationToken).ConfigureAwait(false);}}
            else{
                using var resources=new ExecutionScope(manager.QueryExecutionOptions(cancellationToken));using var buffered=resources.Query(rows).ToStorage();
                session.Run(()=>{manager.ValidateNativeStatement(session);if(implicitTransaction){manager.Commit(session);}else if(savepoint is not null){session.ReleaseSavepoint(savepoint);savepoint=null;}return 0;});
                foreach(var row in buffered){cancellationToken.ThrowIfCancellationRequested();await stream.WriteAsync(NativeFrame.Row,row,outputCancellationToken).ConfigureAwait(false);}
            }
        }catch{
            session.Run(()=>{
                if(implicitTransaction&&session.IsInTransaction){manager.Rollback(session);}
                else if(savepoint is not null&&session.TransactionState>0){try{session.RollbackToSavepoint(savepoint);}catch{session.MarkRollbackOnly();throw;}}return 0;
            });throw;
        }finally{if(savepoint is not null&&session.IsInTransaction){session.Run(()=>{session.ReleaseSavepoint(savepoint);return 0;});}}
    }
    private static void ValidateContracts(NativeQueryDefinition definition,NativeQueryRequest request,string? schemaFingerprint,NativeQueryExecution? execution=null){
        if(definition.ArgumentsContract is{} arguments&&arguments!=request.ArgumentsContract){throw new NativeQueryException("The native query argument contract does not match the client type.",true);}
        if(definition.ResultContract is{} result&&result!=request.ResultContract){throw new NativeQueryException("The native query result contract does not match the client type.",true);}
        if(definition.SchemaFingerprint!=schemaFingerprint){throw new NativeQueryException("The native query schema fingerprint is stale.");}
        if(request.SchemaFingerprint==schemaFingerprint){return;}
        if(execution?.HasSchemaIndependentWireContract==true&&ReferenceEquals(execution.Definition,definition)
            &&Hash(definition.ArgumentsContract)&&Hash(definition.ResultContract)&&definition.ArgumentsContract==request.ArgumentsContract&&definition.ResultContract==request.ResultContract){return;}
        throw new NativeQueryException("The native query schema fingerprint is stale.");
        static bool Hash(string? hash)=>hash?.Length==64&&hash.All(Uri.IsHexDigit);
    }
    private sealed record NativeContext(object Database,string DatabaseName,CancellationToken CancellationToken,Func<string,object>? Resolve=null,ResourceBudget? Budget=null):INativeQueryContext{
        public object GetDatabase(string name)=>name.Equals(this.DatabaseName,StringComparison.OrdinalIgnoreCase)?this.Database:this.Resolve?.Invoke(name)??throw new NativeQueryException("The native query has no declared access to database '"+name+"'.");
    }
    public void Dispose()=>this.StopAsync().GetAwaiter().GetResult();
}
