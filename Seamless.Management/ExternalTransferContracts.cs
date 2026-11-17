namespace Seamless.Management;

/// <summary>
/// 外部DB への接続。<paramref name="ConnectionString"/> にはパスワードを入れない(入っていれば断る)。
/// パスワードは <paramref name="Password"/> で渡すか、Manager の機械の Windows 資格情報マネージャーに保存したものを使う。
/// <paramref name="SavePassword"/> が true なら、渡したパスワードを資格情報マネージャーに保存する(外部DBの決定2：保存するかは毎回選ぶ)。
/// </summary>
public sealed record ExternalConnectionRequest(string Provider,string ConnectionString,string? Password=null,bool SavePassword=false);
/// <summary>つないだ結果。<paramref name="Databases"/> はその接続で見える利用者の DB。<paramref name="PasswordSaved"/> は資格情報マネージャーに保存があるか。</summary>
public sealed record ExternalConnectionInfo(string Provider,string Server,string? UserName,bool IntegratedSecurity,string[] Databases,bool PasswordSaved);

/// <summary>取り込みの下見の要求。<paramref name="SnapshotViews"/> は写し(取り込んだ時点の行)にすると利用者が選んだビュー(「スキーマ.ビュー」)。</summary>
public sealed record ExternalImportPreviewRequest(ExternalConnectionRequest Connection,string SourceDatabase,string TargetDatabase,string[] SnapshotViews);
/// <summary>表1つと行数。</summary>
public sealed record ExternalTableSummary(string Schema,string Name,long Rows);
/// <summary>ビュー1つと扱い。<paramref name="State"/> は Translated(C# のビューに訳した)・Snapshot(写し)・NotImported(取り込まない)。</summary>
public sealed record ExternalViewSummary(string Schema,string Name,string State);
/// <summary>回避策の候補(<c>CompatibilityWorkaround</c> と同じ中身)。</summary>
public sealed record ExternalWorkaround(string Code,string Description,string? Draft=null);
/// <summary>元の DB と同じ動きにならない所(<c>CompatibilityIssue</c> と同じ中身)。<paramref name="Kind"/> はオブジェクトの種類の名前。</summary>
public sealed record ExternalCompatibilityIssue(string Kind,string Schema,string Name,string? Column,string Code,string Difference,ExternalWorkaround[] Workarounds);
/// <summary>
/// 下見の結果。<paramref name="Token"/> は生成物の指紋(下見の後に元の DB の形が変われば取り込みを止める)。
/// <paramref name="TargetExists"/> が true なら、取り込みは「置き換える」を選ばないと進めない。
/// <paramref name="Files"/>・<paramref name="SourceBytes"/> は生成した C# の件数と大きさ(DB 定義の上限は256件・4MB)。
/// </summary>
public sealed record ExternalImportPreview(string Token,string SourceDatabase,string TargetDatabase,bool TargetExists,ExternalTableSummary[] Tables,ExternalViewSummary[] Views,ExternalCompatibilityIssue[] Issues,int Files,long SourceBytes);
/// <summary>
/// 取り込みの要求。<paramref name="PreviewToken"/> は下見の印。<paramref name="ProjectDirectory"/> は生成した C# を DB のソースのプロジェクトとして書く空のフォルダー(決定3)。
/// </summary>
public sealed record ExternalImportRequest(ExternalImportPreviewRequest Preview,string PreviewToken,string ProjectDirectory,bool ReplaceExisting,Guid OperationId);
/// <summary>取り込みの最後の報告。<paramref name="Backup"/> は置き換えたときの古い世代の場所。<paramref name="ProjectPath"/> は書いたプロジェクトの .csproj。</summary>
public sealed record ExternalImportReport(string Database,string DefinitionId,long Version,ExternalTableSummary[] Tables,ExternalViewSummary[] Views,ExternalCompatibilityIssue[] Issues,string? Backup,string? ProjectPath);
/// <summary>
/// 取り込みの進み具合。<paramref name="State"/> は Running・Succeeded・Failed・Cancelled。<paramref name="Phase"/> は段階
/// (Reading・Generating・Building・Loading・Publishing・WritingProject・Done)。<paramref name="Table"/> と <paramref name="Rows"/> は入れている表と、その表の入れた行数。
/// </summary>
public sealed record ExternalTransferStatus(Guid OperationId,string State,string Phase,string? Table,long Rows,long TotalRows,string? Error,ExternalImportReport? Report);

/// <summary>
/// 外部DB(今は SQL Server だけ)との取り込み・書き出し。接続文字列とパスワードを扱うので、OS の所有者を確かめた接続だけが使う(対応の印 OwnerExternalTransferV1)。
/// 設計：Document/refactoring-designs/20261117-外部DBとファイルの取り込みと書き出し.md。
/// </summary>
public interface IManagementTransferOperations{
    Task<ExternalConnectionInfo> ConnectExternalAsync(ExternalConnectionRequest request,CancellationToken cancellationToken=default);
    Task<ExternalImportPreview> PreviewExternalImportAsync(ExternalImportPreviewRequest request,CancellationToken cancellationToken=default);
    Task<ExternalTransferStatus> StartExternalImportAsync(ExternalImportRequest request,CancellationToken cancellationToken=default);
    Task<ExternalTransferStatus> ExternalTransferStatusAsync(Guid operationId,CancellationToken cancellationToken=default);
}
