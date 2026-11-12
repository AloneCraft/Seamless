using System.Text.Json;

namespace Seamless.Management.Server;

public static partial class ManagementStoreUpgrade{
    private const string TypedManifestFile="management.typed-upgrade.json",TypedMarker="upgrading-managed-types-v1";
    public static void UpgradeManagedTypes(string root,string backup,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();root=NormalizeRoot(root);NoLink(root);using var owner=Program.LockOffline(root);
        if(File.Exists(Child(root,ManifestFile))||File.Exists(Child(root,PreparedManifestFile))||File.Exists(Child(root,TypedManifestFile))){throw new ManagementException("RecoveryRequired","中断した管理領域の更新を先に明示復旧してください。");}
        var format=File.ReadAllText(Child(root,"management.format")).Trim();if(format=="7"){return;}if(format!="6"){throw new ManagementException("MigrationRequired","型付き管理行への移行元は形式6である必要があります。");}
        Program.CopyLocked(root,backup,cancellationToken);var id=Guid.NewGuid();var stage=TypedStage(root,id);
        if(Directory.Exists(stage)||File.Exists(stage)){throw new IOException("ステージの保存先は未使用である必要があります。");}
        Directory.CreateDirectory(stage);var raw=Child(stage,"records.transfer");StoreHeader header;
        using(var source=new ManagementStore(root,owner,true)){header=source.ExportRaw(raw,cancellationToken);}
        using(var destination=ManagementStore.CreateUpgradeStage(stage,7)){
            destination.ImportRaw(raw,header,cancellationToken);
            using(var source=new ManagementStore(root,owner,true)){destination.ImportManagedTypes(source,cancellationToken);destination.ImportAudit(source,cancellationToken);}
            destination.Write(()=>destination.AddHistory("offline-upgrade","system","ManagedTypesUpgraded",id.ToString("N"),7));
        }
        using(var source=new ManagementStore(root,owner,true))using(var check=new ManagementStore(stage)){
            if(check.Format!=7||check.InstanceId!=header.InstanceId){throw new InvalidDataException("更新後の管理ヘッダーを検証できません。");}
            check.VerifyManagedTypes(source,cancellationToken);check.AuditEvents(new(Limit:1));check.ReadSummaries(new(Limit:1));
        }
        File.Delete(raw);Checkpoint?.Invoke("Staged");cancellationToken.ThrowIfCancellationRequested();
        var manifest=new Manifest(id,header.InstanceId,6);var path=Child(root,TypedManifestFile);WriteNew(path,JsonSerializer.SerializeToUtf8Bytes(manifest));
        try{InstallStage(root,stage,TypedPrevious(root,id),TypedMarker,7);File.Delete(path);}
        catch(Exception error){try{if(File.Exists(path)){RollbackTypedInside(root,owner,manifest);}}catch(Exception rollback){throw new AggregateException("型付き管理領域への更新と取消の両方に失敗しました。明示復旧してください。",error,rollback);}throw;}
        Checkpoint?.Invoke("Published");
    }
    public static void RollbackManagedTypes(string root){
        root=NormalizeRoot(root);NoLink(root);using var owner=Program.LockOffline(root);var path=Child(root,TypedManifestFile);
        if(!File.Exists(path)){throw new ManagementException("NoPendingUpgrade","取り消す型付き管理領域の更新はありません。");}
        if(new FileInfo(path).Length>4096){throw new InvalidDataException("更新マニフェストが不正です。");}
        var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(path))??throw new InvalidDataException("更新マニフェストがありません。");
        if(manifest.Id==Guid.Empty||manifest.InstanceId==Guid.Empty||manifest.OriginalFormat!=6){throw new InvalidDataException("更新マニフェストの識別情報が不正です。");}RollbackTypedInside(root,owner,manifest);
    }
    private static void RollbackTypedInside(string root,FileStream owner,Manifest manifest){
        RestoreStage(root,TypedStage(root,manifest.Id),TypedPrevious(root,manifest.Id),Child(root,"system.typed-failed-"+manifest.Id.ToString("N")));WriteFormat(root,"6");
        try{using var restored=ManagementStore.OpenUpgradeRecovery(root,owner);if(restored.Format!=6||restored.InstanceId!=manifest.InstanceId){throw new InvalidDataException("元の管理領域を確認できません。");}}
        catch{WriteFormat(root,TypedMarker);throw;}File.Delete(Child(root,TypedManifestFile));
    }
    private static string TypedStage(string root,Guid id)=>Sibling(root,".typed-"+id.ToString("N"));
    private static string TypedPrevious(string root,Guid id)=>Child(root,"system.before-typed-"+id.ToString("N"));
}
