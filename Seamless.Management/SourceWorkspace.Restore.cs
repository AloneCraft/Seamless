namespace Seamless.Management;

public sealed record DraftRestoreCopy(string Name,DefinitionExport Export);
public sealed record DatabaseDraftRestore(DefinitionVersion Published,DraftRestoreCopy[] Copies,string DiskHash,Guid? ConflictId);
public sealed record TableDraftRestore(DatabaseDraftRestore Snapshot,Definition Before,Definition After,string Path);

public sealed partial class SourceWorkspace{
    public DatabaseDraftRestore PreviewDatabaseRestore(DefinitionVersion published,Definition editor,long editorBaseVersion,long editorRevision){
        using var lease=this.Lock();
        this.AssertCurrent();
        this.CheckRestoreTarget(published,editor);
        var disk=this.ReadDisk();
        this.CheckRestorePaths(published,disk);
        var conflict=this.ActiveConflict();
        var copies=new List<DraftRestoreCopy>{
            new("manager-editor",new(editor,editorBaseVersion,editorRevision)),
            new("workspace-draft",this.Current),
            new("external-files",this.Current with{Definition=this.Current.Definition with{Source=disk}})
        };
        if(conflict is not null){
            copies.Add(new("conflict-manager",this.Current with{Definition=conflict.Manager}));
            copies.Add(new("conflict-external",this.Current with{Definition=this.Current.Definition with{Source=conflict.Disk}}));
        }
        return new(published,copies.ToArray(),disk.Hash(),conflict?.Id);
    }

    public void CheckDatabaseRestore(DatabaseDraftRestore expected){
        using var lease=this.Lock();
        this.AssertCurrent();
        this.ReadDatabaseRestoreDisk(expected);
    }

    public TableDraftRestore PreviewTableRestore(DefinitionVersion published,Definition editor,Definition? editorBase,long editorBaseVersion,long editorRevision,string path){
        var snapshot=this.PreviewDatabaseRestore(published,editor,editorBaseVersion,editorRevision);
        if(snapshot.ConflictId is not null){throw ConflictPending();}
        var current=snapshot.Copies.Single(copy=>copy.Name=="workspace-draft").Export;
        if(current.BaseVersion!=published.Version){throw new ManagementException("Conflict","稼働版が進みました。下書きの基準版を確認してください。");}
        var disk=snapshot.Copies.Single(copy=>copy.Name=="external-files").Export.Definition.Source;
        var working=editorBase is null?editor:MergeDefinitions(editorBase,editor,current.Definition);
        var before=working with{Source=Merge(current.Definition.Source,working.Source,disk)};
        return new(snapshot,before,DatabaseDesigner.PrepareTableRestore(published.Definition,before,path),path);
    }

    public void FinishTableRestore(TableDraftRestore expected,Draft saved){
        using var lease=this.Lock();
        this.AssertCurrent();
        var snapshot=expected.Snapshot;
        var disk=this.ReadDatabaseRestoreDisk(snapshot);
        if(saved.BaseVersion!=snapshot.Published.Version||saved.Definition.Hash()!=expected.After.Hash()){
            throw new ManagementException("Conflict","保存された個人下書きが確認した表の復元結果と一致しません。");
        }
        var external=this.State.WithoutManager;
        var migration=this.State.WithoutManagerMigration;
        var undoConflict=this.State.UndoConflict;
        if(!undoConflict){
            try{
                var published=snapshot.Published.Definition;
                var path=expected.Before.Migration?.Tables.SingleOrDefault(item=>item.Target==expected.Path)?.Source??expected.Path;
                var externalOnly=expected.Before with{Source=Merge(this.State.Synchronized,external,disk),Migration=migration};
                if(DatabaseDesigner.Tables(published).Contains(path)||DatabaseDesigner.Tables(externalOnly).Contains(path)){
                    externalOnly=DatabaseDesigner.PrepareTableRestore(published,externalOnly,path);
                }
                external=externalOnly.Source;
                migration=externalOnly.Migration;
            }catch(ManagementException){undoConflict=true;}
        }
        this.Commit(this.State with{
            Current=new(saved.Definition,saved.BaseVersion,saved.Revision),Synchronized=saved.Definition.Source,
            WithoutManager=external,WithoutManagerMigration=migration,UndoConflict=undoConflict,SavedDraftHash=saved.Definition.Hash(),SavedDraftDefinition=saved.Definition.Freeze()
        },Writes(disk,saved.Definition.Source));
    }

    public void FinishDatabaseRestore(DatabaseDraftRestore expected,Draft saved){
        using var lease=this.Lock();
        this.AssertCurrent();
        var disk=this.ReadDatabaseRestoreDisk(expected);
        var target=expected.Published.Definition with{Migration=null};
        if(saved.BaseVersion!=expected.Published.Version||saved.Definition.Hash()!=target.Hash()){
            throw new ManagementException("Conflict","保存された個人下書きが確認した稼働版と一致しません。");
        }
        var source=saved.Definition.Source;
        this.Commit(this.State with{
            Current=new(saved.Definition,saved.BaseVersion,saved.Revision),Published=source,Synchronized=source,
            WithoutManager=source,SavedDraftHash=saved.Definition.Hash(),SavedDraftDefinition=saved.Definition.Freeze(),UndoConflict=false,
            ManagerMigrationTracked=true,WithoutManagerMigration=null,
            ResolvedConflictId=expected.ConflictId??this.State.ResolvedConflictId
        },Writes(disk,source));
        if(expected.ConflictId is not null){this.RemoveResolvedConflict();}
    }

    private SourceSnapshot ReadDatabaseRestoreDisk(DatabaseDraftRestore expected){
        this.CheckRestoreTarget(expected.Published,expected.Copies[0].Export.Definition);
        var saved=expected.Copies.Single(copy=>copy.Name=="workspace-draft").Export;
        var disk=this.ReadDisk();
        if(disk.Hash()!=expected.DiskHash||this.ActiveConflict()?.Id!=expected.ConflictId||
            this.Current.BaseVersion!=saved.BaseVersion||this.Current.DraftRevision!=saved.DraftRevision||
            this.Current.Definition.Hash()!=saved.Definition.Hash()){
            throw new ManagementException("Conflict","確認後に外部ファイルまたは作業領域が変わりました。差分を開き直してください。");
        }
        this.CheckRestorePaths(expected.Published,disk);
        return disk;
    }

    private void CheckRestoreTarget(DefinitionVersion published,Definition editor){
        if(this.State.PendingTablePublication is not null){throw new ManagementException("Busy","先に単独保存の結果を照会してください。");}
        if(editor.Id!=this.Current.Definition.Id||published.Definition.Id!=editor.Id||published.Definition.Database!=editor.Database||
            published.Definition.Kind!=DefinitionKind.Database||published.Version<1){
            throw new ManagementException("Conflict","戻す対象が作業領域の稼働DBと一致しません。");
        }
        var source=published.Definition.Source;
        if(!source.References.SequenceEqual(this.State.Synchronized.References)||
            !(source.Defines??[]).SequenceEqual(this.State.Synchronized.Defines??[],StringComparer.Ordinal)){
            throw new ManagementException("Unsupported","稼働版と連携中のプロジェクトで参照・コンパイル条件が異なります。");
        }
    }

    private void CheckRestorePaths(DefinitionVersion published,SourceSnapshot disk){
        foreach(var file in published.Definition.Source.Files){
            if(!disk.Files.Any(existing=>string.Equals(existing.Path,file.Path,StringComparison.OrdinalIgnoreCase))&&File.Exists(this.FilePath(file.Path))){
                throw new ManagementException("Conflict","復元先に管理対象外のファイルがあります: "+file.Path);
            }
        }
    }
}
