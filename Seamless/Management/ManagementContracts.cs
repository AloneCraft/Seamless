using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Seamless.Management;

public sealed record SourceFile(string Path,string Text);
/// <summary>生成コードの表示用。Kindは User(利用者ファイル)・Database(DB生成器)・FLinq(FLinq生成器)。</summary>
public sealed record GeneratedSourceFile(string Kind,string Path,string Text);
public sealed record SourceReference(string Name,string Sha256);
/// <summary>ファイル/システムDBに依存しない、版を固定した解析入力。参照名はサーバーの許可済み参照へ解決する。</summary>
public sealed record SourceSnapshot(string Revision,SourceFile[] Files,SourceReference[] References,string TargetFramework="net10.0",string LanguageVersion="14",string[]? Defines=null){
    public SourceSnapshot Freeze()=>this with{Files=this.Files.ToArray(),References=this.References.ToArray(),Defines=this.Defines?.ToArray()};
    public string Hash()=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this,ManagementJson.Options))));
}
[Flags]public enum ManagementPermission{None=0,View=1,Execute=2,Edit=4,Validate=8,Apply=16,Manage=32,ReadRows=64,WriteRows=128,All=255}
public enum DefinitionKind{Database,Query}
public enum OperationState{Running,Succeeded,Failed,Cancelled,OutcomeUnknown}
public sealed record DatabaseGrant(string Database,ManagementPermission Permissions);
public sealed record UserAccount(string Name,string PasswordHash,DatabaseGrant[] Grants,bool Disabled=false);
public sealed record UserInfo(string Name,DatabaseGrant[] Grants,bool Disabled);
public sealed record ChangeUserRequest(string Name,string? Password,DatabaseGrant[] Grants,bool Disabled=false);
public sealed record ChangePasswordRequest(string Name,string Password);
public sealed record Definition(string Id,string Database,DefinitionKind Kind,string EntryType,string EntryMember,SourceSnapshot Source,bool WritesData=false,[property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]MigrationDefinition? Migration=null){
    public Definition Freeze()=>this with{Source=this.Source.Freeze(),Migration=this.Migration?.Freeze()};
    public string Hash()=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this,ManagementJson.Options)));
}
public sealed record DefinitionVersion(Definition Definition,long Version,string Owner,DateTimeOffset CreatedAt,string ArtifactHash="",string StoragePath=""){
    // 現在の接続名を、成果物のハッシュ対象である世代ごとのDefinitionから分離する。
    public string Database{get;init;}=Definition.Database;
}
public sealed record DefinitionHeader(string Id,string Database,DefinitionKind Kind,string EntryType,string EntryMember,bool WritesData);
public sealed record DefinitionSummary(DefinitionHeader Definition,long Version,string Owner,DateTimeOffset CreatedAt,string ArtifactHash);
public sealed record PublishedDefinitionInfo(DefinitionHeader Definition,long Version,string ArtifactHash,AuditQueryInfo? Method=null);
public sealed record Draft(Definition Definition,string Owner,long Revision,long BaseVersion,DateTimeOffset SavedAt);
public sealed record DraftLookup(Draft? Draft);
public sealed record DraftSummary(DefinitionHeader Definition,long Revision,long BaseVersion,DateTimeOffset SavedAt);
public sealed record SaveDraftRequest(Definition Definition,long ExpectedRevision,long BaseVersion,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]long? ExpectedPublishedVersion=null);
public sealed record DraftKey(string Id,long Revision){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? BundleHash{get;init;}
}
public sealed record TableDraftKey(DraftKey Draft,string Table);
public sealed record DiagnosticInfo(string Code,string Message,string? Path=null,int Start=0,int Length=0,string Severity="Error");
public sealed record AnalysisResult(string SourceHash,EditorNode[] Nodes,DiagnosticInfo[] Diagnostics,bool Current);
public sealed record EditorNode(string Id,string Kind,string Label,string Path,int Start,int Length,string? ParentId,bool Editable);
public sealed record SourceEdit(string SourceHash,string Path,int Start,int Length,string Replacement);
public sealed record BuildResult(string SourceHash,string ArtifactHash,DiagnosticInfo[] Diagnostics,ExecutionPlanDocument[] Plans);
public sealed record ChangePreview(string Token,string Id,long DraftRevision,long CurrentVersion,string[] Changes,string[] Dependencies,bool CanApply,DiagnosticInfo[] Diagnostics,ValidationResult? Validation=null,bool RequiresBackup=false){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? BundleHash{get;init;}
}
public sealed record ApplyRequest(string Id,long DraftRevision,string PreviewToken,Guid OperationId,[property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]bool ConfirmDataLoss=false){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? BundleHash{get;init;}
}
public sealed record TableChangePreview(ChangePreview Preview,TableSelection[] Tables);
public sealed record ApplyTableRequest(ApplyRequest Apply,string Table);
public sealed record OperationInfo(Guid Id,string Owner,string Database,string DefinitionId,OperationState State,string? Error,long? PublishedVersion,DateTimeOffset StartedAt,string IntentHash="",string? BackupPath=null);
public sealed record HistoryEntry(long Sequence,string User,string Database,string Action,string DefinitionId,long Version,DateTimeOffset At){
    public string? DatabaseDefinitionId{get;init;}
}
public sealed record Page<T>(T[] Items,long NextCursor,bool HasMore);
public sealed record ValidationDatabase(string Id,string Database,string SourceDefinitionId,long SourceVersion,string Owner,string SourceHash="",bool FromDraft=false,bool Deleted=false){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? BundleHash{get;init;}
}
public sealed record ValidationResult(string DefinitionId,string DefinitionHash,long DraftRevision,string Owner,string ValidationDatabaseId,bool Passed,string? Error,DateTimeOffset At){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? BundleHash{get;init;}
}
public sealed record ExecuteRequest(string Id,string? ValidationDatabaseId,JsonElement Arguments,int MaximumRows=1000,long? DraftRevision=null){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public long? ExpectedVersion{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public string? ExpectedArtifactHash{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public string? ExpectedDatabaseDefinitionId{get;init;}
}
public sealed record SeedRequest(string ValidationDatabaseId,string Table,JsonElement[] Rows);
public sealed record SeedTable(string Table,JsonElement[] Rows);
public sealed record SeedBatchRequest(string ValidationDatabaseId,SeedTable[] Tables);
public sealed record ExecutionRow(JsonElement Value);
public sealed record LoginRequest(string Name,string Password);
public sealed record LoginResult(string Token,DateTimeOffset ExpiresAt,UserInfo User);
public sealed record ServerInfo(Guid InstanceId,string ProductVersion,string OperatingSystem,string[] Capabilities);
public sealed record RpcRequest(Guid Id,string Operation,string? Token,string Database,JsonElement Payload);
public sealed record RpcResponse(Guid Id,string Kind,JsonElement Payload,string? ErrorCode=null,string? Error=null);
public sealed class ManagementException(string code,string message):Exception(message){public string Code{get;}=code;}
public static class ManagementJson{
    public static JsonSerializerOptions Options{get;}=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=true,MaxDepth=128};
    public static JsonElement Element<T>(T value)=>JsonSerializer.SerializeToElement(value,Options);
    public static T Read<T>(JsonElement value)=>value.Deserialize<T>(Options)??throw new ManagementException("InvalidRequest","要求の内容がありません。");
}
