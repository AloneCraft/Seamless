namespace Seamless.Management.Server;

/// <summary>ブラウザー要求ごとに利用者を再検査し、共通サーバーの実行枠で管理処理を行う。</summary>
internal sealed class AuthenticatedManagementOperations(ConcurrentManagementOperations execution,UserBrowserSessions sessions,Func<Task<string?>> sessionId):IManagementOperations{
    public Task<CatalogPage<CatalogSchema>> SchemasAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Schemas(user,request),cancellationToken);
    public Task<CatalogObjectDetails> GetObjectDetailsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.PublishedObjectDetails(user,request),cancellationToken);
    public Task<QueryTracePage> QueryTraceAsync(QueryTraceQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.QueryTraceAsync(user,query,cancellationToken),cancellationToken).Unwrap();
    public Task<ResourceHeatReadResult> QueryResourceHeatAsync(ResourceHeatQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.QueryResourceHeatAsync(user,query,cancellationToken),cancellationToken).Unwrap();
    public async Task<QueryTraceWatchResult> WatchQueryTraceAsync(QueryTraceWatchQuery query,CancellationToken cancellationToken=default){
        var id=await sessionId().ConfigureAwait(false);
        return await execution.WatchAuthenticatedTraceAsync(()=>sessions.Account(id),query,cancellationToken).ConfigureAwait(false);
    }
    public Task<QueryExecutionHistoryPage> QueryExecutionHistoryAsync(QueryExecutionHistoryQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync((service,user)=>service.QueryExecutionHistoryAsync(user,query,cancellationToken),cancellationToken);
    public Task<AuthorizationScopeInfo> AuthorizationScopeAsync(AuthorizationScopeQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationScopeInfo>(user,"authorizationScope",query,cancellationToken),cancellationToken);
    public Task<AuthorizationChangeResult> ChangeAuthorizationScopeAsync(AuthorizationScopeEdit edit,CancellationToken cancellationToken=default){var frozen=edit.Freeze();return this.InvokeTaskAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,"authorizationScopeChange",frozen,cancellationToken),cancellationToken);}
    public Task<DatabaseAuthorizationList> AuthorizationDatabasesAsync(DatabaseAuthorizationListQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync((service,user)=>service.ManageAuthorizationAsync<DatabaseAuthorizationList>(user,"authorizationDatabases",query,cancellationToken),cancellationToken);
    public Task<DatabaseAuthorizationInfo> DatabaseAuthorizationAsync(DatabaseAuthorizationQuery query,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync((service,user)=>service.ManageAuthorizationAsync<DatabaseAuthorizationInfo>(user,"authorizationDatabase",query,cancellationToken),cancellationToken);
    public Task<AuthorizationChangeResult> ChangeDatabaseAuthorizationAsync(DatabaseAuthorizationEdit edit,CancellationToken cancellationToken=default){var frozen=edit.Freeze();return this.InvokeTaskAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,"authorizationChange",frozen,cancellationToken),cancellationToken);}
    public Task<AuthorizationChangeResult> AuthorizationChangeResultAsync(Guid operationId,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,"authorizationOperation",operationId,cancellationToken),cancellationToken);
    private async Task<T> InvokeAsync<T>(Func<ManagementService,UserAccount,T> operation,CancellationToken cancellationToken){
        var id=await sessionId().ConfigureAwait(false);
        return await execution.InvokeAuthenticatedAsync(()=>sessions.Account(id),operation,cancellationToken).ConfigureAwait(false);
    }
    private async Task<T> InvokeTaskAsync<T>(Func<ManagementService,UserAccount,Task<T>> operation,CancellationToken cancellationToken){
        var id=await sessionId().ConfigureAwait(false);
        return await execution.InvokeAuthenticatedTaskAsync(()=>sessions.Account(id),operation,cancellationToken).ConfigureAwait(false);
    }
    public Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default)=>this.InvokeAsync((service,_)=>service.Info,cancellationToken);
    public Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Databases(user,cursor,versionToken),cancellationToken);
    public Task<CatalogDatabase> RenameDatabaseAsync(RenameDatabaseRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.RenameDatabase(user,request,cancellationToken),cancellationToken);
    public Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Objects(user,request),cancellationToken);
    public Task<Draft> GetDraftAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.GetDraft(user,id),cancellationToken);
    public Task<Draft?> FindDraftAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.FindDraft(user,id),cancellationToken);
    public Task<Page<DraftSummary>> UnpublishedDatabasesAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.UnpublishedDatabases(user,cursor),cancellationToken);
    public Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.GetDefinition(user,id),cancellationToken);
    public Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellationToken=default){
        var frozen=request with{Definition=request.Definition.Freeze()};
        return this.InvokeAsync((service,user)=>service.SaveDraft(user,frozen),cancellationToken);
    }
    public Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Build(user,key),cancellationToken);
    public Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.CreateValidationDraft(user,key),cancellationToken);
    public Task DropValidationAsync(string id,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>{service.DropValidation(user,id);return true;},cancellationToken);
    public Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Preview(user,key),cancellationToken);
    public Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Apply(user,request),cancellationToken);
    public Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.GetOperation(user,id),cancellationToken);
    public Task<CatalogPage<FunctionSummary>> FunctionsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Functions(user,request),cancellationToken);
    public Task<FunctionDetails> GetFunctionDetailsAsync(FunctionTarget target,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.GetFunctionDetails(user,target),cancellationToken);
    public Task<ObjectExecutionResult> ExecuteFunctionAsync(FunctionExecutionRequest request,CancellationToken cancellationToken=default){var frozen=request.Freeze();return this.InvokeTaskAsync((service,user)=>service.ExecuteFunctionAsync(user,frozen,cancellationToken),cancellationToken);}
    public Task<ObjectExecutionResult> ExecuteObjectAsync(ObjectExecutionRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeTaskAsync((service,user)=>service.ExecuteObjectAsync(user,request,cancellationToken),cancellationToken);
    public Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeTaskAsync((service,user)=>service.ReadRowsAsync(user,request,cancellationToken),cancellationToken);
    public Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellationToken=default)=>
        this.InvokeTaskAsync((service,user)=>service.CommitRowsAsync(user,batch,cancellationToken),cancellationToken);
    public Task<ExecutionPlanDocument[]> PlansAsync(string definitionId,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Plans(user,definitionId).Plans,cancellationToken);
    public Task<QueryPlanResult> QueryPlansAsync(QueryPlanRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.QueryPlans(user,request),cancellationToken);
    public Task<CatalogPage<DefinitionSummary>> QueriesAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Queries(user,request),cancellationToken);
    public Task<RecoveryGuideDocument> RecoveryGuideAsync(RecoveryScenario scenario,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.RecoveryGuide(user,scenario),cancellationToken);
    public Task<ServerSettingsSnapshot> ServerSettingsAsync(CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.ServerSettings(user),cancellationToken);
    public Task<ServerSettingsSnapshot> SaveServerSettingsAsync(ServerSettingsEdit request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.SaveServerSettings(user,request),cancellationToken);
    public Task<Page<UserProfile>> UserProfilesAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.UserProfiles(user,cursor),cancellationToken);
    public Task<UserProfile> EditUserProfileAsync(UserProfileEdit request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.InvokeAsync((service,user)=>service.EditUserProfile(user,frozen),cancellationToken);
    }
    public Task<AuditPage> AuditEventsAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.AuditEvents(user,query),cancellationToken);
    public Task<AuditReadPage> ReadSummariesAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.ReadSummaries(user,query),cancellationToken);
    public Task<AuditDeliveryPage> AuditDeliveryStatusAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.AuditDeliveryStatus(user,cursor),cancellationToken);
    public Task<AuditReadBufferStatus> ReadAuditStatusAsync(CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.ReadAuditStatus(user),cancellationToken);
}
