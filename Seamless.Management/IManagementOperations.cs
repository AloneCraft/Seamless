namespace Seamless.Management;

/// <summary>画面が共用する管理操作。実行先の所有権と認証は各実行形態が管理する。</summary>
public interface IManagementOperations{
    Task<CatalogPage<CatalogSchema>> SchemasAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>Task.FromException<CatalogPage<CatalogSchema>>(new ManagementException("Unsupported","この接続は公開スキーマ一覧に対応していません。"));
    Task<CatalogObjectDetails> GetObjectDetailsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>Task.FromException<CatalogObjectDetails>(new ManagementException("Unsupported","この接続は公開オブジェクト詳細に対応していません。"));
    Task<CatalogPage<FunctionSummary>> FunctionsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>Task.FromException<CatalogPage<FunctionSummary>>(new ManagementException("Unsupported","この接続は共通関数一覧に対応していません。"));
    Task<FunctionDetails> GetFunctionDetailsAsync(FunctionTarget target,CancellationToken cancellationToken=default)=>Task.FromException<FunctionDetails>(new ManagementException("Unsupported","この接続は共通関数の詳細に対応していません。"));
    Task<ObjectExecutionResult> ExecuteFunctionAsync(FunctionExecutionRequest request,CancellationToken cancellationToken=default)=>Task.FromException<ObjectExecutionResult>(new ManagementException("Unsupported","この接続は共通関数の実行に対応していません。"));
    Task<QueryTracePage> QueryTraceAsync(QueryTraceQuery query,CancellationToken cancellationToken=default)=>Task.FromException<QueryTracePage>(new ManagementException("Unsupported","この接続はクエリトレースに対応していません。"));
    Task<ResourceHeatReadResult> QueryResourceHeatAsync(ResourceHeatQuery query,CancellationToken cancellationToken=default)=>Task.FromException<ResourceHeatReadResult>(new ManagementException("Unsupported","この接続は資源履歴に対応していません。"));
    Task<QueryTraceWatchResult> WatchQueryTraceAsync(QueryTraceWatchQuery query,CancellationToken cancellationToken=default)=>Task.FromException<QueryTraceWatchResult>(new ManagementException("Unsupported","この接続はクエリトレース通知に対応していません。"));
    Task<QueryExecutionHistoryPage> QueryExecutionHistoryAsync(QueryExecutionHistoryQuery query,CancellationToken cancellationToken=default)=>Task.FromException<QueryExecutionHistoryPage>(new ManagementException("Unsupported","この接続は実績履歴に対応していません。"));
    Task<AuthorizationScopeInfo> AuthorizationScopeAsync(AuthorizationScopeQuery query,CancellationToken cancellationToken=default)=>Task.FromException<AuthorizationScopeInfo>(new ManagementException("Unsupported","サーバー全体のロール管理に対応していません。"));
    Task<AuthorizationChangeResult> ChangeAuthorizationScopeAsync(AuthorizationScopeEdit edit,CancellationToken cancellationToken=default)=>Task.FromException<AuthorizationChangeResult>(new ManagementException("Unsupported","サーバー全体のロール管理に対応していません。"));
    Task<DatabaseAuthorizationList> AuthorizationDatabasesAsync(DatabaseAuthorizationListQuery query,CancellationToken cancellationToken=default)=>Task.FromException<DatabaseAuthorizationList>(new ManagementException("Unsupported","共通ロール管理に対応していません。"));
    Task<DatabaseAuthorizationInfo> DatabaseAuthorizationAsync(DatabaseAuthorizationQuery query,CancellationToken cancellationToken=default)=>Task.FromException<DatabaseAuthorizationInfo>(new ManagementException("Unsupported","共通ロール管理に対応していません。"));
    Task<AuthorizationChangeResult> ChangeDatabaseAuthorizationAsync(DatabaseAuthorizationEdit edit,CancellationToken cancellationToken=default)=>Task.FromException<AuthorizationChangeResult>(new ManagementException("Unsupported","共通ロール管理に対応していません。"));
    Task<AuthorizationChangeResult> AuthorizationChangeResultAsync(Guid operationId,CancellationToken cancellationToken=default)=>Task.FromException<AuthorizationChangeResult>(new ManagementException("Unsupported","共通ロール管理に対応していません。"));
    Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default);
    Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellationToken=default);
    Task<CatalogDatabase> RenameDatabaseAsync(RenameDatabaseRequest request,CancellationToken cancellationToken=default)=>
        Task.FromException<CatalogDatabase>(new ManagementException("Unsupported","この接続はDB名変更に対応していません。"));
    Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellationToken=default);
    Task<CatalogPage<DefinitionSummary>> QueriesAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>
        Task.FromException<CatalogPage<DefinitionSummary>>(new ManagementException("Unsupported","この接続は公開クエリ一覧に対応していません。"));
    Task<Draft> GetDraftAsync(string id,CancellationToken cancellationToken=default);
    Task<Draft?> FindDraftAsync(string id,CancellationToken cancellationToken=default)=>
        Task.FromException<Draft?>(new ManagementException("Unsupported","この接続は個人下書きの任意照会に対応していません。"));
    Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellationToken=default);
    Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellationToken=default);
    Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellationToken=default);
    Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellationToken=default);
    Task DropValidationAsync(string id,CancellationToken cancellationToken=default);
    Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellationToken=default);
    Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellationToken=default);
    Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellationToken=default);
    Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellationToken=default);
    Task<ObjectExecutionResult> ExecuteObjectAsync(ObjectExecutionRequest request,CancellationToken cancellationToken=default)=>
        Task.FromException<ObjectExecutionResult>(new ManagementException("Unsupported","この接続はオブジェクト実行に対応していません。"));
    Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellationToken=default);
    Task<ExecutionPlanDocument[]> PlansAsync(string definitionId,CancellationToken cancellationToken=default);
    Task<QueryPlanResult> QueryPlansAsync(QueryPlanRequest request,CancellationToken cancellationToken=default)=>
        Task.FromException<QueryPlanResult>(new ManagementException("Unsupported","この接続は対象別の実行計画に対応していません。"));
    Task<ServerSettingsSnapshot> ServerSettingsAsync(CancellationToken cancellationToken=default)=>
        Task.FromException<ServerSettingsSnapshot>(new ManagementException("Unsupported","このホストは管理サーバー設定に対応していません。"));
    Task<ServerSettingsSnapshot> SaveServerSettingsAsync(ServerSettingsEdit request,CancellationToken cancellationToken=default)=>
        Task.FromException<ServerSettingsSnapshot>(new ManagementException("Unsupported","このホストは管理サーバー設定の保存に対応していません。"));
    Task<Page<UserProfile>> UserProfilesAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        Task.FromException<Page<UserProfile>>(new ManagementException("Unsupported","この接続は利用者管理に対応していません。"));
    Task<UserProfile> EditUserProfileAsync(UserProfileEdit request,CancellationToken cancellationToken=default)=>
        Task.FromException<UserProfile>(new ManagementException("Unsupported","この接続は利用者編集に対応していません。"));
    Task<AuditPage> AuditEventsAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        Task.FromException<AuditPage>(new ManagementException("Unsupported","この接続は監査検索に対応していません。"));
    Task<AuditReadPage> ReadSummariesAsync(AuditQuery query,CancellationToken cancellationToken=default)=>
        Task.FromException<AuditReadPage>(new ManagementException("Unsupported","この接続は参照集計に対応していません。"));
    Task<AuditDeliveryPage> AuditDeliveryStatusAsync(long cursor=0,CancellationToken cancellationToken=default)=>
        Task.FromException<AuditDeliveryPage>(new ManagementException("Unsupported","この接続は監査転記状態に対応していません。"));
    Task<AuditReadBufferStatus> ReadAuditStatusAsync(CancellationToken cancellationToken=default)=>
        Task.FromException<AuditReadBufferStatus>(new ManagementException("Unsupported","この接続は参照監査の記録状態に対応していません。"));
}
