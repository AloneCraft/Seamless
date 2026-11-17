using Seamless.Security;

namespace Seamless.Management;

/// <summary>認可管理用の公開版識別情報。利用者データや実装ソースを含まない。</summary>
public sealed record GatewayPublicationRequest(Guid DatabaseResourceId,string ModuleId,string QueryId){
    public Guid? ExpectedGenerationId{get;init;}
}
public sealed record GatewayPublicationCursor(Guid CatalogVersion,long PolicyVersion,string Scope,string ModuleId,string QueryId);
public sealed record GatewayPublicationListRequest(GatewayPublicationCursor? After=null,int PageSize=50);
public sealed record GatewayPublicationItem(Guid DatabaseResourceId,string Database,string ModuleId,string QueryId,Guid GenerationId,bool IsReadOnly);
public sealed record GatewayPublicationList(IReadOnlyList<GatewayPublicationItem> Items,GatewayPublicationCursor? Next);
public sealed record GatewayPublicationDatabase(Guid ResourceId,int DatabaseId,string Name,Guid GenerationId,string SchemaFingerprint,string AssemblySha256);
public sealed record GatewayPublicationIdentity(Guid ManagementInstanceId,Guid GatewayWalId,int DatabaseId,string ModuleId,string QueryId,Guid QueryGenerationId,
    string ManifestSha256,string AssemblySha256,IReadOnlyList<GatewayPublicationDatabase> Databases);
public sealed record GatewayPublicationAccess(Guid ResourceId,SecurityPermission Permissions,bool Delegated=false);
public sealed record GatewayPublicationApprovalRequest(GatewayPublicationRequest Publication,Guid QueryResourceId,IReadOnlyList<GatewayPublicationAccess> Access);
public sealed record GatewayPublicationApprovalResult(Guid GenerationId,string? Document,bool Registered,bool AlreadyRegistered,Guid? RegistrationId=null);
