using System.Text.Json;

namespace Seamless.Management.Server;

public static partial class ManagementStoreUpgrade{
    private const string PreparedManifestFile="management.prepared-wal-upgrade.json";
    private const string PreparedMarker="upgrading-prepared-wal-v1";

    public static void UpgradePreparedWal(string root,string backup)=>UpgradePreparedWal(root,backup,CancellationToken.None);
    public static void UpgradePreparedWal(string root,string backup,CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        root=NormalizeRoot(root);NoLink(root);using var owner=Program.LockOffline(root);
        if(File.Exists(Child(root,ManifestFile))||File.Exists(Child(root,PreparedManifestFile))||File.Exists(Child(root,TypedManifestFile))){
            throw new ManagementException("RecoveryRequired","中断した管理領域の更新を先に明示復旧してください。");
        }
        var format=File.ReadAllText(Child(root,"management.format")).Trim();
        if(format is "6" or "7"){return;}
        if(format!="5"){throw new ManagementException("MigrationRequired","先にupgrade-auditで形式5へ更新してから分割WALへ移行してください。");}
        Program.CopyLocked(root,backup,cancellationToken);
        var id=Guid.NewGuid();var stage=PreparedStage(root,id);
        if(Directory.Exists(stage)||File.Exists(stage)){throw new IOException("ステージの保存先は未使用である必要があります。");}
        Directory.CreateDirectory(stage);var raw=Child(stage,"records.transfer");StoreHeader header;
        using(var source=new ManagementStore(root,owner,true)){header=source.ExportRaw(raw,cancellationToken);}
        using(var destination=ManagementStore.CreateUpgradeStage(stage,6)){
            destination.ImportRaw(raw,header,cancellationToken);
            using(var source=new ManagementStore(root,owner,true)){destination.ImportAudit(source,cancellationToken);}
            destination.Write(()=>destination.AddHistory("offline-upgrade","system","PreparedWalUpgraded",id.ToString("N"),6));
            if(destination.InstanceId!=header.InstanceId){throw new InvalidDataException("更新後のインスタンスIDが一致しません。");}
        }
        using(var check=new ManagementStore(stage)){
            if(check.Format!=6||check.InstanceId!=header.InstanceId){throw new InvalidDataException("更新後の管理ヘッダーを検証できません。");}
            check.AuditEvents(new(Limit:1));check.ReadSummaries(new(Limit:1));
        }
        File.Delete(raw);Checkpoint?.Invoke("Staged");cancellationToken.ThrowIfCancellationRequested();
        var manifest=new Manifest(id,header.InstanceId,5);var path=Child(root,PreparedManifestFile);
        WriteNew(path,JsonSerializer.SerializeToUtf8Bytes(manifest));
        try{
            InstallStage(root,stage,PreparedPrevious(root,id),PreparedMarker,6);File.Delete(path);
        }catch(Exception error){
            try{if(File.Exists(path)){RollbackPreparedInside(root,owner,manifest);}}
            catch(Exception rollback){throw new AggregateException("分割WALへの更新と取消の両方に失敗しました。通常起動せず明示復旧してください。",error,rollback);}
            throw;
        }
        Checkpoint?.Invoke("Published");
    }

    public static void RollbackPreparedWal(string root){
        root=NormalizeRoot(root);NoLink(root);using var owner=Program.LockOffline(root);var path=Child(root,PreparedManifestFile);
        if(!File.Exists(path)){throw new ManagementException("NoPendingUpgrade","取り消す分割WAL更新はありません。");}
        if(new FileInfo(path).Length>4096){throw new InvalidDataException("更新マニフェストが不正です。");}
        var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(path))??throw new InvalidDataException("更新マニフェストがありません。");
        if(manifest.Id==Guid.Empty||manifest.InstanceId==Guid.Empty||manifest.OriginalFormat!=5){throw new InvalidDataException("更新マニフェストの識別情報が不正です。");}
        RollbackPreparedInside(root,owner,manifest);
    }

    private static void RollbackPreparedInside(string root,FileStream owner,Manifest manifest){
        RestoreStage(root,PreparedStage(root,manifest.Id),PreparedPrevious(root,manifest.Id),Child(root,"system.wal-failed-"+manifest.Id.ToString("N")));
        WriteFormat(root,"5");
        try{
            using var restored=ManagementStore.OpenUpgradeRecovery(root,owner);
            if(restored.Format!=5||restored.InstanceId!=manifest.InstanceId){throw new InvalidDataException("元の管理領域を確認できません。");}
        }catch{WriteFormat(root,PreparedMarker);throw;}
        File.Delete(Child(root,PreparedManifestFile));
    }

    private static string PreparedStage(string root,Guid id)=>Sibling(root,".wal-"+id.ToString("N"));
    private static string PreparedPrevious(string root,Guid id)=>Child(root,"system.before-wal-"+id.ToString("N"));
}
