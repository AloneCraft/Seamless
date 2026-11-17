namespace Seamless.Management.Server;

public sealed partial class InProcessManagementHost{
    public Task<QueryExecutionHistoryPage> QueryExecutionHistoryAsync(QueryExecutionHistoryQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.QueryExecutionHistoryAsync(user,query,cancellationToken),cancellationToken).AsTask().Unwrap();
    public Task<AuthorizationScopeInfo> AuthorizationScopeAsync(AuthorizationScopeQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationScopeInfo>(user,"authorizationScope",query,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();
    public Task<AuthorizationChangeResult> ChangeAuthorizationScopeAsync(AuthorizationScopeEdit edit,CancellationToken cancellationToken=default){var frozen=edit.Freeze();return this.InvokeAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,"authorizationScopeChange",frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();}
    public Task<DatabaseAuthorizationList> AuthorizationDatabasesAsync(DatabaseAuthorizationListQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.ManageAuthorizationAsync<DatabaseAuthorizationList>(user,"authorizationDatabases",query,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();
    public Task<DatabaseAuthorizationInfo> DatabaseAuthorizationAsync(DatabaseAuthorizationQuery query,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.ManageAuthorizationAsync<DatabaseAuthorizationInfo>(user,"authorizationDatabase",query,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();
    public Task<AuthorizationChangeResult> ChangeDatabaseAuthorizationAsync(DatabaseAuthorizationEdit edit,CancellationToken cancellationToken=default){var frozen=edit.Freeze();return this.InvokeAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,"authorizationChange",frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();}
    public Task<AuthorizationChangeResult> AuthorizationChangeResultAsync(Guid operationId,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.ManageAuthorizationAsync<AuthorizationChangeResult>(user,"authorizationOperation",operationId,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();
    public Task<CatalogPage<CatalogSchema>> SchemasAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Schemas(user,request),cancellationToken).AsTask();
    public Task<CatalogObjectDetails> GetObjectDetailsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.PublishedObjectDetails(user,request),cancellationToken).AsTask();
    public CatalogPage<CatalogObject> Objects(CatalogRequest request)=>this.Invoke((service,user)=>service.Objects(user,request));
    public CatalogDatabase RenameDatabase(RenameDatabaseRequest request)=>this.Invoke((service,user)=>service.RenameDatabase(user,request));
    public Task<CatalogDatabase> RenameDatabaseAsync(RenameDatabaseRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.RenameDatabase(user,request,cancellationToken),cancellationToken).AsTask();
    public Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Objects(user,request),cancellationToken).AsTask();
    public Draft GetDraft(string id)=>this.Invoke((service,user)=>service.GetDraft(user,id));
    public Task<Draft> GetDraftAsync(string id,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.GetDraft(user,id),cancellationToken).AsTask();
    public Draft? FindDraft(string id)=>this.Invoke((service,user)=>service.FindDraft(user,id));
    public Task<Draft?> FindDraftAsync(string id,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.FindDraft(user,id),cancellationToken).AsTask();
    public DefinitionVersion GetDefinition(string id)=>this.Invoke((service,user)=>service.GetDefinition(user,id));
    public Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.GetDefinition(user,id),cancellationToken).AsTask();
    public Draft SaveDraft(SaveDraftRequest request){
        var frozen=request with{Definition=request.Definition.Freeze()};
        return this.Invoke((service,user)=>service.SaveDraft(user,frozen));
    }
    public Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellationToken=default){
        var frozen=request with{Definition=request.Definition.Freeze()};
        return this.InvokeAsync((service,user)=>service.SaveDraft(user,frozen),cancellationToken).AsTask();
    }
    public BuildResult Build(DraftKey key)=>this.Invoke((service,user)=>service.Build(user,key));
    public Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Build(user,key),cancellationToken).AsTask();
    public ValidationDatabase CreateValidationDraft(DraftKey key)=>this.Invoke((service,user)=>service.CreateValidationDraft(user,key));
    public Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.CreateValidationDraft(user,key),cancellationToken).AsTask();
    public void DropValidation(string id)=>this.Invoke((service,user)=>{service.DropValidation(user,id);return true;});
    public Task DropValidationAsync(string id,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>{service.DropValidation(user,id);return true;},cancellationToken).AsTask();
    public ChangePreview Preview(DraftKey key)=>this.Invoke((service,user)=>service.Preview(user,key));
    public Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Preview(user,key),cancellationToken).AsTask();
    public OperationInfo Apply(ApplyRequest request)=>this.Invoke((service,user)=>service.Apply(user,request));
    public Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Apply(user,request),cancellationToken).AsTask();
    public OperationInfo GetOperation(Guid id)=>this.Invoke((service,user)=>service.GetOperation(user,id));
    public Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.GetOperation(user,id),cancellationToken).AsTask();
    public Task<CatalogPage<FunctionSummary>> FunctionsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.Functions(user,request),cancellationToken).AsTask();
    public Task<FunctionDetails> GetFunctionDetailsAsync(FunctionTarget target,CancellationToken cancellationToken=default)=>this.InvokeAsync((service,user)=>service.GetFunctionDetails(user,target),cancellationToken).AsTask();
    public Task<ObjectExecutionResult> ExecuteFunctionAsync(FunctionExecutionRequest request,CancellationToken cancellationToken=default){var frozen=request.Freeze();return this.InvokeAsync((service,user)=>service.ExecuteFunctionAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();}
    public Task<ObjectExecutionResult> ExecuteObjectAsync(ObjectExecutionRequest request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.InvokeAsync((service,user)=>service.ExecuteObjectAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();
    }
    public Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.InvokeAsync((service,user)=>service.ReadRowsAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();
    }
    public Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellationToken=default){
        var frozen=batch.Freeze();return this.InvokeAsync((service,user)=>service.CommitRowsAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken).AsTask();
    }
    public Task<ExecutionPlanDocument[]> PlansAsync(string definitionId,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Plans(user,definitionId).Plans,cancellationToken).AsTask();
    public Task<QueryPlanResult> QueryPlansAsync(QueryPlanRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.QueryPlans(user,request),cancellationToken).AsTask();
    public Task<CatalogPage<DefinitionSummary>> QueriesAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.Queries(user,request),cancellationToken).AsTask();
    public Task<RecoveryGuideDocument> RecoveryGuideAsync(RecoveryScenario scenario,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.RecoveryGuide(user,scenario),cancellationToken).AsTask();
    public Task<ServerSettingsSnapshot> ServerSettingsAsync(CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.ServerSettings(user),cancellationToken).AsTask();
    public Task<ServerSettingsSnapshot> SaveServerSettingsAsync(ServerSettingsEdit request,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.SaveServerSettings(user,request),cancellationToken).AsTask();
    public Task<Page<UserProfile>> UserProfilesAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.UserProfiles(user,cursor),cancellationToken).AsTask();
    public Task<UserProfile> EditUserProfileAsync(UserProfileEdit request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.InvokeAsync((service,user)=>service.EditUserProfile(user,frozen),cancellationToken).AsTask();
    }
    public Task<AuditPage> AuditEventsAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.AuditEvents(user,query),cancellationToken).AsTask();
    public Task<AuditReadPage> ReadSummariesAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.ReadSummaries(user,query),cancellationToken).AsTask();
    public Task<AuditDeliveryPage> AuditDeliveryStatusAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.AuditDeliveryStatus(user,cursor),cancellationToken).AsTask();
    public Task<AuditReadBufferStatus> ReadAuditStatusAsync(CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.ReadAuditStatus(user),cancellationToken).AsTask();
}
