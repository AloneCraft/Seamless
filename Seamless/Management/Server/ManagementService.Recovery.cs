namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    // サーバーの保存先を含むため、管理領域全体の管理権限を要求する。
    public RecoveryGuideDocument RecoveryGuide(UserAccount actor,RecoveryScenario scenario){
        lock(this.store.Gate){actor=this.CurrentActor(actor);Require(actor,"*",ManagementPermission.Manage);}
        var guide=RecoveryGuideCommand.Create(scenario,this.store.Root,null,null);
        if(scenario!=RecoveryScenario.RestoreFromBackup){return guide;}
        var logicalBackupRoot=Path.Combine(this.store.Root,"backups");
        string backupRoot;
        try{backupRoot=ManagementPlacement.DirectoryPath(logicalBackupRoot,allowShortcut:true);}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException){
            return guide;
        }
        var backups=this.Records<OperationInfo>("operation")
            .Where(operation=>operation.State==OperationState.Succeeded&&
                operation.BackupPath==Path.Combine(logicalBackupRoot,operation.Id.ToString("N"))&&
                Directory.Exists(Path.Combine(backupRoot,operation.Id.ToString("N"))))
            .OrderByDescending(operation=>operation.StartedAt).Take(100)
            .Select(operation=>new RecoveryBackupLocation(operation.Database,operation.StartedAt,Path.Combine(backupRoot,operation.Id.ToString("N")))).ToArray();
        return guide with{Backups=backups};
    }
}
