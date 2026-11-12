using System.Text;
using System.Text.Json;

namespace Seamless.Management.Server;

public static partial class ManagementStoreUpgrade{
    internal static Action<string>? Checkpoint{get;set;}
    private const string ManifestFile="management.audit-upgrade.json";
    private sealed record Manifest(Guid Id,Guid InstanceId,int OriginalFormat);
    public static void Upgrade(string root,string backup){
        root=NormalizeRoot(root);NoLink(root);using var owner=Program.LockOffline(root);
        if(File.Exists(Path.Combine(root,ManifestFile))){throw new ManagementException("RecoveryRequired","中断した監査更新を先に明示復旧してください。");}
        if(File.Exists(Path.Combine(root,PreparedManifestFile))){throw new ManagementException("RecoveryRequired","中断した分割WAL更新を先に明示復旧してください。");}
        if(File.Exists(Path.Combine(root,TypedManifestFile))){throw new ManagementException("RecoveryRequired","中断した型付き管理更新を先に明示復旧してください。");}
        var format=File.ReadAllText(Path.Combine(root,"management.format")).Trim();if(format is "5" or "6" or "7"){return;}if(format is not "3" and not "4"){throw new ManagementException("MigrationRequired","更新元の形式は3または4である必要があります。");}
        Program.CopyLocked(root,backup);var id=Guid.NewGuid();var stage=Stage(root,id);if(Directory.Exists(stage)||File.Exists(stage)){throw new IOException("ステージの保存先は未使用である必要があります。");}Directory.CreateDirectory(stage);var raw=Child(stage,"records.transfer");StoreHeader header;
        using(var source=new ManagementStore(root,owner,true)){header=source.ExportRaw(raw);}
        using(var destination=ManagementStore.CreateAuditStage(stage)){
            destination.ImportRaw(raw,header);if(header.Format==4){using var source=new ManagementStore(root,owner,true);destination.ImportAudit(source);}destination.Write(()=>destination.AddHistory("offline-upgrade","system","AuditUpgraded",id.ToString("N"),5));
            if(destination.InstanceId!=header.InstanceId){throw new InvalidDataException("更新後のインスタンスIDが一致しません。");}
        }
        using(var check=new ManagementStore(stage)){if(check.Format!=5||check.InstanceId!=header.InstanceId){throw new InvalidDataException("更新後の管理ヘッダーを検証できません。");}check.AuditEvents(new(Limit:1));check.ReadSummaries(new(Limit:1));}
        File.Delete(raw);Checkpoint?.Invoke("Staged");var manifest=new Manifest(id,header.InstanceId,header.Format);var path=Child(root,ManifestFile);WriteNew(path,JsonSerializer.SerializeToUtf8Bytes(manifest));
        try{
            InstallStage(root,stage,Previous(root,id),"upgrading-audit-v1",5);File.Delete(path);
        }catch(Exception error){
            try{if(File.Exists(path)){RollbackInside(root,owner,manifest);}}catch(Exception rollback){throw new AggregateException("監査更新と取消の両方に失敗しました。通常起動せず明示復旧してください。",error,rollback);}throw;
        }
        Checkpoint?.Invoke("Published");
    }
    public static void Rollback(string root){
        root=NormalizeRoot(root);NoLink(root);using var owner=Program.LockOffline(root);var file=Child(root,ManifestFile);
        if(!File.Exists(file)){throw new ManagementException("NoPendingUpgrade","取り消す更新はありません。確定済みの更新を過去版へ戻す処理とは別です。");}
        if(new FileInfo(file).Length>4096){throw new InvalidDataException("更新マニフェストが不正です。");}var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(file))??throw new InvalidDataException("更新マニフェストがありません。");
        if(manifest.Id==Guid.Empty||manifest.InstanceId==Guid.Empty||manifest.OriginalFormat is not 3 and not 4){throw new InvalidDataException("更新マニフェストの識別情報が不正です。");}RollbackInside(root,owner,manifest);
    }
    private static void RollbackInside(string root,FileStream owner,Manifest manifest){
        RestoreStage(root,Stage(root,manifest.Id),Previous(root,manifest.Id),Child(root,"system.audit-failed-"+manifest.Id.ToString("N")));
        WriteFormat(root,manifest.OriginalFormat.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try{using var restored=ManagementStore.OpenUpgradeRecovery(root,owner);if(restored.Format!=manifest.OriginalFormat||restored.InstanceId!=manifest.InstanceId){throw new InvalidDataException("元の管理領域を確認できません。");}}
        catch{WriteFormat(root,"upgrading-audit-v1");throw;}File.Delete(Child(root,ManifestFile));
    }
    private static void InstallStage(string root,string stage,string previous,string marker,int format){
        WriteFormat(root,marker);Checkpoint?.Invoke("Marked");
        var current=Child(root,"system");Directory.Move(current,previous);Checkpoint?.Invoke("OldMoved");
        NoLink(Child(stage,"system"));Directory.Move(Child(stage,"system"),current);Checkpoint?.Invoke("NewInstalled");
        WriteFormat(root,format.ToString(System.Globalization.CultureInfo.InvariantCulture));Checkpoint?.Invoke("Committed");
    }
    private static void RestoreStage(string root,string stage,string previous,string failed){
        var current=Child(root,"system");var stagedSystem=Child(stage,"system");
        if(Directory.Exists(previous)){
            NoLink(previous);if(Directory.Exists(current)){NoLink(current);if(Directory.Exists(failed)||File.Exists(failed)){throw new IOException("取消先に別のデータがあります。");}Directory.Move(current,failed);}Directory.Move(previous,current);
        }else if(!Directory.Exists(current)||!Directory.Exists(stagedSystem)){throw new ManagementException("RecoveryRequired","元のsystemを特定できません。退避先から明示復旧してください。");}
    }
    private static string Stage(string root,Guid id)=>Sibling(root,".audit-"+id.ToString("N"));
    private static string Sibling(string root,string name){var parent=Path.GetDirectoryName(root)??throw new InvalidDataException("管理領域の親がありません。");var path=Path.GetFullPath(Path.Combine(parent,name));if(Path.GetDirectoryName(path)!=parent||path==root){throw new InvalidDataException("ステージの保存先が不正です。");}return path;}
    private static string NormalizeRoot(string root){var path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));if(Path.GetDirectoryName(path) is null){throw new ArgumentException("管理領域には専用のサブフォルダーを指定してください。");}return path;}
    private static string Previous(string root,Guid id)=>Child(root,"system.before-audit-"+id.ToString("N"));
    private static string Child(string root,string name){var path=Path.GetFullPath(Path.Combine(root,name));if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("管理領域の外を操作できません。");}return path;}
    private static void NoLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む管理領域は明示的に解決してください。");}}
    private static void WriteNew(string path,byte[] bytes){using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);file.Write(bytes);file.Flush(true);}
    private static void WriteFormat(string root,string format){using var file=new FileStream(Child(root,"management.format"),FileMode.Create,FileAccess.Write,FileShare.Read);file.Write(Encoding.UTF8.GetBytes(format));file.Flush(true);}
}
