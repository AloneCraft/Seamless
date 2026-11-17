using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Seamless.Management.Server;

public static class UpdateAuditUpgrade{
    internal static Action<string,Guid>? Checkpoint{get;set;}
    public static OperationInfo Enable(string root,string database,string backup,Guid? operationId=null,CancellationToken cancellation=default){
        ArgumentException.ThrowIfNullOrWhiteSpace(database);if(database.Length>128||database.Equals(ManagementService.SystemDatabaseId,StringComparison.OrdinalIgnoreCase)){throw new ManagementException("Validation","公開済みのユーザーDBを指定してください。");}
        var id=operationId??Guid.NewGuid();if(id==Guid.Empty){throw new ArgumentException("空でない操作IDを指定してください。",nameof(operationId));}
        root=Normalize(root);backup=Path.GetFullPath(backup);NoLinks(root);NoLinks(backup);cancellation.ThrowIfCancellationRequested();using var owner=Program.LockOffline(root);Program.CopyLocked(root,backup,cancellation);Checkpoint?.Invoke("BackupCompleted",id);cancellation.ThrowIfCancellationRequested();
        using var store=new ManagementStore(root,owner,true);if(!store.HasAudit){throw new ManagementException("MigrationRequired","先にupgrade-auditでシステム監査表を明示導入してください。");}
        var intent=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("enable-update-audit-v1\n"+store.InstanceId.ToString("N")+"\n"+database)));
        var existing=store.Read<OperationInfo>("operation",id.ToString("N"));if(existing is not null){if(existing.IntentHash!=intent||existing.Database!=database){throw new ManagementException("Conflict","操作IDは別の要求に使用されています。");}return Observed(existing);}
        var original=FindDatabase(store,database);using var runtime=new NativeRuntime(store,startAuditWorker:false);using var originalBuild=runtime.ReadArtifact(original);var enabled=IsEnabled(store,original);
        var operation=new OperationInfo(id,"offline-maintenance",database,original.Definition.Id,OperationState.Running,null,null,DateTimeOffset.UtcNow,intent,backup);
        if(enabled){operation=operation with{State=OperationState.Succeeded,PublishedVersion=original.Version};store.Write(()=>{store.Put("operation",id.ToString("N"),operation);store.AddHistory(operation.Owner,database,"UpdateAuditAlreadyEnabled",original.Definition.Id,original.Version);});return operation;}
        store.Write(()=>store.Put("operation",id.ToString("N"),operation));
        try{
            Checkpoint?.Invoke("Started",id);cancellation.ThrowIfCancellationRequested();
            runtime.Publish(original.Definition,cancellation,publication=>{
                Checkpoint?.Invoke("Prepared",id);cancellation.ThrowIfCancellationRequested();var active=store.Read<DefinitionVersion>("active",original.Definition.Id);
                if(active?.Version!=original.Version||active.Definition.Hash()!=original.Definition.Hash()||active.ArtifactHash!=original.ArtifactHash){throw new ManagementException("Conflict","準備中に対象DBの稼働版が変わりました。");}
                foreach(var dependent in publication.Dependents){if(store.Read<DefinitionVersion>("active",dependent.Definition.Id)?.Version!=dependent.ExpectedVersion){throw new ManagementException("Conflict","依存する問い合わせの版が変わりました。");}}
                var origin=publication.Primary.AuditOrigin??throw new InvalidDataException("新世代の監査転記元がありません。");var at=DateTimeOffset.UtcNow;var version=new DefinitionVersion(original.Definition,checked(original.Version+1),operation.Owner,at,publication.Primary.ArtifactHash,publication.Primary.StoragePath){Database=publication.Primary.Database};var completed=operation with{State=OperationState.Succeeded,PublishedVersion=version.Version};
                store.Write(()=>{
                    store.Put("auditOrigin",origin.WalId.ToString("N"),origin);
                    foreach(var dependent in publication.Dependents){var rebuilt=new DefinitionVersion(dependent.Definition,checked(dependent.ExpectedVersion+1),operation.Owner,at,dependent.ArtifactHash,dependent.StoragePath){Database=dependent.Database};store.Put("version",rebuilt.Definition.Id+":"+rebuilt.Version,rebuilt);store.Put("active",rebuilt.Definition.Id,rebuilt);store.AddHistory(operation.Owner,database,"Rebuilt",rebuilt.Definition.Id,rebuilt.Version);}
                    store.Put("version",version.Definition.Id+":"+version.Version,version);store.Put("active",version.Definition.Id,version);store.Put("operation",id.ToString("N"),completed);store.AddHistory(operation.Owner,database,"UpdateAuditEnabled",version.Definition.Id,version.Version);
                });operation=completed;Checkpoint?.Invoke("Committed",id);
            },enableAuditGeneration:id,migrationCheckpoint:(phase,_)=>Checkpoint?.Invoke("Copy:"+phase,id));
        }catch(Exception error){var durable=store.Read<OperationInfo>("operation",id.ToString("N"));if(durable?.State==OperationState.Succeeded){operation=durable;}else{var state=OperationState.Failed;if(error is OperationCanceledException){state=OperationState.Cancelled;}operation=operation with{State=state,Error=error.GetBaseException().Message,PublishedVersion=null};store.Write(()=>store.Put("operation",id.ToString("N"),operation));}}
        return operation;
    }
    public static OperationInfo GetOperation(string root,Guid operationId){if(operationId==Guid.Empty){throw new ArgumentException("操作IDが必要です。");}root=Normalize(root);NoLinks(root);using var owner=Program.LockOffline(root);using var store=new ManagementStore(root,owner,true);return Observed(store.Read<OperationInfo>("operation",operationId.ToString("N"))??throw new ManagementException("NotFound","操作がありません。"));}
    private static OperationInfo Observed(OperationInfo value){if(value.State==OperationState.Running){return value with{State=OperationState.OutcomeUnknown,Error="中断されたオフライン操作です。公開版と操作結果を確認してください。"};}return value;}
    private static DefinitionVersion FindDatabase(ManagementStore store,string database){
        DefinitionVersion? found=null;var cursor=0L;do{var page=store.SelectPage<DefinitionVersion,DefinitionVersion>("active",cursor,32,null,static value=>value,64);foreach(var item in page.Items){if(item.Definition.Kind!=DefinitionKind.Database||item.Database!=database){continue;}if(found is not null){throw new ManagementException("Conflict","同じ名前の公開DBが複数あります。");}found=item;}cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);return found??throw new ManagementException("NotFound","公開されたユーザーDBがありません。");
    }
    private static bool IsEnabled(ManagementStore store,DefinitionVersion version){
        var manifestPath=Path.Combine(store.Root,version.StoragePath,"manifest.json");using var file=File.OpenRead(manifestPath);var manifest=JsonSerializer.Deserialize<ArtifactManifest>(file,ManagementJson.Options)??throw new InvalidDataException("manifestがありません。");if(manifest.MetadataConsumer is null){return false;}if(manifest.MetadataConsumer!="audit-v1"||manifest.AuditDestination!=store.InstanceId){throw new InvalidDataException("監査方式の識別情報が一致しません。");}
        var cursor=0L;do{var page=store.SelectPage<AuditOrigin,AuditOrigin>("auditOrigin",cursor,32,null,static value=>value,64);foreach(var origin in page.Items){if(origin.StoragePath==version.StoragePath&&origin.DefinitionId==version.Definition.Id&&origin.ArtifactHash==version.ArtifactHash&&!origin.Drained&&origin.Options.MetadataConsumer=="audit-v1"){return true;}}cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);throw new ManagementException("RecoveryRequired","導入済み世代の転記元が見つかりません。");
    }
    private static string Normalize(string root){var path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));if(Path.GetDirectoryName(path) is null){throw new ArgumentException("専用の管理領域を指定してください。");}return path;}
    private static void NoLinks(string path){var current=Path.GetFullPath(path);while(current is not null){if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む保存先は明示的に解決してください。");}current=Path.GetDirectoryName(current);}}
}
