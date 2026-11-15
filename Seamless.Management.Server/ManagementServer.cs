using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;
using Seamless.Transport.Kestrel;

namespace Seamless.Management.Server;

public sealed partial class ManagementServer:IAsyncDisposable{
    private const int RequestReadMilliseconds=15000;
    private readonly ManagementService? managementService;
    private ManagementService service=>this.managementService??throw new ManagementException("Unsupported","このリスナーは共通認可の管理専用です。");
    private readonly ManagementAuthorizationApi? authorization;
    private readonly KestrelTcpHost? tcp;
    private readonly ConnectionQuota quota=new(32);
    private readonly FrameBufferQuota? bufferedRequests;
    private readonly FrameBufferQuota? pendingWrites;
    private readonly Func<int,IDisposable>? reserveWrite;
    private readonly TimeSpan? responseWriteTimeout;
    private readonly CancellationTokenSource stopping=new();
    private readonly ConcurrentDictionary<long,Task> connections=new();
    private long sequence;
    private bool started;
    private Task? pipeAccept;
    private readonly string? localUser;
    private readonly LocalOwnerIdentity? localOwner;
    private readonly LocalBrowserSessions? localBrowser;
    private readonly SourceProjectService sourceProjects=new();
    private ConcurrentManagementOperations? execution;
    private readonly global::Seamless.Execution.ExecutionScope? pipeResources;
    private readonly ResourceMemoryPool? pipeMemoryPool;
    internal long PipeBufferBytes=>this.pipeResources?.Budget.MemoryBytes??0;
    internal void ShareExecutionSlots(ConcurrentManagementOperations operations){
        ArgumentNullException.ThrowIfNull(operations);if(this.started){throw new InvalidOperationException("要求実行枠は受付の開始前に接続してください。");}this.execution=operations;
    }
    public string? PipeName{get;}
    internal string? UnixPipePath{get;}
    internal long BufferedRequestBytes=>this.bufferedRequests?.UsedBytes??0;
    internal long PendingWriteBytes=>this.pendingWrites?.UsedBytes??0;
    public event Action<Exception>? ConnectionFailed;
    public static ManagementServer ForLocalOwner(ManagementService service)=>CreateLocalOwner(service,null,null,null);
    public static ManagementServer ForLocalOwner(ManagementService service,LocalBrowserSessions browser)=>CreateLocalOwner(service,null,null,browser);
    public static ManagementServer ForLocalOwner(ManagementService service,X509Certificate2 certificate,IPEndPoint endpoint,LocalBrowserSessions? browser=null)=>
        CreateLocalOwner(service,certificate,endpoint,browser);
    public static ManagementServer ForLocalOwner(ManagementService service,X509Certificate2 certificate,IPEndPoint endpoint,LocalBrowserSessions browser,long? maximumBufferedRequestBytes,long? maximumPendingWriteBytes,TimeSpan? responseWriteTimeout)=>
        CreateLocalOwner(service,certificate,endpoint,browser,maximumBufferedRequestBytes,maximumPendingWriteBytes,responseWriteTimeout);
    private static ManagementServer CreateLocalOwner(ManagementService service,X509Certificate2? certificate,IPEndPoint? endpoint,LocalBrowserSessions? browser,long? maximumBufferedRequestBytes=4*1024*1024,long? maximumPendingWriteBytes=4*1024*1024,TimeSpan? responseWriteTimeout=null){
        ArgumentNullException.ThrowIfNull(service);
        if(browser is not null&&browser.InstanceId!=service.Info.InstanceId){throw new ArgumentException("ブラウザーセッションのインスタンスが一致しません。",nameof(browser));}
        return new(service,null,certificate,endpoint,null,maximumBufferedRequestBytes,maximumPendingWriteBytes,responseWriteTimeout??TimeSpan.FromSeconds(15),LocalOwnerIdentity.Capture(),browser);
    }
    public ManagementServer(ManagementService service,X509Certificate2 certificate,IPEndPoint endpoint,string? localUser=null):this(service,certificate,endpoint,localUser,null){}
    public ManagementServer(ManagementService service,X509Certificate2 certificate,IPEndPoint endpoint,string? localUser,long? maximumBufferedRequestBytes):this(service,certificate,endpoint,localUser,maximumBufferedRequestBytes,null,null){}
    public ManagementServer(ManagementService service,X509Certificate2 certificate,IPEndPoint endpoint,string? localUser,long? maximumBufferedRequestBytes,long? maximumPendingWriteBytes,TimeSpan? responseWriteTimeout):this(service,null,certificate,endpoint,localUser,maximumBufferedRequestBytes,maximumPendingWriteBytes,responseWriteTimeout){}
    internal static ManagementServer ForAuthorization(StoredAuthorization policy,Seamless.Authentication.OidcTokenVerifier tokens,X509Certificate2 certificate,IPEndPoint endpoint,
        Func<GatewayPublicationRequest,Seamless.Security.AuthorizationLease,GatewayPublicationIdentity>? gatewayPublication=null,
        Func<GatewayPublicationListRequest,Seamless.Security.AuthorizationLease,GatewayPublicationList>? gatewayPublications=null,
        Func<GatewayPublicationApprovalRequest,Seamless.Security.AuthorizationLease,bool,GatewayPublicationApprovalResult>? gatewayApproval=null,bool gatewayApprovalRegistration=false)=>
        new(null,new(policy,tokens,gatewayPublication,gatewayPublications,gatewayApproval,gatewayApprovalRegistration),certificate,endpoint,null,4*1024*1024,4*1024*1024,TimeSpan.FromSeconds(15));
    private ManagementServer(ManagementService? service,ManagementAuthorizationApi? authorization,X509Certificate2? certificate,IPEndPoint? endpoint,string? localUser,long? maximumBufferedRequestBytes,long? maximumPendingWriteBytes,TimeSpan? responseWriteTimeout,LocalOwnerIdentity? localOwner=null,LocalBrowserSessions? localBrowser=null){
        if(certificate is null&&localOwner is null){throw new ArgumentNullException(nameof(certificate));}
        if(certificate is not null&&!certificate.HasPrivateKey){throw new ArgumentException("秘密鍵を持つサーバー証明書が必要です。",nameof(certificate));}
        if(maximumBufferedRequestBytes is{} maximum){this.bufferedRequests=new(maximum);}
        if(maximumPendingWriteBytes is{} output){
            var budget=this.pendingWrites=new FrameBufferQuota(output);
            this.reserveWrite=bytes=>budget.TryReserve(bytes)??throw new ResponseDeliveryAborted("管理RPCの送信予算が使用中です。");
        }
        if(responseWriteTimeout is{} timeout&&(timeout<=TimeSpan.Zero||timeout.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(responseWriteTimeout));}
        this.responseWriteTimeout=responseWriteTimeout;
        this.managementService=service;this.authorization=authorization;this.localUser=localUser;this.localOwner=localOwner;this.localBrowser=localBrowser;
        if(certificate is not null){this.tcp=new(endpoint??throw new ArgumentNullException(nameof(endpoint)),this.ServeTcpAsync,certificate,this.quota,error=>this.ConnectionFailed?.Invoke(error));}
        if(localUser is not null){ManagementStore.ValidateAccountName(localUser);this.PipeName=LocalServerConnection.PipeNameFor(this.service.Info.InstanceId);}
        if(localOwner is not null){this.PipeName=LocalServerConnection.PipeNameFor(this.service.Info.InstanceId);}
        if(OperatingSystem.IsLinux()&&this.PipeName is not null){this.UnixPipePath=LocalServerConnection.PipeEndpointFor(this.PipeName);}
        if(this.PipeName is not null){this.pipeResources=this.service.CreateTransportResources();this.pipeMemoryPool=new(this.pipeResources);}
    }
    public int Port=>this.tcp?.Port??0;
    public void Start(){if(this.started){throw new InvalidOperationException("起動済みです。");}this.tcp?.StartAsync().GetAwaiter().GetResult();this.started=true;if(this.PipeName is not null){this.pipeAccept=this.AcceptPipeAsync();}}
    private async Task AcceptPipeAsync(){
        try{while(!this.stopping.IsCancellationRequested){
            var pipe=new NamedPipeServerStream(this.UnixPipePath??this.PipeName!,PipeDirection.InOut,32,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            try{await pipe.WaitForConnectionAsync(this.stopping.Token);}
            catch(UnauthorizedAccessException error){pipe.Dispose();this.ConnectionFailed?.Invoke(error);continue;}
            catch{pipe.Dispose();throw;}
            var permit=this.quota.TryEnter();if(permit is null){pipe.Dispose();continue;}
            var id=Interlocked.Increment(ref this.sequence);var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);this.connections[id]=completion.Task;
            _=Task.Run(async()=>{try{await using var transport=new PipeTransport(pipe,this.pipeMemoryPool!);await this.ServeConnectionAsync(pipe,this.localUser,new(transport,ManagementWire.MaximumBytes,true,null,null,null,null,this.pipeMemoryPool),pipe.Dispose,transport.ConnectionClosed);}finally{pipe.Dispose();permit.Dispose();this.connections.TryRemove(id,out _);completion.TrySetResult();}});
        }}catch(OperationCanceledException)when(this.stopping.IsCancellationRequested){}
    }
    private async Task ServeTcpAsync(ConnectionContext connection){using var stream=new DuplexPipeStream(connection.Transport);await this.ServeConnectionAsync(stream,null,new(connection.Transport,ManagementWire.MaximumBytes,bigEndian:true),connection.Abort,connection.ConnectionClosed);}
    // 接続は1要求ごとに閉じる。Manager はブラウザーからだけ使い、接続を保つ切替は持たない（2026-11-12 の決定）。
    private Task ServeConnectionAsync(Stream stream,string? localUser,LengthPrefixedPipe? frames=null,Action? abort=null,CancellationToken connectionClosed=default)=>
        this.ServeRequestAsync(stream,localUser,frames,abort,connectionClosed);
    private async Task ServeRequestAsync(Stream stream,string? localUser,LengthPrefixedPipe? frames,Action? abort,CancellationToken connectionClosed){
        RpcRequest? request=null;
        IDisposable? reservation=null;
        IDisposable? executionLease=null;
        Action<int>? admit=null;
        if(this.bufferedRequests is{} budget){
            admit=bytes=>reservation=budget.TryReserve(bytes)??throw new ManagementException("Busy","管理RPCの受信予算が使用中です。");
        }
        try{
            using(var readDeadline=CancellationTokenSource.CreateLinkedTokenSource(this.stopping.Token)){
                readDeadline.CancelAfter(RequestReadMilliseconds);
                if(frames is null){request=await ManagementWire.ReadRequestOrEndAsync(stream,readDeadline.Token,admit);}
                else{request=await frames.ReadAsync(ReadRequest,readDeadline.Token,null,admit);}
            }
            if(request is null){throw new EndOfStreamException();}
            LocalOwnerIdentity? owner=null;
            if(this.localOwner is not null&&stream is NamedPipeServerStream pipe){this.localOwner.Verify(pipe);owner=this.localOwner;}
            if(request.Operation=="info"){var info=this.authorization?.Info??this.service.Info;if(this.PipeName is not null){info=info with{Capabilities=[..info.Capabilities,"SameUserPipeV1"]};}if(this.localOwner is not null){info=info with{Capabilities=[..info.Capabilities,"OsOwnerPipeV1"]};}if(owner is not null){info=info with{Capabilities=[..info.Capabilities,"OwnerSourceProjectsV1","OwnerSourceDirectoryV1","OwnerSourceDraftsV1","OwnerSourceBuildsV1","OwnerSourceBuildImportsV1","OwnerSourceBuildPublicationV1","OwnerSourceConflictsV1","OwnerSourceConfigurationsV1"]};}if(owner is not null&&this.localBrowser is not null){info=info with{Capabilities=[..info.Capabilities,"LocalBrowserV1"]};}await this.Send(stream,request.Id,"result",info);return;}
            if(request.Operation is "browserLaunch" or "browserRevoke"){
                if(owner is null||this.localBrowser is null){throw new ManagementException("Unsupported","ブラウザー起動はOS所有者を確認したローカルIPCでのみ利用できます。");}
                if(request.Operation=="browserLaunch"){await this.Send(stream,request.Id,"result",this.localBrowser.CreateLaunch());}
                else{this.localBrowser.RevokeAll();await this.Send(stream,request.Id,"result",new{Completed=true});}
                return;
            }
            using var requestCancellation=CancellationTokenSource.CreateLinkedTokenSource(this.stopping.Token,connectionClosed);
            var operationToken=requestCancellation.Token;
            if(request.Operation is "sourceDirectory" or "sourceLocation" or "sourceProject" or "sourceSynchronize" or "sourceCreate" or "sourceSaveDraft" or "sourceInspectBuild" or "sourceImportBuild" or "sourceImportedBuild" or "sourceFindImportedBuild" or "sourceInspectConflict" or "sourceResolveConflict"){
                if(owner is null){throw new ManagementException("Permission","開発ソースはOS所有者を確認したローカルIPCでのみ操作できます。");}
                if(this.execution is{} sourceExecution){executionLease=await sourceExecution.EnterExecutionAsync(operationToken).ConfigureAwait(false);}
                using var sourceCancellation=CancellationTokenSource.CreateLinkedTokenSource(this.stopping.Token,connectionClosed);
                object sourceResult=request.Operation switch{
                    "sourceDirectory"=>await this.sourceProjects.BrowseSourceDirectoryAsync(request.Payload.GetProperty("path").GetString()!,sourceCancellation.Token),
                    "sourceInspectConflict"=>await this.sourceProjects.InspectSourceConflictAsync(ManagementJson.Read<SourceProjectConflictRequest>(request.Payload),sourceCancellation.Token),
                    "sourceResolveConflict"=>await this.sourceProjects.ResolveSourceConflictAsync(ManagementJson.Read<SourceProjectConflictResolution>(request.Payload),sourceCancellation.Token),
                    "sourceLocation"=>await this.sourceProjects.OpenSourceLocationAsync(request.Payload.GetProperty("path").GetString()!,sourceCancellation.Token),
                    "sourceProject"=>await this.sourceProjects.OpenSourceProjectAsync(ManagementJson.Read<SourceProjectOpenRequest>(request.Payload),sourceCancellation.Token),
                    "sourceCreate"=>await this.sourceProjects.CreateSourceProjectAsync(ManagementJson.Read<SourceProjectCreateRequest>(request.Payload),
                        (id,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(this.service.GetDraft(owner.Account(),id));},sourceCancellation.Token),
                    "sourceSaveDraft"=>await this.SaveOwnerSourceDraftAsync(request.Payload.GetProperty("id").GetString()!,owner,sourceCancellation.Token),
                    "sourceInspectBuild"=>await this.sourceProjects.InspectBuildAsync(ManagementJson.Read<SourceProjectBuildRequest>(request.Payload),
                        (id,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(this.service.GetDraft(owner.Account(),id));},SourceBuildArtifactInspector.Inspect,sourceCancellation.Token),
                    "sourceImportBuild"=>await this.ImportOwnerSourceBuildAsync(ManagementJson.Read<SourceProjectBuildImportRequest>(request.Payload),owner,sourceCancellation.Token),
                    "sourceImportedBuild"=>this.service.GetImportedSourceBuild(owner.Account(),ManagementJson.Read<DraftKey>(request.Payload)),
                    "sourceFindImportedBuild"=>new SourceBuildLookup(this.service.FindImportedSourceBuild(owner.Account(),ManagementJson.Read<DraftKey>(request.Payload))),
                    _=>await this.sourceProjects.SynchronizeSourceProjectAsync(ManagementJson.Read<SourceProjectSynchronization>(request.Payload),sourceCancellation.Token)
                };
                await this.Send(stream,request.Id,"result",sourceResult);return;
            }
            if(this.authorization is{} authorization){
                using var operationCancellation=CancellationTokenSource.CreateLinkedTokenSource(this.stopping.Token,connectionClosed);
                var authorizedResult=await authorization.InvokeAsync(request,operationCancellation.Token);await this.Send(stream,request.Id,"result",authorizedResult);return;
            }
            if(request.Operation=="login"){await this.Send(stream,request.Id,"result",this.service.Login(ManagementJson.Read<LoginRequest>(request.Payload)));return;}
            UserAccount Authenticate(){if(owner is not null){return owner.Account();}if(localUser is not null){return this.service.AuthenticateLocal(localUser);}return this.service.Authenticate(request.Token);}
            var user=Authenticate();
            if(request.Operation=="localIdentity"){if(localUser is null&&owner is null){throw new ManagementException("Unsupported","この接続はローカルIPCではありません。");}await this.Send(stream,request.Id,"result",new UserInfo(user.Name,user.Grants,user.Disabled));return;}
            // 満杯の実行枠を待たず、処理中の操作を取り消せる制御経路を維持する。
            if(request.Operation=="cancel"){await this.Send(stream,request.Id,"result",this.Cancel(user,request.Payload),operationToken);return;}
            if(request.Operation is "queryTrace" or "watchQueryTrace" or "queryResourceHeat"){
                if(this.execution is{} traceAdmission){executionLease=await traceAdmission.EnterExecutionAsync(operationToken).ConfigureAwait(false);}
                user=Authenticate();
                async Task<object> ReadTraceAsync(){
                    if(request.Operation=="queryResourceHeat"){return await this.service.QueryResourceHeatAsync(user,ManagementJson.Read<ResourceHeatQuery>(request.Payload),operationToken).ConfigureAwait(false);}
                    if(request.Operation=="queryTrace"){return await this.service.QueryTraceAsync(user,ManagementJson.Read<QueryTraceQuery>(request.Payload),operationToken).ConfigureAwait(false);}
                    using var watch=this.service.BeginQueryTraceWatch(user,ManagementJson.Read<QueryTraceWatchQuery>(request.Payload));
                    return await watch.WaitAsync(operationToken).ConfigureAwait(false);
                }
                var pendingTrace=ReadTraceAsync();
                executionLease?.Dispose();executionLease=null;
                var traceResult=await pendingTrace.ConfigureAwait(false);
                if(this.execution is{} traceCompletion){executionLease=await traceCompletion.EnterExecutionAsync(operationToken).ConfigureAwait(false);}
                operationToken.ThrowIfCancellationRequested();this.service.AuthorizeQueryTrace(Authenticate());
                await this.Send(stream,request.Id,"result",traceResult,operationToken);return;
            }
            if(this.execution is{} executionSlots){executionLease=await executionSlots.EnterExecutionAsync(operationToken).ConfigureAwait(false);}
            operationToken.ThrowIfCancellationRequested();user=Authenticate();
            if(request.Operation=="execute"){
                var cancellationToken=operationToken;
                foreach(var row in this.service.Execute(user,ManagementJson.Read<ExecuteRequest>(request.Payload),cancellationToken)){
                    await this.Send(stream,request.Id,"row",row,cancellationToken);
                }
                await this.Send(stream,request.Id,"complete",new{Complete=true},cancellationToken);
                return;
            }
            object result=request.Operation switch{
                "auditEvents"=>this.service.AuditEvents(user,ManagementJson.Read<AuditQuery>(request.Payload)),
                "queryExecutionHistory"=>await this.service.QueryExecutionHistoryAsync(user,ManagementJson.Read<QueryExecutionHistoryQuery>(request.Payload),operationToken),
                "auditSummaries"=>this.service.ReadSummaries(user,ManagementJson.Read<AuditQuery>(request.Payload)),
                "catalogDatabases"=>this.service.Databases(user,Cursor(request.Payload),request.Payload.TryGetProperty("versionToken",out var catalogVersion)?catalogVersion.GetString()??"":""),
                "renameDatabase"=>this.service.RenameDatabase(user,ManagementJson.Read<RenameDatabaseRequest>(request.Payload),operationToken),
                "catalogSchemas"=>this.service.Schemas(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogObjects"=>this.service.Objects(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogQueries"=>this.service.Queries(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogDetails"=>this.service.ObjectDetails(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogPublishedDetails"=>this.service.PublishedObjectDetails(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "catalogSource"=>this.service.ObjectSource(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "definitions"=>this.service.Definitions(user,Cursor(request.Payload)),
                "definition"=>this.service.GetDefinition(user,Id(request.Payload)),
                "definitionInfo"=>this.service.DefinitionInfo(user,Id(request.Payload)),
                "draft"=>this.service.GetDraft(user,Id(request.Payload)),
                "findDraft"=>new DraftLookup(this.service.FindDraft(user,Id(request.Payload))),
                "drafts"=>this.service.Drafts(user,Cursor(request.Payload)),
                "saveDraft"=>this.service.SaveDraft(user,ManagementJson.Read<SaveDraftRequest>(request.Payload)),
                "build"=>this.service.Build(user,ManagementJson.Read<DraftKey>(request.Payload)),
                "analyze"=>this.service.Analyze(user,ManagementJson.Read<Definition>(request.Payload)),
                "version"=>this.service.Version(user,Id(request.Payload),request.Payload.GetProperty("version").GetInt64()),
                "preview"=>this.service.Preview(user,ManagementJson.Read<DraftKey>(request.Payload)),
                "previewTable"=>this.service.PreviewTable(user,ManagementJson.Read<TableDraftKey>(request.Payload)),
                "apply"=>this.service.Apply(user,ManagementJson.Read<ApplyRequest>(request.Payload)),
                "applyTable"=>this.service.ApplyTable(user,ManagementJson.Read<ApplyTableRequest>(request.Payload)),
                "operation"=>this.service.GetOperation(user,ManagementJson.Read<Guid>(request.Payload)),
                "validation"=>this.Validation(user,request.Payload),
                "validations"=>this.service.Validations(user,Cursor(request.Payload)),
                "dropValidation"=>this.DropValidation(user,request.Payload),
                "plans"=>this.service.Plans(user,Id(request.Payload)),
                "queryPlans"=>this.service.QueryPlans(user,ManagementJson.Read<QueryPlanRequest>(request.Payload)),
                "publishedAnalysis"=>this.service.AnalyzePublished(user,Id(request.Payload)),
                "history"=>this.service.History(user,Cursor(request.Payload)),
                "auditDelivery"=>this.service.AuditDeliveryStatus(user,Cursor(request.Payload)),
                "auditReadBuffer"=>this.service.ReadAuditStatus(user),
                "users"=>this.service.Users(user,Cursor(request.Payload)),
                "serverSettings"=>this.service.ServerSettings(user),
                "authorizationDatabases"=>await this.service.ManageAuthorizationAsync<DatabaseAuthorizationList>(user,request.Operation,ManagementJson.Read<DatabaseAuthorizationListQuery>(request.Payload),operationToken),
                "authorizationDatabase"=>await this.service.ManageAuthorizationAsync<DatabaseAuthorizationInfo>(user,request.Operation,ManagementJson.Read<DatabaseAuthorizationQuery>(request.Payload),operationToken),
                "authorizationScope"=>await this.service.ManageAuthorizationAsync<AuthorizationScopeInfo>(user,request.Operation,ManagementJson.Read<AuthorizationScopeQuery>(request.Payload),operationToken),
                "authorizationScopeChange"=>await this.service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,request.Operation,ManagementJson.Read<AuthorizationScopeEdit>(request.Payload),operationToken),
                "authorizationChange"=>await this.service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,request.Operation,ManagementJson.Read<DatabaseAuthorizationEdit>(request.Payload),operationToken),
                "authorizationOperation"=>await this.service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,request.Operation,ManagementJson.Read<Guid>(request.Payload),operationToken),
                "saveServerSettings"=>this.service.SaveServerSettings(user,ManagementJson.Read<ServerSettingsEdit>(request.Payload)),
                "userProfiles"=>this.service.UserProfiles(user,Cursor(request.Payload)),
                "editUserProfile"=>this.service.EditUserProfile(user,ManagementJson.Read<UserProfileEdit>(request.Payload)),
                "seedBatch"=>this.SeedBatch(user,request.Payload),
                "seed"=>this.Seed(user,request.Payload),
                "readRows"=>await this.service.ReadRowsAsync(user,ManagementJson.Read<TableRowsRequest>(request.Payload),operationToken),
                "functions"=>this.service.Functions(user,ManagementJson.Read<CatalogRequest>(request.Payload)),
                "functionDetails"=>this.service.GetFunctionDetails(user,ManagementJson.Read<FunctionTarget>(request.Payload)),
                "executeFunction"=>await this.service.ExecuteFunctionAsync(user,ManagementJson.Read<FunctionExecutionRequest>(request.Payload),operationToken),
                "executeObject"=>await this.service.ExecuteObjectAsync(user,ManagementJson.Read<ObjectExecutionRequest>(request.Payload),operationToken),
                "commitRows"=>await this.service.CommitRowsAsync(user,ManagementJson.Read<TableRowBatch>(request.Payload),operationToken),
                "changeUser"=>this.ChangeUser(user,request.Payload),
                "changePassword"=>this.ChangePassword(user,request.Payload),
                _=>throw new ManagementException("Unsupported","未対応の管理操作です。")
            };
            await this.Send(stream,request.Id,"result",result);
            return;
        }catch(OperationCanceledException)when(connectionClosed.IsCancellationRequested){
            abort?.Invoke();
        }catch(ResponseDeliveryAborted error){
            abort?.Invoke();
            this.ConnectionFailed?.Invoke(error);
        }catch(Exception error){
            if(request is null){this.ConnectionFailed?.Invoke(error);return;}var actual=error;if(error is System.Reflection.TargetInvocationException{InnerException:{} inner}){actual=inner;}
            var code="OperationFailed";if(actual is ManagementException management){code=management.Code;}else if(actual is Seamless.Security.AuthorizationException denied){code=denied.Code;}else if(actual is Seamless.Authentication.OidcAuthenticationException authentication){code=authentication.Code;}else if(actual is OperationCanceledException){code="Cancelled";}
            else if(request.Operation=="commitRows"&&actual is Seamless.Database.ForeignKeyViolationException){code="Constraint";}
            else if(request.Operation=="commitRows"&&actual is JsonException or FormatException or OverflowException){code="Validation";}
            try{await this.WriteResponseAsync(stream,new RpcResponse(request.Id,"error",ManagementJson.Element(new{}),code,actual.Message));}
            catch(ResponseDeliveryAborted){abort?.Invoke();}
        }finally{executionLease?.Dispose();reservation?.Dispose();}
    }
    private static RpcRequest ReadRequest(System.Buffers.ReadOnlySequence<byte> bytes){var reader=new Utf8JsonReader(bytes);var request=JsonSerializer.Deserialize<RpcRequest>(ref reader,ManagementJson.Options)??throw new ManagementException("Protocol","要求がありません。");if(reader.Read()){throw new ManagementException("Protocol","要求の末尾に余分なJSONがあります。");}return request;}
    private object SeedBatch(UserAccount user,JsonElement payload){this.service.SeedBatch(user,ManagementJson.Read<SeedBatchRequest>(payload));return new{success=true};}
    private object Cancel(UserAccount user,JsonElement payload){this.service.Cancel(user,ManagementJson.Read<Guid>(payload));return new{Accepted=true};}
    private object Seed(UserAccount user,JsonElement payload){this.service.Seed(user,ManagementJson.Read<SeedRequest>(payload));return new{Completed=true};}
    private object DropValidation(UserAccount user,JsonElement payload){this.service.DropValidation(user,Id(payload));return new{Completed=true};}
    private object ChangeUser(UserAccount user,JsonElement payload){this.service.ChangeUser(user,ManagementJson.Read<ChangeUserRequest>(payload));return new{Completed=true};}
    private object ChangePassword(UserAccount user,JsonElement payload){this.service.ChangePassword(user,ManagementJson.Read<ChangePasswordRequest>(payload));return new{Completed=true};}
    private object Validation(UserAccount user,JsonElement payload){
        if(payload.TryGetProperty("draftRevision",out var revision)&&revision.ValueKind==JsonValueKind.Number){
            string? bundleHash=null;if(payload.TryGetProperty("bundleHash",out var bundle)&&bundle.ValueKind!=JsonValueKind.Null){bundleHash=bundle.GetString();}
            return this.service.CreateValidationDraft(user,new(Id(payload),revision.GetInt64()){BundleHash=bundleHash});
        }
        return this.service.CreateValidation(user,Id(payload));
    }
    private static string Id(JsonElement payload)=>payload.GetProperty("id").GetString()??throw new ManagementException("InvalidRequest","IDが必要です。");
    private static long Cursor(JsonElement payload){if(payload.TryGetProperty("cursor",out var cursor)){return cursor.GetInt64();}return 0;}
    private Task Send(Stream stream,Guid id,string kind,object value,CancellationToken? cancellationToken=null)=>
        this.WriteResponseAsync(stream,new(id,kind,ManagementJson.Element(value)),cancellationToken);
    private async Task WriteResponseAsync(Stream stream,RpcResponse response,CancellationToken? cancellationToken=null){
        var cancellation=cancellationToken??this.stopping.Token;
        using var deadline=this.responseWriteTimeout.HasValue?CancellationTokenSource.CreateLinkedTokenSource(cancellation):null;
        if(this.responseWriteTimeout is{} timeout){deadline!.CancelAfter(timeout);}
        try{await ManagementWire.WriteAsync(stream,response,deadline?.Token??cancellation,this.reserveWrite);}
        catch(IOException error)when(error is not ResponseDeliveryAborted){throw new ResponseDeliveryAborted("管理RPCの応答を送信できませんでした。",error);}
        catch(OperationCanceledException error){throw new ResponseDeliveryAborted("管理RPCの応答送信を取り消しました。",error);}
    }
    private sealed class ResponseDeliveryAborted(string message,Exception? cause=null):IOException(message,cause);
    private async Task<ImportedSourceBuild> ImportOwnerSourceBuildAsync(SourceProjectBuildImportRequest request,LocalOwnerIdentity owner,CancellationToken cancellationToken){
        var user=owner.Account();var artifacts=this.service.SourceBuilds;
        var build=await this.sourceProjects.InspectBuildAsync(request.Build,
            (id,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(this.service.GetDraft(user,id));},
            (context,path,token)=>artifacts.Capture(context,path,request.ExpectedBundleHash,token),cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return this.service.BindImportedSourceBuild(user,build);
    }
    private Task<Draft> SaveOwnerSourceDraftAsync(string id,LocalOwnerIdentity owner,CancellationToken cancellationToken){
        var user=owner.Account();
        return this.sourceProjects.SaveDatabaseDraftAsync(id,
            (key,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(this.service.GetDefinition(user,key));},
            (key,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(this.service.FindDraft(user,key));},
            (request,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(this.service.SaveDraft(user,request));},cancellationToken);
    }
    public async ValueTask DisposeAsync(){
        this.localBrowser?.Dispose();this.stopping.Cancel();this.managementService?.RequestStop();
        try{if(this.tcp is not null){await this.tcp.DisposeAsync();}}
        finally{
            try{if(this.pipeAccept is not null){await this.pipeAccept;}await Task.WhenAll(this.connections.Values);this.sourceProjects.Dispose();}
            finally{
                this.pipeMemoryPool?.Dispose();
                try{this.pipeResources?.Dispose();}finally{this.stopping.Dispose();}
            }
        }
    }
}
