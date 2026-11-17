using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using System.Text;
using MemoryPack;
using Seamless.Execution;
using Seamless.Execution.Codecs;

namespace Seamless.Database.Client;

/// <summary>接続単位で要求を直列化する型付きクエリクライアント。</summary>
public sealed partial class NativeQueryClient(string host="localhost",int port=14335):IAsyncDisposable{
    private const int MaximumReusablePayloadBytes=64*1024;
    private readonly SemaphoreSlim gate=new(1,1);
    private TcpClient? socket;
    private Stream? stream;
    private readonly bool tls;
    private readonly X509Certificate2? trustedRoot;
    private readonly Func<CancellationToken,ValueTask<string>>? accessTokenProvider;
    private NativeTransaction? transaction;
    private bool disposed;
    private long requestSequence;
    private NativeQueryClient(string host,int port,bool tls,X509Certificate2? trustedRoot,Func<CancellationToken,ValueTask<string>>? accessTokenProvider):this(host,port){
        this.tls=tls;this.trustedRoot=trustedRoot;this.accessTokenProvider=accessTokenProvider;
    }
    private NativeQueryClient(string host,int port,X509Certificate2? trustedRoot):this(host,port,true,trustedRoot,null){}
    /// <summary>TLSを明示して接続する。省略時はOSの信頼ストアを使い、失敗時に平文へ切り替えない。証明書は呼出し元が所有する。</summary>
    public static NativeQueryClient ForTls(string host="localhost",int port=14335,X509Certificate2? trustedRoot=null)=>new(host,port,trustedRoot);
    /// <summary>呼出し元が所有するレコード予算と制限を使ってTLS接続を構築する。</summary>
    public static NativeQueryClient ForTls(string host,int port,X509Certificate2? trustedRoot,ResourceBudget recordBudget,RecordGraphValidationLimits? recordLimits=null){
        ArgumentNullException.ThrowIfNull(recordBudget);recordLimits??=RecordGraphValidationLimits.Default;recordLimits.CheckForNative();
        return new(host,port,trustedRoot){RecordBudget=recordBudget,RecordLimits=recordLimits};
    }
    private NativeQueryClient(string host,int port,X509Certificate2? trustedRoot,Func<CancellationToken,ValueTask<string>> accessTokenProvider):this(host,port,true,trustedRoot,accessTokenProvider??throw new ArgumentNullException(nameof(accessTokenProvider))){}
    /// <summary>TLSと本人確認が完了した接続だけを使う。認証失敗後の要求の自動再送は行わない。</summary>
    public static NativeQueryClient ForAuthenticatedTls(Func<CancellationToken,ValueTask<string>> accessTokenProvider,string host="localhost",int port=14335,X509Certificate2? trustedRoot=null)=>new(host,port,trustedRoot,accessTokenProvider);
    /// <summary>認証付きTLS接続へ、呼出し元が所有するレコード予算と制限を設定する。</summary>
    public static NativeQueryClient ForAuthenticatedTls(Func<CancellationToken,ValueTask<string>> accessTokenProvider,string host,int port,X509Certificate2? trustedRoot,ResourceBudget recordBudget,RecordGraphValidationLimits? recordLimits=null){
        ArgumentNullException.ThrowIfNull(recordBudget);recordLimits??=RecordGraphValidationLimits.Default;recordLimits.CheckForNative();
        return new(host,port,trustedRoot,accessTokenProvider){RecordBudget=recordBudget,RecordLimits=recordLimits};
    }
    public NativeAuthenticatedPrincipal? AuthenticatedPrincipal{get;private set;}
    public async Task ConnectAsync(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.transaction is not null){throw new InvalidOperationException("A transaction already owns this connection.");}await this.EnterConnectionAsync(cancellationToken).ConfigureAwait(false);try{await this.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);}catch{this.CloseConnection();throw;}finally{this.ReleaseOperation(preservePreconnected:true);}
    }
    private async ValueTask EnterConnectionAsync(CancellationToken cancellationToken){
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if(this.disposed){
            this.gate.Release();
            throw new ObjectDisposedException(nameof(NativeQueryClient));
        }
    }
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken){
        if(this.stream is not null){return;}
        if(this.pipeName is not null){await this.ConnectPipeAsync(cancellationToken).ConfigureAwait(false);return;}
        var connected=new TcpClient{NoDelay=true};Stream? transport=null;
        try{
            await connected.ConnectAsync(host,port,cancellationToken).ConfigureAwait(false);
            transport=connected.GetStream();
            if(this.tls){
                var secure=new SslStream(transport,leaveInnerStreamOpen:false);transport=secure;
                var options=new SslClientAuthenticationOptions{TargetHost=host,CertificateRevocationCheckMode=X509RevocationMode.Online};
                if(this.trustedRoot is not null){
                    options.CertificateChainPolicy=new(){TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.Online};
                    options.CertificateChainPolicy.CustomTrustStore.Add(this.trustedRoot);
                }
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(15));
                await secure.AuthenticateAsClientAsync(options,deadline.Token).ConfigureAwait(false);
            }
            NativeAuthenticatedPrincipal? principal=null;
            if(this.accessTokenProvider is not null){
                principal=await this.AuthenticateAsync(transport,cancellationToken).ConfigureAwait(false);
                if(this.reconnectPrincipal is{} expected&&principal!=expected){throw new InvalidDataException("The native client received a different subject.");}
            }
            if(this.UseServerConnectionPolicy&&this.configuredConnectionPolicy is null&&!this.connectionPolicyDiscovered){
                await this.DiscoverConnectionPolicyAsync(transport,cancellationToken).ConfigureAwait(false);
            }
            if(principal is not null&&(this.UseServerConnectionPolicy||this.ConnectionPolicy.Lifetime!=NativeConnectionLifetime.KeepAlive)){this.reconnectPrincipal=principal;}
            this.socket=connected;this.stream=transport;this.AuthenticatedPrincipal=principal;
        }catch{transport?.Dispose();connected.Dispose();throw;}
    }
    public IAsyncEnumerable<TResult> QueryAsync<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>this.QueryCore<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,null,cancellationToken);
    internal IAsyncEnumerable<TResult> QueryCore<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,NativeTransaction? transaction,CancellationToken cancellationToken)=>
        this.QueryProjectedAsync<TArguments,TResult,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,transaction,
            static payload=>MemoryPackSerializer.Deserialize<TResult>(payload)!,cancellationToken);

    internal async IAsyncEnumerable<TOutput> QueryProjectedAsync<TArguments,TResult,TOutput>(string moduleId,string queryId,string database,string schemaFingerprint,
        TArguments arguments,NativeTransaction? transaction,Func<ReadOnlySpan<byte>,TOutput> projectRow,[EnumeratorCancellation]CancellationToken cancellationToken,NativeRecordQueryContract? recordContract=null){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(transaction is null&&this.transaction is not null){throw new InvalidOperationException("Use the active transaction to execute queries.");}
        if(transaction is not null){this.Validate(transaction);transaction.Enter();}else{await this.EnterConnectionAsync(cancellationToken).ConfigureAwait(false);}
        var complete=false;var atFrameBoundary=true;var fatal=false;NativeQueryCancellation? cancellation=null;
        try{
            NativeQueryRequest request;
            global::Seamless.Execution.OwnedValue<byte[]>? recordPayload=null;
            try{
                if(recordContract is null){request=new(moduleId,queryId,database,schemaFingerprint,MemoryPackSerializer.Serialize(arguments),transaction?.Id??Guid.Empty,NativeWireContract.For<TArguments>(),NativeWireContract.For<TResult>());await this.SendAsync(request,cancellationToken).ConfigureAwait(false);}
                else{
                    recordPayload=NativeRecordCodec<TArguments>.SerializeOwned(arguments,this.RecordBudget,cancellationToken,this.RecordLimits);
                    request=new(moduleId,queryId,database,schemaFingerprint,recordPayload.Value,transaction?.Id??Guid.Empty,recordContract.ArgumentsContract,recordContract.ResultContract);
                    await this.SendAsync(request,cancellationToken,this.RecordBudget).ConfigureAwait(false);
                }
                cancellation=new(this.stream!,this.requestSequence,cancellationToken);
                request=null!;
            }finally{recordPayload?.Dispose();}
            var header=new byte[5];
            byte[] reusablePayload=[];
            while(true){
                atFrameBoundary=false;
                var (kind,length)=await NativeFrame.ReadHeaderAsync(this.stream!,header,cancellation.ReadToken).ConfigureAwait(false)
                    ??throw new EndOfStreamException("The server disconnected before completing the query.");
                if(kind==NativeFrame.Completed){
                    // 完了を認める前に検証し、不正な本体は確保も読み取りもしない。
                    if(length!=0){throw new InvalidDataException("Unexpected payload in native query completion.");}
                    atFrameBoundary=true;
                    if(cancellationToken.IsCancellationRequested){continue;}
                    complete=true;yield break;
                }
                if(kind==NativeFrame.Cancelled&&length!=8){throw new InvalidDataException("Invalid native cancellation acknowledgement.");}
                if(length>reusablePayload.Length&&length<=MaximumReusablePayloadBytes){
                    reusablePayload=new byte[Math.Max(length,Math.Clamp(reusablePayload.Length*2,256,MaximumReusablePayloadBytes))];
                }
                // 行変換の間だけspanを貸し出す。大きな行の領域は次の受信に持ち越さない。
                var payload=(length<=reusablePayload.Length?reusablePayload:new byte[length]).AsMemory(0,length);
                await this.stream!.ReadExactlyAsync(payload,cancellation.ReadToken).ConfigureAwait(false);atFrameBoundary=true;
                if(kind==NativeFrame.Cancelled){fatal=true;cancellation.Acknowledge(payload.Span);fatal=false;cancellationToken.ThrowIfCancellationRequested();throw new InvalidDataException("Unexpected native cancellation acknowledgement.");}
                if(kind==NativeFrame.RecoverableError){complete=true;throw new NativeQueryException(Encoding.UTF8.GetString(payload.Span),true);}
                if(kind==NativeFrame.Error){fatal=true;throw new NativeQueryException(Encoding.UTF8.GetString(payload.Span));}
                if(kind!=NativeFrame.Row){fatal=true;throw new InvalidDataException("Unexpected native response kind.");}
                if(cancellationToken.IsCancellationRequested){continue;}
                fatal=true;var row=projectRow(payload.Span);fatal=false;
                payload=default;
                yield return row;
            }
        }finally{
            try{
                cancellation?.StopListening();
                if(!complete||cancellation?.Requested==true){
                    var reusable=!fatal&&atFrameBoundary&&cancellation is not null&&await cancellation.FinishAsync().ConfigureAwait(false);
                    if(!reusable){this.CloseConnection();if(transaction is not null&&ReferenceEquals(this.transaction,transaction)){transaction.Finish();this.transaction=null;this.ReleaseOperation();}}
                }
            }finally{
                try{if(cancellation is not null){await cancellation.DisposeAsync().ConfigureAwait(false);}}finally{if(transaction is not null){transaction.Exit();}else{this.ReleaseOperation();}}
            }
        }
    }
    public async Task<TResult> ExecuteScalarAsync<TArguments,TResult>(string moduleId,string queryId,string database,string fingerprint,TArguments arguments,CancellationToken cancellationToken=default){
        var found=false;TResult result=default!;
        await foreach(var value in this.QueryAsync<TArguments,TResult>(moduleId,queryId,database,fingerprint,arguments,cancellationToken).ConfigureAwait(false)){
            if(found){throw new InvalidDataException("A scalar query returned multiple rows.");}result=value;found=true;
        }
        if(!found){throw new InvalidDataException("A scalar query returned no rows.");}return result;
    }
    public async Task<NativeTransaction> BeginTransactionAsync(string database,CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.transaction is not null){throw new InvalidOperationException("A transaction is already active.");}
        await this.EnterConnectionAsync(cancellationToken).ConfigureAwait(false);
        try{var id=Guid.NewGuid();await this.ControlAsync("begin",database,id,cancellationToken).ConfigureAwait(false);var result=new NativeTransaction(this,database,id);this.transaction=result;return result;}
        catch{this.CloseConnection();this.ReleaseOperation();throw;}
    }
    internal async Task FinishTransactionAsync(NativeTransaction transaction,bool commit,CancellationToken cancellationToken){
        this.Validate(transaction);transaction.Enter();
        var keep=false;
        try{var operation="rollback";if(commit){operation="commit";}await this.ControlAsync(operation,transaction.Database,transaction.Id,cancellationToken).ConfigureAwait(false);if(commit&&transaction.TransactionCount>1){transaction.CompleteNested();keep=true;}}
        catch{this.CloseConnection();throw;}
        finally{transaction.Exit();if(!keep){transaction.Finish();this.transaction=null;this.ReleaseOperation();}}
    }
    public Task<NativeTransactionOutcome> GetTransactionOutcomeAsync(string database,Guid transactionId,CancellationToken cancellationToken=default)=>this.ExecuteScalarAsync<Guid,NativeTransactionOutcome>("$transaction","outcome",database,"",transactionId,cancellationToken);
    internal async Task<NativeTransactionOutcome> GetTransactionOutcomeAsync(NativeTransaction transaction,CancellationToken cancellationToken){
        var found=false;NativeTransactionOutcome result=default;
        await foreach(var row in this.QueryCore<Guid,NativeTransactionOutcome>("$transaction","outcome",transaction.Database,"",transaction.Id,transaction,cancellationToken).ConfigureAwait(false)){if(found){throw new InvalidDataException("Multiple outcome records were returned.");}result=row;found=true;}
        if(!found){throw new InvalidDataException("The outcome record was missing.");}return result;
    }
    internal async Task ChangeTransactionAsync(NativeTransaction transaction,string operation,string? savepoint,CancellationToken cancellationToken){
        this.Validate(transaction);if(savepoint is not null&&(savepoint.Length is 0 or >32)){throw new ArgumentException("Savepoint names must contain 1 through 32 characters.",nameof(savepoint));}transaction.Enter();
        try{
            byte[]? arguments=null;if(savepoint is not null){arguments=MemoryPackSerializer.Serialize(savepoint);}
            await this.ControlAsync(operation,transaction.Database,transaction.Id,cancellationToken,arguments).ConfigureAwait(false);if(operation=="begin"){transaction.EnterNested();}
        }catch(NativeQueryException error)when(error.IsRecoverable){throw;}
        catch{this.CloseConnection();transaction.Finish();this.transaction=null;this.ReleaseOperation();throw;}
        finally{transaction.Exit();}
    }
    private void Validate(NativeTransaction transaction){if(!ReferenceEquals(this.transaction,transaction)||transaction.IsCompleted){throw new InvalidOperationException("The transaction does not belong to this connection or has completed.");}}
    private async Task ControlAsync(string operation,string database,Guid transactionId,CancellationToken cancellationToken,byte[]? arguments=null){
        await this.SendAsync(new("$transaction",operation,database,"",arguments??[],transactionId),cancellationToken).ConfigureAwait(false);
        var frame=await NativeFrame.ReadAsync(this.stream!,cancellationToken).ConfigureAwait(false)??throw new EndOfStreamException();
        if(frame.Kind is NativeFrame.Error or NativeFrame.RecoverableError){throw new NativeQueryException(Encoding.UTF8.GetString(frame.Payload),frame.Kind==NativeFrame.RecoverableError);}if(frame.Kind!=NativeFrame.Completed||frame.Payload.Length!=0){throw new InvalidDataException("Unexpected transaction response.");}
    }
    private async ValueTask SendAsync(NativeQueryRequest request,CancellationToken cancellationToken,global::Seamless.Execution.ResourceBudget? budget=null){
        await this.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        using var wireMemory=budget?.ReserveMemory(checked(256L+request.Arguments.Length+4L*(request.ModuleId.Length+request.QueryId.Length+request.Database.Length+request.SchemaFingerprint.Length+(long)(request.ArgumentsContract?.Length??0)+(request.ResultContract?.Length??0))));
        await NativeFrame.WriteAsync(this.stream!,NativeFrame.Request,MemoryPackSerializer.Serialize(request),cancellationToken).ConfigureAwait(false);
        this.requestSequence=checked(this.requestSequence+1);
    }
    private void CloseConnection(){this.stream?.Dispose();this.socket?.Dispose();this.stream=null;this.socket=null;this.requestSequence=0;this.AuthenticatedPrincipal=null;}
    public async ValueTask DisposeAsync(){
        if(this.pool is not null){this.ReturnToPool();return;}
        if(this.disposed){return;}
        try{if(this.transaction is not null){await this.transaction.DisposeAsync().ConfigureAwait(false);}}
        finally{
            lock(this.gate){
                this.disposed=true;
                this.idleTimer?.Dispose();
                this.CloseConnection();
                this.gate.Dispose();
            }
        }
    }
}
public sealed partial class NativeTransaction:IAsyncDisposable{
    private readonly NativeQueryClient client;
    private int active;
    internal NativeTransaction(NativeQueryClient client,string database,Guid id){this.client=client;this.Database=database;this.Id=id;}
    public string Database{get;}
    public Guid Id{get;}
    public bool IsCompleted{get;private set;}
    internal void Enter(){if(Interlocked.CompareExchange(ref this.active,1,0)!=0){throw new InvalidOperationException("Concurrent operations in one transaction are not supported.");}}
    internal void Exit()=>Volatile.Write(ref this.active,0);
    public int TransactionCount{get;private set;}=1;
    internal void EnterNested()=>this.TransactionCount=checked(this.TransactionCount+1);
    internal void CompleteNested()=>this.TransactionCount--;
    internal void Finish(){this.IsCompleted=true;this.TransactionCount=0;}
    public IAsyncEnumerable<TResult> QueryAsync<TArguments,TResult>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>this.client.QueryCore<TArguments,TResult>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,cancellationToken);
    internal IAsyncEnumerable<TOutput> ProjectResult<TArguments,TResult,TOutput>(string moduleId,string queryId,string schemaFingerprint,TArguments arguments,
        Func<ReadOnlySpan<byte>,TOutput> projectRow,CancellationToken cancellationToken)=>
        this.client.QueryProjectedAsync<TArguments,TResult,TOutput>(moduleId,queryId,this.Database,schemaFingerprint,arguments,this,projectRow,cancellationToken);
    public IAsyncEnumerable<TResult> QueryAsync<TArguments,TResult>(string moduleId,string queryId,string database,string schemaFingerprint,TArguments arguments,CancellationToken cancellationToken=default)=>this.client.QueryCore<TArguments,TResult>(moduleId,queryId,database,schemaFingerprint,arguments,this,cancellationToken);
    public Task BeginNestedAsync(CancellationToken cancellationToken=default)=>this.client.ChangeTransactionAsync(this,"begin",null,cancellationToken);
    public Task SavepointAsync(string name,CancellationToken cancellationToken=default)=>this.client.ChangeTransactionAsync(this,"save",name,cancellationToken);
    public Task RollbackToSavepointAsync(string name,CancellationToken cancellationToken=default)=>this.client.ChangeTransactionAsync(this,"rollback-to",name,cancellationToken);
    public Task<NativeTransactionOutcome> GetOutcomeAsync(CancellationToken cancellationToken=default){if(this.IsCompleted){return this.client.GetTransactionOutcomeAsync(this.Database,this.Id,cancellationToken);}return this.client.GetTransactionOutcomeAsync(this,cancellationToken);}
    public Task CommitAsync(CancellationToken cancellationToken=default)=>this.client.FinishTransactionAsync(this,true,cancellationToken);
    public Task RollbackAsync(CancellationToken cancellationToken=default)=>this.client.FinishTransactionAsync(this,false,cancellationToken);
    public async ValueTask DisposeAsync(){if(!this.IsCompleted){await this.RollbackAsync().ConfigureAwait(false);}}
}
