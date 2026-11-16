namespace Seamless.Management.Server;

/// <summary>同居サーバーの管理処理を、同じServiceへ有界に配送する。</summary>
public sealed partial class ConcurrentManagementOperations:IManagementOperations,IManagementSourceOperations,IAsyncDisposable{
    public Task<QueryExecutionHistoryPage> QueryExecutionHistoryAsync(QueryExecutionHistoryQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync(()=>this.service.QueryExecutionHistoryAsync(this.owner,query,cancellationToken),cancellationToken);
    public Task<AuthorizationScopeInfo> AuthorizationScopeAsync(AuthorizationScopeQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync(()=>this.service.ManageAuthorizationAsync<AuthorizationScopeInfo>(this.owner,"authorizationScope",query,cancellationToken),cancellationToken);
    public Task<AuthorizationChangeResult> ChangeAuthorizationScopeAsync(AuthorizationScopeEdit edit,CancellationToken cancellationToken=default){var frozen=edit.Freeze();return this.InvokeTaskAsync(()=>this.service.ManageAuthorizationAsync<AuthorizationChangeResult>(this.owner,"authorizationScopeChange",frozen,cancellationToken),cancellationToken);}
    public Task<DatabaseAuthorizationList> AuthorizationDatabasesAsync(DatabaseAuthorizationListQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync(()=>this.service.ManageAuthorizationAsync<DatabaseAuthorizationList>(this.owner,"authorizationDatabases",query,cancellationToken),cancellationToken);
    public Task<DatabaseAuthorizationInfo> DatabaseAuthorizationAsync(DatabaseAuthorizationQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync(()=>this.service.ManageAuthorizationAsync<DatabaseAuthorizationInfo>(this.owner,"authorizationDatabase",query,cancellationToken),cancellationToken);
    public Task<AuthorizationChangeResult> ChangeDatabaseAuthorizationAsync(DatabaseAuthorizationEdit edit,CancellationToken cancellationToken=default){var frozen=edit.Freeze();return this.InvokeTaskAsync(()=>this.service.ManageAuthorizationAsync<AuthorizationChangeResult>(this.owner,"authorizationChange",frozen,cancellationToken),cancellationToken);}
    public Task<AuthorizationChangeResult> AuthorizationChangeResultAsync(Guid operationId,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync(()=>this.service.ManageAuthorizationAsync<AuthorizationChangeResult>(this.owner,"authorizationOperation",operationId,cancellationToken),cancellationToken);
    private readonly ManagementService service;
    public Task<CatalogPage<CatalogSchema>> SchemasAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Schemas(this.owner,request),cancellationToken);
    public Task<CatalogObjectDetails> GetObjectDetailsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.PublishedObjectDetails(this.owner,request),cancellationToken);
    private readonly UserAccount owner=LocalOwnerIdentity.Capture().Account();
    private readonly SourceProjectService sources=new();
    private readonly SemaphoreSlim slots;
    private readonly int capacity;
    private readonly CancellationTokenSource stopping=new();
    private readonly object disposalGate=new();
    private Task? disposal;
    internal Action? BeforeExecutionAdmission{get;set;}
    internal Action? ExecutionAdmissionCancelled{get;set;}
    public ConcurrentManagementOperations(ManagementService service,int capacity=32){
        ArgumentNullException.ThrowIfNull(service);
        if(capacity is <1 or >1024){throw new ArgumentOutOfRangeException(nameof(capacity));}
        this.service=service;this.capacity=capacity;this.slots=new(capacity,capacity);
    }
    private async Task<T> InvokeAsync<T>(Func<T> operation,CancellationToken cancellationToken){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        using var lease=await this.EnterExecutionAsync(cancellation.Token).ConfigureAwait(false);
        return await Task.Run(operation,cancellation.Token).ConfigureAwait(false);
    }
    private async Task<T> InvokeTaskAsync<T>(Func<Task<T>> operation,CancellationToken cancellationToken){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        using var lease=await this.EnterExecutionAsync(cancellation.Token).ConfigureAwait(false);
        //同期的な復旧・DB操作の開始もUIの同期コンテキストから離す。
        return await Task.Run(operation,cancellation.Token).ConfigureAwait(false);
    }
    internal async ValueTask<IDisposable> EnterExecutionAsync(CancellationToken cancellationToken){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        this.BeforeExecutionAdmission?.Invoke();
        try{await this.slots.WaitAsync(cancellation.Token).ConfigureAwait(false);}
        catch(OperationCanceledException){this.ExecutionAdmissionCancelled?.Invoke();throw;}
        try{cancellation.Token.ThrowIfCancellationRequested();return new ExecutionLease(this.slots);}
        catch{this.slots.Release();throw;}
    }
    private sealed class ExecutionLease(SemaphoreSlim slots):IDisposable{
        private SemaphoreSlim? owner=slots;
        public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.Release();
    }
    internal Task<T> InvokeAuthenticatedAsync<T>(Func<UserAccount> authenticate,Func<ManagementService,UserAccount,T> operation,CancellationToken cancellationToken)=>
        this.InvokeAsync(()=>operation(this.service,authenticate()),cancellationToken);
    internal Task<T> InvokeAuthenticatedTaskAsync<T>(Func<UserAccount> authenticate,Func<ManagementService,UserAccount,Task<T>> operation,CancellationToken cancellationToken)=>
        this.InvokeTaskAsync(()=>operation(this.service,authenticate()),cancellationToken);
    public Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Info with{Capabilities=[..this.service.Info.Capabilities,"OwnerSourceProjectsV1","OwnerSourceDirectoryV1","OwnerSourceDraftsV1","OwnerSourceBuildsV1","OwnerSourceSdkBuildV1","OwnerSourceBuildImportsV1","OwnerSourceBuildPublicationV1","OwnerSourceConflictsV1","OwnerSourceConfigurationsV1"]},cancellationToken);
    public Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Databases(this.owner,cursor,versionToken),cancellationToken);
    public Task<CatalogDatabase> RenameDatabaseAsync(RenameDatabaseRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.RenameDatabase(this.owner,request,cancellationToken),cancellationToken);
    public Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Objects(this.owner,request),cancellationToken);
    public Task<Draft> GetDraftAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.GetDraft(this.owner,id),cancellationToken);
    public Task<Draft?> FindDraftAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.FindDraft(this.owner,id),cancellationToken);
    public Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.GetDefinition(this.owner,id),cancellationToken);
    public Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellationToken=default){
        var frozen=request with{Definition=request.Definition.Freeze()};
        return this.InvokeAsync(()=>this.service.SaveDraft(this.owner,frozen),cancellationToken);
    }
    public Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Build(this.owner,key),cancellationToken);
    public Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.CreateValidationDraft(this.owner,key),cancellationToken);
    public Task DropValidationAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>{this.service.DropValidation(this.owner,id);return true;},cancellationToken);
    public Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Preview(this.owner,key),cancellationToken);
    public Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Apply(this.owner,request),cancellationToken);
    public Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.GetOperation(this.owner,id),cancellationToken);
    public Task<CatalogPage<FunctionSummary>> FunctionsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.Functions(this.owner,request),cancellationToken);
    public Task<FunctionDetails> GetFunctionDetailsAsync(FunctionTarget target,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.service.GetFunctionDetails(this.owner,target),cancellationToken);
    public Task<ObjectExecutionResult> ExecuteFunctionAsync(FunctionExecutionRequest request,CancellationToken cancellationToken=default){var frozen=request.Freeze();return this.InvokeTaskAsync(()=>this.service.ExecuteFunctionAsync(this.owner,frozen,cancellationToken),cancellationToken);}
    public Task<ObjectExecutionResult> ExecuteObjectAsync(ObjectExecutionRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeTaskAsync(()=>this.service.ExecuteObjectAsync(this.owner,request,cancellationToken),cancellationToken);
    public Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeTaskAsync(()=>this.service.ReadRowsAsync(this.owner,request,cancellationToken),cancellationToken);
    public Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellationToken=default)=>
        this.InvokeTaskAsync(()=>this.service.CommitRowsAsync(this.owner,batch,cancellationToken),cancellationToken);
    public Task<ExecutionPlanDocument[]> PlansAsync(string definitionId,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.Plans(this.owner,definitionId).Plans,cancellationToken);
    public Task<QueryPlanResult> QueryPlansAsync(QueryPlanRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.QueryPlans(this.owner,request),cancellationToken);
    public Task<CatalogPage<DefinitionSummary>> QueriesAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.Queries(this.owner,request),cancellationToken);
    public Task<ServerSettingsSnapshot> ServerSettingsAsync(CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.ServerSettings(this.owner),cancellationToken);
    public Task<ServerSettingsSnapshot> SaveServerSettingsAsync(ServerSettingsEdit request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.SaveServerSettings(this.owner,request),cancellationToken);
    public Task<Page<UserProfile>> UserProfilesAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.UserProfiles(this.owner,cursor),cancellationToken);
    public Task<UserProfile> EditUserProfileAsync(UserProfileEdit request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.InvokeAsync(()=>this.service.EditUserProfile(this.owner,frozen),cancellationToken);
    }
    public Task<AuditPage> AuditEventsAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.AuditEvents(this.owner,query),cancellationToken);
    public Task<AuditReadPage> ReadSummariesAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.ReadSummaries(this.owner,query),cancellationToken);
    public Task<AuditDeliveryPage> AuditDeliveryStatusAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.AuditDeliveryStatus(this.owner,cursor),cancellationToken);
    public Task<AuditReadBufferStatus> ReadAuditStatusAsync(CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.ReadAuditStatus(this.owner),cancellationToken);
    public Task<SourceDirectory> BrowseSourceDirectoryAsync(string path,CancellationToken cancellationToken=default)=>this.sources.BrowseSourceDirectoryAsync(path,cancellationToken);
    public Task<SourceProjectConflict> InspectSourceConflictAsync(SourceProjectConflictRequest request,CancellationToken cancellationToken=default)=>this.sources.InspectSourceConflictAsync(request,cancellationToken);
    public Task<SourceProjectConflictResult> ResolveSourceConflictAsync(SourceProjectConflictResolution request,CancellationToken cancellationToken=default)=>this.sources.ResolveSourceConflictAsync(request,cancellationToken);
    public Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default)=>this.sources.OpenSourceLocationAsync(path,cancellationToken);
    public Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default)=>this.sources.OpenSourceProjectAsync(request,cancellationToken);
    public Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default)=>this.sources.SynchronizeSourceProjectAsync(request,cancellationToken);
    public Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,CancellationToken cancellationToken=default)=>this.sources.CreateSourceProjectAsync(request,this.GetDraftAsync,cancellationToken);
    public Task<Draft> SaveSourceProjectDraftAsync(string id,CancellationToken cancellationToken=default)=>
        this.sources.SaveDatabaseDraftAsync(id,this,cancellationToken);
    public Task<SourceProjectBuild> InspectSourceProjectBuildAsync(SourceProjectBuildRequest request,CancellationToken cancellationToken=default)=>
        this.sources.InspectBuildAsync(request,this.GetDraftAsync,SourceBuildArtifactInspector.Inspect,cancellationToken);
    public async Task<ImportedSourceBuild> ImportSourceProjectBuildAsync(SourceProjectBuildImportRequest request,CancellationToken cancellationToken=default){
        var artifacts=this.service.SourceBuilds;
        var build=await this.sources.InspectBuildAsync(request.Build,this.GetDraftAsync,
            (context,path,token)=>artifacts.Capture(context,path,request.ExpectedBundleHash,token),cancellationToken).ConfigureAwait(false);
        return await this.InvokeAsync(()=>this.service.BindImportedSourceBuild(this.owner,build),cancellationToken).ConfigureAwait(false);
    }
    public Task<ImportedSourceBuild> GetImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.GetImportedSourceBuild(this.owner,key),cancellationToken);
    public Task<ImportedSourceBuild?> FindImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.service.FindImportedSourceBuild(this.owner,key),cancellationToken);
    public ValueTask DisposeAsync(){lock(this.disposalGate){return new(this.disposal??=this.DisposeCoreAsync());}}
    internal void StopAccepting()=>this.stopping.Cancel();
    private async Task DisposeCoreAsync(){
        this.StopAccepting();
        for(var index=0;index<this.capacity;index++){await this.slots.WaitAsync().ConfigureAwait(false);}
        this.sources.Dispose();
        this.slots.Dispose();this.stopping.Dispose();
    }
}
