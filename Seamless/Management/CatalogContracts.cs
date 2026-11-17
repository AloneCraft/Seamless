namespace Seamless.Management;

public sealed record CatalogDatabase(string Id,string Name,string DefinitionId,long Version,string ArtifactHash,bool IsSystem);
public sealed record RenameDatabaseRequest(string DatabaseId,string ExpectedName,string Name,Guid OperationId);
public sealed record CatalogRequest(string DatabaseId,string VersionToken="",long Cursor=0,string? Schema=null,string? Kind=null,string? ObjectId=null,string? NameFilter=null,bool IncludeSystemObjects=true);
public sealed record CatalogPage<T>(T[] Items,long NextCursor,bool HasMore,string VersionToken);
public sealed record CatalogSchema(string Name);
public sealed record CatalogObject(string Id,string Name,string Schema,string Kind,string RecordType,string? KeyType=null);
public sealed record CatalogField(string Name,string Type){
    /// <summary>NameはJSONの値キー。宣言上の名前と異なる場合だけ、画面表示用にC#の宣言名を持つ。</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public string? DisplayName{get;init;}
}
public sealed record CatalogSource(string DefinitionId,long Version,string ArtifactHash,string? Path,int Start,int Length,bool Available){public string? UnavailableReason{get;init;}}
public sealed record CatalogObjectDetails(CatalogObject Object,CatalogField[] Columns,CatalogField[] KeyMembers,CatalogField[] Parameters,string? ReturnType,CatalogSource Source,ForeignKeyDesign[]? ForeignKeys=null);
public sealed record CatalogObjectSource(CatalogSource Location,DefinitionVersion? Definition);
