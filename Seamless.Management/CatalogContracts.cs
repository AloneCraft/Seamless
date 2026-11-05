namespace Seamless.Management;

public sealed record CatalogDatabase(string Id,string Name,string DefinitionId,long Version,string ArtifactHash,bool IsSystem);
public sealed record CatalogRequest(string DatabaseId,string VersionToken="",long Cursor=0,string? Schema=null,string? Kind=null,string? ObjectId=null,string? NameFilter=null);
public sealed record CatalogPage<T>(T[] Items,long NextCursor,bool HasMore,string VersionToken);
public sealed record CatalogSchema(string Name);
public sealed record CatalogObject(string Id,string Name,string Schema,string Kind,string RecordType,string? KeyType=null);
public sealed record CatalogField(string Name,string Type);
public sealed record CatalogSource(string DefinitionId,long Version,string ArtifactHash,string? Path,int Start,int Length,bool Available);
public sealed record CatalogObjectDetails(CatalogObject Object,CatalogField[] Columns,CatalogField[] KeyMembers,CatalogField[] Parameters,string? ReturnType,CatalogSource Source);
public sealed record CatalogObjectSource(CatalogSource Location,DefinitionVersion? Definition);
