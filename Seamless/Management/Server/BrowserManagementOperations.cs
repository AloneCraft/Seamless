namespace Seamless.Management.Server;

/// <summary>画面の回線が残っていても、新しい管理操作の受付時にセッションを検査する。</summary>
public sealed partial class BrowserManagementOperations:IManagementOperations,IManagementSourceOperations,IManagedDatabaseSourceOperations{
    bool IManagedDatabaseSourceOperations.CanOpenManagedDatabaseSource=>
        this.operations is IManagedDatabaseSourceOperations{CanOpenManagedDatabaseSource:true};
    Task<string> IManagedDatabaseSourceOperations.OpenManagedDatabaseSourceAsync(string id,CancellationToken cancellationToken)=>
        this.InvokeAsync(()=>((IManagedDatabaseSourceOperations)this.operations).OpenManagedDatabaseSourceAsync(id,cancellationToken));
    public Task<CatalogPage<CatalogSchema>> SchemasAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.SchemasAsync(request,cancellationToken));
    public Task<CatalogObjectDetails> GetObjectDetailsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.GetObjectDetailsAsync(request,cancellationToken));
    public Task<QueryTracePage> QueryTraceAsync(QueryTraceQuery query,CancellationToken cancellationToken=default)=>this.InvokeTraceAsync(()=>this.operations.QueryTraceAsync(query,cancellationToken));
    public Task<ResourceHeatReadResult> QueryResourceHeatAsync(ResourceHeatQuery query,CancellationToken cancellationToken=default)=>this.InvokeTraceAsync(()=>this.operations.QueryResourceHeatAsync(query,cancellationToken));
    public Task<QueryTraceWatchResult> WatchQueryTraceAsync(QueryTraceWatchQuery query,CancellationToken cancellationToken=default)=>this.InvokeTraceAsync(()=>this.operations.WatchQueryTraceAsync(query,cancellationToken));
    private async Task<T> InvokeTraceAsync<T>(Func<Task<T>> operation){
        var id=await this.sessionId().ConfigureAwait(false);_=this.sessions.Authenticate(id);
        var result=await operation().ConfigureAwait(false);_=this.sessions.Authenticate(id);return result;
    }
    public Task<QueryExecutionHistoryPage> QueryExecutionHistoryAsync(QueryExecutionHistoryQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.QueryExecutionHistoryAsync(query,cancellationToken));
    public Task<AuthorizationScopeInfo> AuthorizationScopeAsync(AuthorizationScopeQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.AuthorizationScopeAsync(query,cancellationToken));
    public Task<AuthorizationChangeResult> ChangeAuthorizationScopeAsync(AuthorizationScopeEdit edit,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ChangeAuthorizationScopeAsync(edit.Freeze(),cancellationToken));
    public Task<DatabaseAuthorizationList> AuthorizationDatabasesAsync(DatabaseAuthorizationListQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.AuthorizationDatabasesAsync(query,cancellationToken));
    public Task<DatabaseAuthorizationInfo> DatabaseAuthorizationAsync(DatabaseAuthorizationQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.DatabaseAuthorizationAsync(query,cancellationToken));
    public Task<AuthorizationChangeResult> ChangeDatabaseAuthorizationAsync(DatabaseAuthorizationEdit edit,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ChangeDatabaseAuthorizationAsync(edit.Freeze(),cancellationToken));
    public Task<AuthorizationChangeResult> AuthorizationChangeResultAsync(Guid operationId,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.AuthorizationChangeResultAsync(operationId,cancellationToken));
    private readonly LocalBrowserSessions sessions;
    private readonly IManagementOperations operations;
    private readonly Func<Task<string?>> sessionId;
    public BrowserManagementOperations(LocalBrowserSessions sessions,IManagementOperations operations,Func<Task<string?>> sessionId){
        this.sessions=sessions??throw new ArgumentNullException(nameof(sessions));
        this.operations=operations??throw new ArgumentNullException(nameof(operations));
        this.sessionId=sessionId??throw new ArgumentNullException(nameof(sessionId));
    }
    private async Task<T> InvokeAsync<T>(Func<Task<T>> operation){
        _=this.sessions.Authenticate(await this.sessionId().ConfigureAwait(false));
        return await operation().ConfigureAwait(false);
    }
    private async Task InvokeAsync(Func<Task> operation){
        _=this.sessions.Authenticate(await this.sessionId().ConfigureAwait(false));
        await operation().ConfigureAwait(false);
    }
    public Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.InfoAsync(cancellationToken));
    public Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.operations.DatabasesAsync(cursor,versionToken,cancellationToken));
    public Task<CatalogDatabase> RenameDatabaseAsync(RenameDatabaseRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.operations.RenameDatabaseAsync(request,cancellationToken));
    public Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.operations.ObjectsAsync(request,cancellationToken));
    public Task<Draft> GetDraftAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.GetDraftAsync(id,cancellationToken));
    public Task<Draft?> FindDraftAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.FindDraftAsync(id,cancellationToken));
    public Task<Page<DraftSummary>> UnpublishedDatabasesAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.operations.UnpublishedDatabasesAsync(cursor,cancellationToken));
    public Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.GetDefinitionAsync(id,cancellationToken));
    public Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellationToken=default){
        var frozen=request with{Definition=request.Definition.Freeze()};
        return this.InvokeAsync(()=>this.operations.SaveDraftAsync(frozen,cancellationToken));
    }
    public Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.BuildAsync(key,cancellationToken));
    public Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.operations.CreateValidationDraftAsync(key,cancellationToken));
    public Task DropValidationAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.DropValidationAsync(id,cancellationToken));
    public Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.PreviewAsync(key,cancellationToken));
    public Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ApplyAsync(request,cancellationToken));
    public Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.GetOperationAsync(id,cancellationToken));
    public Task<CatalogPage<FunctionSummary>> FunctionsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.FunctionsAsync(request,cancellationToken));
    public Task<FunctionDetails> GetFunctionDetailsAsync(FunctionTarget target,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.GetFunctionDetailsAsync(target,cancellationToken));
    public Task<ObjectExecutionResult> ExecuteFunctionAsync(FunctionExecutionRequest request,CancellationToken cancellationToken=default){var frozen=request.Freeze();return this.InvokeAsync(()=>this.operations.ExecuteFunctionAsync(frozen,cancellationToken));}
    public Task<ObjectExecutionResult> ExecuteObjectAsync(ObjectExecutionRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ExecuteObjectAsync(request,cancellationToken));
    public Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ReadRowsAsync(request,cancellationToken));
    public Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.CommitRowsAsync(batch,cancellationToken));
    public Task<ExecutionPlanDocument[]> PlansAsync(string definitionId,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.PlansAsync(definitionId,cancellationToken));
    public Task<QueryPlanResult> QueryPlansAsync(QueryPlanRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.QueryPlansAsync(request,cancellationToken));
    public Task<CatalogPage<DefinitionSummary>> QueriesAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.QueriesAsync(request,cancellationToken));
    public Task<RecoveryGuideDocument> RecoveryGuideAsync(RecoveryScenario scenario,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.RecoveryGuideAsync(scenario,cancellationToken));
    public Task<ServerSettingsSnapshot> ServerSettingsAsync(CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ServerSettingsAsync(cancellationToken));
    public Task<ServerSettingsSnapshot> SaveServerSettingsAsync(ServerSettingsEdit request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.SaveServerSettingsAsync(request,cancellationToken));
    public Task<Page<UserProfile>> UserProfilesAsync(long cursor=0,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.UserProfilesAsync(cursor,cancellationToken));
    public Task<UserProfile> EditUserProfileAsync(UserProfileEdit request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.InvokeAsync(()=>this.operations.EditUserProfileAsync(frozen,cancellationToken));
    }
    public Task<AuditPage> AuditEventsAsync(AuditQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.AuditEventsAsync(query,cancellationToken));
    public Task<AuditReadPage> ReadSummariesAsync(AuditQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ReadSummariesAsync(query,cancellationToken));
    public Task<AuditDeliveryPage> AuditDeliveryStatusAsync(long cursor=0,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.AuditDeliveryStatusAsync(cursor,cancellationToken));
    public Task<AuditReadBufferStatus> ReadAuditStatusAsync(CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.operations.ReadAuditStatusAsync(cancellationToken));
    private IManagementSourceOperations Sources=>this.operations as IManagementSourceOperations??throw new ManagementException("Unsupported","この接続は開発ソースに対応していません。");
    public Task<SourceDirectory> BrowseSourceDirectoryAsync(string path,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Sources.BrowseSourceDirectoryAsync(path,cancellationToken));
    public Task<SourceProjectConflict> InspectSourceConflictAsync(SourceProjectConflictRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Sources.InspectSourceConflictAsync(request,cancellationToken));
    public Task<SourceProjectConflictResult> ResolveSourceConflictAsync(SourceProjectConflictResolution request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Sources.ResolveSourceConflictAsync(request,cancellationToken));
    public Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Sources.OpenSourceLocationAsync(path,cancellationToken));
    public Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Sources.OpenSourceProjectAsync(request,cancellationToken));
    public Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default){
        var frozen=request with{Baseline=request.Baseline.Freeze(),Source=request.Source.Freeze()};
        return this.InvokeAsync(()=>this.Sources.SynchronizeSourceProjectAsync(frozen,cancellationToken));
    }
    public Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.Sources.CreateSourceProjectAsync(request,cancellationToken));
    public Task<Draft> SaveSourceProjectDraftAsync(string id,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.Sources.SaveSourceProjectDraftAsync(id,cancellationToken));
    public Task<SourceProjectBuild> InspectSourceProjectBuildAsync(SourceProjectBuildRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.Sources.InspectSourceProjectBuildAsync(request,cancellationToken));
    public Task<ImportedSourceBuild> ImportSourceProjectBuildAsync(SourceProjectBuildImportRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.Sources.ImportSourceProjectBuildAsync(request,cancellationToken));
    public Task<ImportedSourceBuild> GetImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.Sources.GetImportedSourceBuildAsync(key,cancellationToken));
    public Task<ImportedSourceBuild?> FindImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync(()=>this.Sources.FindImportedSourceBuildAsync(key,cancellationToken));
}
