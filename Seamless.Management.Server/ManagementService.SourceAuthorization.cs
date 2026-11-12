namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private DefinitionVersion Published(string id)=>this.store.Read<DefinitionVersion>("active",id)??throw new ManagementException("NotFound","定義がありません。");
    private void RequireSource(UserAccount user,DefinitionVersion version){Require(user,this.store.CurrentDatabase(version.Definition),ManagementPermission.View);Require(user,this.store.CurrentDatabase(version.Definition),ManagementPermission.Edit);}
    public PublishedDefinitionInfo DefinitionInfo(UserAccount user,string id){var version=this.Published(id);var definition=version.Definition;if(!Can(user,version.Database,ManagementPermission.View)&&(definition.Kind!=DefinitionKind.Query||!Can(user,version.Database,ManagementPermission.Execute))){throw new ManagementException("Permission","公開情報の閲覧または問い合わせの実行権限が必要です。");}AuditQueryInfo? method=null;if(definition.Kind==DefinitionKind.Query){method=this.store.ReadAuditMethod(version.ArtifactHash,definition.Source.Hash());if(method is not null){method=method with{QueryTemplate=null};}}return new(new(definition.Id,version.Database,definition.Kind,definition.EntryType,definition.EntryMember,definition.WritesData),version.Version,version.ArtifactHash,method);}
}
