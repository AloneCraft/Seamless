namespace Seamless.Management;

/// <summary>サーバー下書きとの統合の前に残す控えの1つ（Manager の入力、作業領域の下書き、外部ファイル、競合中の両方など）。</summary>
public sealed record ServerDraftCopy(string Name,DefinitionExport Export);
/// <summary>サーバー下書きとの統合の前の控え。統合の直前に、確認した後で外部ファイルや作業領域が変わっていないかの照合にも使う。</summary>
public sealed record ServerDraftBackup(DefinitionVersion Published,ServerDraftCopy[] Copies,string DiskHash,Guid? ConflictId);

public sealed partial class SourceWorkspace{
    private ServerDraftBackup CaptureServerDraftBackup(DefinitionVersion published,Definition editor,long editorBaseVersion,long editorRevision){
        using var lease=this.Lock();
        this.AssertCurrent();
        this.CheckRestoreTarget(published,editor);
        var disk=this.ReadDisk();
        this.CheckRestorePaths(published,disk);
        var conflict=this.ActiveConflict();
        var copies=new List<ServerDraftCopy>{
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

    private SourceSnapshot ReadServerDraftBackupDisk(ServerDraftBackup expected){
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
