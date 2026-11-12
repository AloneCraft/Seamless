using System.Text.Json;

namespace Seamless.Management;

public sealed partial class ManagementClient{
    public Task<CatalogPage<CatalogSchema>> SchemasAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<CatalogPage<CatalogSchema>>("catalogSchemas","",request,cancellationToken);
    public Task<CatalogObjectDetails> GetObjectDetailsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<CatalogObjectDetails>("catalogPublishedDetails","",request,cancellationToken);
    public Task<QueryTracePage> QueryTraceAsync(QueryTraceQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<QueryTracePage>("queryTrace","system",query,cancellationToken);
    public Task<ResourceHeatReadResult> QueryResourceHeatAsync(ResourceHeatQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<ResourceHeatReadResult>("queryResourceHeat","system",query,cancellationToken);
    public Task<QueryTraceWatchResult> WatchQueryTraceAsync(QueryTraceWatchQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<QueryTraceWatchResult>("watchQueryTrace","system",query,cancellationToken);
    public Task<QueryExecutionHistoryPage> QueryExecutionHistoryAsync(QueryExecutionHistoryQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<QueryExecutionHistoryPage>("queryExecutionHistory","system",query,cancellationToken);
    public Task<ServerSettingsSnapshot> ServerSettingsAsync(CancellationToken cancellationToken=default)=>
        this.CallAsync<ServerSettingsSnapshot>("serverSettings","*",new{},cancellationToken);
    public Task<ServerSettingsSnapshot> SaveServerSettingsAsync(ServerSettingsEdit request,CancellationToken cancellationToken=default)=>
        this.CallAsync<ServerSettingsSnapshot>("saveServerSettings","*",request,cancellationToken);
    public Task<Page<UserProfile>> UserProfilesAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        this.CallAsync<Page<UserProfile>>("userProfiles","*",new{cursor},cancellationToken);
    public Task<UserProfile> EditUserProfileAsync(UserProfileEdit request,CancellationToken cancellationToken=default)=>
        this.CallAsync<UserProfile>("editUserProfile","*",request.Freeze(),cancellationToken);
    public Task<CatalogDatabase> RenameDatabaseAsync(RenameDatabaseRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<CatalogDatabase>("renameDatabase",request.ExpectedName,request,cancellationToken);
    public Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",request,cancellationToken);
    public Task<Draft> GetDraftAsync(string id,CancellationToken cancellationToken=default)=>
        this.CallAsync<Draft>("draft","",new{id},cancellationToken);
    public async Task<Draft?> FindDraftAsync(string id,CancellationToken cancellationToken=default)=>
        (await this.CallAsync<DraftLookup>("findDraft","",new{id},cancellationToken).ConfigureAwait(false)).Draft;
    public Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellationToken=default)=>
        this.CallAsync<DefinitionVersion>("definition","",new{id},cancellationToken);
    public Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellationToken=default){
        var frozen=request with{Definition=request.Definition.Freeze()};
        return this.CallAsync<Draft>("saveDraft",frozen.Definition.Database,frozen,cancellationToken);
    }
    public Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.CallAsync<BuildResult>("build","",key,cancellationToken);
    public Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.CallAsync<ValidationDatabase>("validation","",new{id=key.Id,draftRevision=key.Revision,bundleHash=key.BundleHash},cancellationToken);
    public Task DropValidationAsync(string id,CancellationToken cancellationToken=default)=>
        this.CallAsync<JsonElement>("dropValidation","",new{id},cancellationToken);
    public Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.CallAsync<ChangePreview>("preview","",key,cancellationToken);
    public Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<OperationInfo>("apply","",request,cancellationToken);
    public Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellationToken=default)=>
        this.CallAsync<OperationInfo>("operation","",id,cancellationToken);
    public Task<CatalogPage<FunctionSummary>> FunctionsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<CatalogPage<FunctionSummary>>("functions","",request,cancellationToken);
    public Task<FunctionDetails> GetFunctionDetailsAsync(FunctionTarget target,CancellationToken cancellationToken=default)=>this.CallAsync<FunctionDetails>("functionDetails","",target,cancellationToken);
    public Task<ObjectExecutionResult> ExecuteFunctionAsync(FunctionExecutionRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<ObjectExecutionResult>("executeFunction","",request.Freeze(),cancellationToken);
    public Task<ObjectExecutionResult> ExecuteObjectAsync(ObjectExecutionRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<ObjectExecutionResult>("executeObject",request.Database,request,cancellationToken);
    public Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<TableRows>("readRows",request.Database,request,cancellationToken);
    public Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellationToken=default)=>
        this.CallAsync<TableRowCommit>("commitRows",batch.Database,batch,cancellationToken);
    public async Task<ExecutionPlanDocument[]> PlansAsync(string definitionId,CancellationToken cancellationToken=default)=>
        (await this.CallAsync<BuildResult>("plans","",new{id=definitionId},cancellationToken).ConfigureAwait(false)).Plans;
    public Task<QueryPlanResult> QueryPlansAsync(QueryPlanRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<QueryPlanResult>("queryPlans","",request,cancellationToken);
    public Task<CatalogPage<DefinitionSummary>> QueriesAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<CatalogPage<DefinitionSummary>>("catalogQueries","",request,cancellationToken);
}
