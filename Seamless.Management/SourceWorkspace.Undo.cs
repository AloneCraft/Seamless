namespace Seamless.Management;

public sealed record ManagerUndoPreview(Definition Before,Definition After,string DiskHash,long BaseVersion,long DraftRevision);
public sealed record ManagerUndoResult(DefinitionExport Draft,string? BackupDirectory);

public sealed partial class SourceWorkspace{
    public ManagerUndoPreview PreviewManagerUndo(Definition manager,Definition? managerBase=null){
        using var lease=this.Lock();
        this.AssertCurrent();
        return this.PlanManagerUndo(manager,managerBase,this.ReadDisk());
    }

    public ManagerUndoResult ConfirmManagerUndo(Definition manager,Definition? managerBase,ManagerUndoPreview expected){
        using var lease=this.Lock();
        this.AssertCurrent();
        var disk=this.ReadDisk();
        if(disk.Hash()!=expected.DiskHash||this.Current.BaseVersion!=expected.BaseVersion||this.Current.DraftRevision!=expected.DraftRevision){
            throw new ManagementException("Conflict","確認後に外部ファイルか下書きの版が変わりました。差分を開き直してください。");
        }
        var plan=this.PlanManagerUndo(manager,managerBase,disk);
        if(plan.Before.Hash()!=expected.Before.Hash()||plan.After.Hash()!=expected.After.Hash()){
            throw new ManagementException("Conflict","確認後に取り消し対象が変わりました。差分を開き直してください。");
        }
        string? backup=null;
        if(plan.Before.Hash()!=plan.After.Hash()){
            backup=Path.Combine(this.Metadata,"manager-undo-"+Guid.NewGuid().ToString("N"));
            CheckLinks(backup);
            SourceArchive.Export(backup,this.Current with{Definition=plan.Before});
        }
        var current=this.Current with{Definition=plan.After};
        this.Commit(this.State with{Current=current,Synchronized=plan.After.Source,WithoutManager=plan.After.Source,
            ManagerMigrationTracked=true,WithoutManagerMigration=plan.After.Migration},Writes(disk,plan.After.Source));
        return new(this.Current,backup);
    }

    private ManagerUndoPreview PlanManagerUndo(Definition manager,Definition? managerBase,SourceSnapshot disk){
        if(this.State.PendingTablePublication is not null){throw new ManagementException("Busy","先に単独保存の結果を照会してください。");}
        if(this.ActiveConflict() is not null){throw ConflictPending();}
        if(this.State.UndoConflict){throw new ManagementException("Conflict","外部編集とManager編集を安全に分離できません。差分を確認してください。");}
        if(manager.Id!=this.Current.Definition.Id||manager.Database!=this.Current.Definition.Database){
            throw new ManagementException("Conflict","取り消し対象が作業中のDBと一致しません。");
        }
        if(managerBase is not null){manager=MergeDefinitions(managerBase,manager,this.Current.Definition);}
        if(!this.State.ManagerMigrationTracked&&this.Current.Definition.Migration is not null){
            throw new ManagementException("Conflict","この作業記録では移行定義の編集元を特定できません。");
        }
        var before=manager with{Source=Merge(this.State.Synchronized,manager.Source,disk)};
        var after=manager with{Source=Merge(this.State.Synchronized,this.State.WithoutManager,disk),Migration=this.State.WithoutManagerMigration};
        return new(before,after,disk.Hash(),this.Current.BaseVersion,this.Current.DraftRevision);
    }
}
