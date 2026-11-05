namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private DefinitionVersion Published(string id)=>this.store.Read<DefinitionVersion>("active",id)??throw new ManagementException("NotFound","定義がありません。");
    private static void RequireSource(UserAccount user,DefinitionVersion version){Require(user,version.Definition.Database,ManagementPermission.View);Require(user,version.Definition.Database,ManagementPermission.Edit);}
    public PublishedDefinitionInfo DefinitionInfo(UserAccount user,string id){var version=this.Published(id);var definition=version.Definition;if(!Can(user,definition.Database,ManagementPermission.View)&&(definition.Kind!=DefinitionKind.Query||!Can(user,definition.Database,ManagementPermission.Execute))){throw new ManagementException("Permission","公開情報の閲覧または問い合わせの実行権限が必要です。");}AuditQueryInfo? method=null;if(definition.Kind==DefinitionKind.Query){method=this.store.ReadAuditMethod(version.ArtifactHash,definition.Source.Hash());if(method is not null){method=method with{QueryTemplate=null};}}return new(new(definition.Id,definition.Database,definition.Kind,definition.EntryType,definition.EntryMember,definition.WritesData),version.Version,version.ArtifactHash,method);}
}
