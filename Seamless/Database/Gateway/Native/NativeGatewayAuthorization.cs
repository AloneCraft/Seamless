using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MemoryPack;
using Seamless.Authentication;
using Seamless.Database.Client;
using Seamless.Security;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Native;

/// <summary>認証元と永続ポリシーはホストが所有する。認可callbackは実行物と資源の対応を確認する。</summary>
public sealed class NativeGatewayAuthorization{
    private readonly OidcTokenVerifier verifier;
    private readonly Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> sessions;
    private readonly Action<AuthorizationLease,NativeQueryRequest> authorizeRequest;
    private readonly Action<AuthorizationLease,NativeQueryRequest,INativeQueryModule,NativeQueryDefinition> authorizeQuery;
    private readonly Action<NativeQueryDefinition,ReadOnlyMemory<byte>> validateArguments;
    private readonly Action<AuthorizationLease,NativeQueryRequest,NativeQueryExecution>? authorizePublishedQuery;
    internal bool RequiresPublishedExecution=>this.authorizePublishedQuery is not null;
    private readonly ConnectionQuota authenticationQuota;
    private readonly TimeSpan timeout;
    private static readonly UTF8Encoding OutcomeUtf8=new(false,true);
    private readonly byte[]? transactionOutcomeKey;
    /// <summary>同じWALの再起動でも維持する、結果照会専用の32バイトの鍵。空の場合は照会を無効にする。</summary>
    public ReadOnlyMemory<byte> TransactionOutcomeKey{
        init{
            if(value.IsEmpty){return;}
            if(value.Length!=32){throw new ArgumentException("The native transaction outcome key must contain 32 bytes.",nameof(value));}
            this.transactionOutcomeKey=value.ToArray();
        }
    }
    public NativeGatewayAuthorization(OidcTokenVerifier verifier,Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> sessions,
        Action<AuthorizationLease,NativeQueryRequest> authorizeRequest,Action<AuthorizationLease,NativeQueryRequest,INativeQueryModule,NativeQueryDefinition> authorizeQuery,
        Action<NativeQueryDefinition,ReadOnlyMemory<byte>> validateArguments,int maximumConcurrentAuthentications=32,TimeSpan? authenticationTimeout=null){
        this.verifier=verifier??throw new ArgumentNullException(nameof(verifier));this.sessions=sessions??throw new ArgumentNullException(nameof(sessions));
        this.authorizeRequest=authorizeRequest??throw new ArgumentNullException(nameof(authorizeRequest));this.authorizeQuery=authorizeQuery??throw new ArgumentNullException(nameof(authorizeQuery));this.validateArguments=validateArguments??throw new ArgumentNullException(nameof(validateArguments));
        this.authenticationQuota=new(maximumConcurrentAuthentications);this.timeout=authenticationTimeout??TimeSpan.FromSeconds(15);
        if(this.timeout<=TimeSpan.Zero||this.timeout.TotalMilliseconds>uint.MaxValue-1){throw new ArgumentOutOfRangeException(nameof(authenticationTimeout));}
    }
    private NativeGatewayAuthorization(OidcTokenVerifier verifier,Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> sessions,
        Action<AuthorizationLease,NativeQueryRequest> authorizeRequest,Action<AuthorizationLease,NativeQueryRequest,NativeQueryExecution> authorizePublishedQuery,int maximumConcurrentAuthentications,TimeSpan? authenticationTimeout)
        :this(verifier,sessions,authorizeRequest,static(_,_,_,_)=>{},static(_,_)=>{},maximumConcurrentAuthentications,authenticationTimeout){this.authorizePublishedQuery=authorizePublishedQuery??throw new ArgumentNullException(nameof(authorizePublishedQuery));}
    /// <summary>保存・ロード時に検証した実行物と、同じ取引から可視な全依存DBを必須とする。</summary>
    public static NativeGatewayAuthorization ForPublishedQueries(OidcTokenVerifier verifier,Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> sessions,
        Action<AuthorizationLease,NativeQueryRequest> authorizeRequest,Action<AuthorizationLease,NativeQueryRequest,NativeQueryExecution> authorizeQuery,int maximumConcurrentAuthentications=32,TimeSpan? authenticationTimeout=null)=>new(verifier,sessions,authorizeRequest,authorizeQuery,maximumConcurrentAuthentications,authenticationTimeout);
    public static NativeGatewayAuthorization ForPublishedQueries(OidcTokenVerifier verifier,Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> sessions,
        Action<AuthorizationLease,NativeQueryRequest> authorizeRequest,Action<AuthorizationLease,NativeQueryRequest,NativeQueryExecution> authorizeQuery,int maximumConcurrentAuthentications,TimeSpan? authenticationTimeout,ReadOnlyMemory<byte> transactionOutcomeKey)=>
        new(verifier,sessions,authorizeRequest,authorizeQuery,maximumConcurrentAuthentications,authenticationTimeout){TransactionOutcomeKey=transactionOutcomeKey};
    internal async Task<OidcAuthorizationSession?> AuthenticateAsync(LengthPrefixedPipe pipe,CancellationToken cancellationToken){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(this.timeout);
        using var permit=this.authenticationQuota.TryEnter();OidcAuthorizationSession? session=null;
        try{
            var token=await pipe.ReadAsync(static frame=>{
                var reader=new SequenceReader<byte>(frame);if(!reader.TryRead(out var kind)||kind!=NativeAuthenticationWire.Authenticate){throw new InvalidDataException("Authentication is required.");}
                return NativeAuthenticationWire.ReadRequest(frame.Slice(reader.Position));
            },deadline.Token,static kind=>kind==NativeAuthenticationWire.Authenticate?NativeAuthenticationWire.MaximumRequestBytes:throw new InvalidDataException("Authentication is required.")).ConfigureAwait(false);
            if(token is null){return null;}
            // 要求を受ける前に閉じるとTLSの送信と競合し、Busyではなく送信失敗となる。
            // 枠がない接続も有界の最初のframeだけ読み、本人確認・DB処理は開始しない。
            if(permit is null){await pipe.WriteAsync(NativeAuthenticationWire.Error,NativeAuthenticationWire.Failure(NativeAuthenticationError.Busy),deadline.Token).ConfigureAwait(false);return null;}
            session=await OidcAuthorizationSession.AuthenticateWithSessionFactoryAsync(this.verifier,this.sessions,token,deadline.Token).ConfigureAwait(false);
            await pipe.WriteAsync(NativeAuthenticationWire.Authenticated,NativeAuthenticationWire.Acknowledgement(session.Subject.AuthorityId,session.Subject.Value),deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();var result=session;session=null;return result;
        }catch(Exception error)when(!cancellationToken.IsCancellationRequested&&!pipe.OutputAborted){
            var code=ClassifyAuthenticationFailure(error);
            try{await pipe.WriteAsync(NativeAuthenticationWire.Error,NativeAuthenticationWire.Failure(code),deadline.Token).ConfigureAwait(false);}catch(OperationCanceledException){}catch(IOException){}
            return null;
        }finally{session?.Dispose();}
    }
    internal async Task ReauthenticateAsync(OidcAuthorizationSession session,LengthPrefixedPipe pipe,string token,CancellationToken cancellationToken){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(this.timeout);
        try{
            await session.ReauthenticateAsync(token,deadline.Token).ConfigureAwait(false);
        }catch(Exception error)when(!cancellationToken.IsCancellationRequested&&(error is OidcAuthenticationException or AuthorizationException||error is OperationCanceledException&&deadline.IsCancellationRequested)){
            await pipe.WriteAsync(NativeAuthenticationWire.Error,NativeAuthenticationWire.Failure(ClassifyAuthenticationFailure(error)),cancellationToken).ConfigureAwait(false);
            return;
        }
        await pipe.WriteAsync(NativeAuthenticationWire.Authenticated,NativeAuthenticationWire.Acknowledgement(session.Subject.AuthorityId,session.Subject.Value),cancellationToken).ConfigureAwait(false);
    }
    private static NativeAuthenticationError ClassifyAuthenticationFailure(Exception error)=>error switch{
        InvalidDataException=>NativeAuthenticationError.InvalidRequest,
        OidcAuthenticationException{Code:OidcAuthenticationException.BusyCode}=>NativeAuthenticationError.Busy,
        AuthorizationException or OidcAuthenticationException{Code:"TokenRejected"}=>NativeAuthenticationError.Rejected,
        _=>NativeAuthenticationError.Unavailable
    };
    internal void DemandRequest(AuthorizationLease lease,NativeQueryRequest request){
        lease.ThrowIfCancellationRequested();
        if(request.ModuleId=="$transaction"){
            switch(request.QueryId){
                case "begin":case "commit":case "rollback":if(request.Arguments.Length!=0){throw new InvalidDataException("Unexpected transaction arguments.");}break;
                case "outcome":
                    if(this.transactionOutcomeKey is null){throw new AuthorizationException("UnsupportedProtectedOperation","Native transaction outcome lookup is not configured.");}
                    NativeWireValidation.Validate<Guid>(request.Arguments);
                    break;
                case "save":case "rollback-to":
                    NativeWireValidation.Validate<string>(request.Arguments,new(){MaximumStringCharacters=32});
                    var name=MemoryPackSerializer.Deserialize<string>(request.Arguments);if(string.IsNullOrEmpty(name)){throw new InvalidDataException("A savepoint name is required.");}break;
                default:throw new InvalidDataException("Unknown transaction operation.");
            }
        }
        this.authorizeRequest(lease,request);lease.ThrowIfCancellationRequested();
    }
    internal Guid StorageTransactionId(Guid walId,int databaseId,SecuritySubjectId subject,Guid wireId){
        if(this.transactionOutcomeKey is not{} key){return wireId;}
        const int prefixBytes=60;
        var input=new byte[prefixBytes+OutcomeUtf8.GetByteCount(subject.Value)];
        "SLTXID01"u8.CopyTo(input);
        walId.TryWriteBytes(input.AsSpan(8,16));
        BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(24,4),databaseId);
        subject.AuthorityId.TryWriteBytes(input.AsSpan(28,16));
        // 固定長のID群に続けてUTF-8の主体を格納する。主体は認証済みの値を使う。
        wireId.TryWriteBytes(input.AsSpan(44,16));
        OutcomeUtf8.GetBytes(subject.Value,input.AsSpan(prefixBytes));
        Span<byte> digest=stackalloc byte[32];
        HMACSHA256.HashData(key,input,digest);
        return new Guid(digest[..16]);
    }
    internal void DemandQuery(AuthorizationLease lease,NativeQueryRequest request,INativeQueryModule module,NativeQueryDefinition definition,NativeQueryExecution? execution){
        lease.ThrowIfCancellationRequested();
        if(this.RequiresPublishedExecution){
            if(execution is null||!ReferenceEquals(execution.Module,module)||!ReferenceEquals(execution.Definition,definition)){throw new AuthorizationException("UnverifiedExecution","The native execution has no verified publication identity.");}
            this.DemandPublishedExecution(lease,request,execution);execution.ValidateArguments(request.Arguments,lease.CancellationToken);
        }else{this.authorizeQuery(lease,request,module,definition);this.validateArguments(definition,request.Arguments);}
        lease.ThrowIfCancellationRequested();
    }
    internal void DemandPublishedExecution(AuthorizationLease lease,NativeQueryRequest request,NativeQueryExecution execution){
        lease.ThrowIfCancellationRequested();
        var authorize=this.authorizePublishedQuery??throw new AuthorizationException("UnverifiedExecution","The server does not use verified native publications.");
        authorize(lease,request,execution);
        lease.ThrowIfCancellationRequested();
    }
}

/// <summary>認証付き入口の有限上限。接続数だけを多数接続の保証としない。</summary>
public sealed record NativeServerLimits{
    public int MaximumConnections{get;init;}=1024;
    public int MaximumConcurrentHandshakes{get;init;}=32;
    public int MaximumConcurrentRequests{get;init;}=64;
    public long MaximumReceiveBufferBytes{get;init;}=64L*1024*1024;
    public long MaximumBufferedRequestBytes{get;init;}=64L*1024*1024;
    public long MaximumPendingWriteBytes{get;init;}=64L*1024*1024;
    public TimeSpan IncompleteFrameTimeout{get;init;}=TimeSpan.FromSeconds(15);
    public TimeSpan FrameWriteTimeout{get;init;}=TimeSpan.FromSeconds(30);
    internal void Validate(){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaximumConnections);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaximumConcurrentRequests);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaximumConcurrentHandshakes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaximumReceiveBufferBytes);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaximumBufferedRequestBytes);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaximumPendingWriteBytes);
        if(this.IncompleteFrameTimeout<=TimeSpan.Zero||this.IncompleteFrameTimeout.TotalMilliseconds>uint.MaxValue-1||this.FrameWriteTimeout<=TimeSpan.Zero||this.FrameWriteTimeout.TotalMilliseconds>uint.MaxValue-1){throw new ArgumentOutOfRangeException(nameof(NativeServerLimits));}
    }
}

internal sealed class NativeConnectionAuthorization(NativeGatewayAuthorization authorization,OidcAuthorizationSession session,CancellationTokenSource lifetime):IDisposable{
    private AuthorizationLease? transaction;
    private Guid transactionId;
    internal Guid WireTransactionId=>this.transactionId;
    private CancellationTokenRegistration transactionCancellation;
    internal async ValueTask<Scope> BeginAsync(NativeQueryRequest request){
        AuthorizationLease lease;var owned=false;
        if(this.transaction is{} current){
            if(request.TransactionId!=this.transactionId){throw new AuthorizationException("TransactionMismatch","The authenticated transaction belongs to a different request.");}lease=current;
        }else{
            if(request.ModuleId=="$transaction"&&request.QueryId=="begin"){
                if(request.TransactionId==Guid.Empty){throw new InvalidDataException("A transaction ID is required.");}
                lease=await session.BeginTransactionAsync(lifetime.Token).ConfigureAwait(false);this.transaction=lease;this.transactionId=request.TransactionId;
                this.transactionCancellation=lease.CancellationToken.Register(static state=>((CancellationTokenSource)state!).Cancel(),lifetime);
            }else{lease=await session.BeginOperationAsync(lifetime.Token).ConfigureAwait(false);owned=true;}
        }
        var scope=new Scope(lease,owned,lifetime);try{authorization.DemandRequest(lease,request);return scope;}catch{scope.Dispose();throw;}
    }
    internal bool RequiresPublishedExecution=>authorization.RequiresPublishedExecution;
    internal void DemandQuery(AuthorizationLease lease,NativeQueryRequest request,INativeQueryModule module,NativeQueryDefinition definition,NativeQueryExecution? execution=null)=>authorization.DemandQuery(lease,request,module,definition,execution);
    internal void CompleteRequest(bool transactionActive){if(!transactionActive){this.ReleaseTransaction();}}
    private void ReleaseTransaction(){this.transactionCancellation.Dispose();this.transactionCancellation=default;this.transaction?.Dispose();this.transaction=null;this.transactionId=default;}
    public void Dispose()=>this.ReleaseTransaction();
    internal sealed class Scope:IDisposable{
        private readonly bool owned;
        private readonly CancellationTokenRegistration cancellation;
        internal AuthorizationLease Lease{get;}
        internal Scope(AuthorizationLease lease,bool owned,CancellationTokenSource lifetime){this.Lease=lease;this.owned=owned;this.cancellation=lease.CancellationToken.Register(static state=>((CancellationTokenSource)state!).Cancel(),lifetime);}
        public void Dispose(){this.cancellation.Dispose();if(this.owned){this.Lease.Dispose();}}
    }
}
