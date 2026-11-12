using System.Security.Cryptography;
using System.Text.Json;

namespace Seamless.Management;

internal sealed record WorkspaceConflict(Guid Id,SourceSnapshot Baseline,Definition? ManagerBase,Definition Manager,SourceSnapshot Disk);
public sealed record SourceConflictFile(string Path,string? Baseline,string? Manager,string? Disk,string? PreferManager,string? PreferDisk){
    public bool Automatic{get;init;}
    public bool IsMetadata{get;init;}
}
public sealed record SourceConflict(Guid Id,string DiskHash,string ManagerHash,SourceConflictFile[] Files);

public sealed partial class SourceWorkspace{
    private string ConflictPath=>Path.Combine(this.Metadata,"conflict.json");

    private static ManagementException ConflictPending()=>new("SourceConflict","外部保存とManagerの編集が競合しています。両方を保持しました。「C#同期の競合を解消…」を開いてください。");

    private WorkspaceConflict? ActiveConflict(){
        if(!File.Exists(this.ConflictPath)){return null;}
        var conflict=Read<WorkspaceConflict>(this.ConflictPath);
        if(conflict.Id==Guid.Empty){
            if(!Same(conflict.Baseline,this.State.Synchronized)){return null;}
            var hash=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(conflict,ManagementJson.Options));
            conflict=conflict with{Id=new Guid(hash.AsSpan(0,16))};
        }
        if(conflict.Id==this.State.ResolvedConflictId){return null;}
        if(conflict.Manager.Id!=this.Current.Definition.Id||!Same(conflict.Baseline,this.State.Synchronized)){
            throw new ManagementException("Conflict","競合記録の基準が現在の作業と一致しません。記録とソースを保持しています。");
        }
        return conflict;
    }

    private void RecordConflict(WorkspaceConflict conflict){
        Atomic(this.ConflictPath,JsonSerializer.Serialize(conflict,ManagementJson.Options));
    }

    public SourceConflict? InspectConflict(){
        using var lease=this.Lock();
        this.AssertCurrent();
        if(this.ActiveConflict() is not{} conflict){return null;}
        var disk=this.ReadDisk();
        var plan=this.PlanConflict(conflict,disk);
        return new(conflict.Id,disk.Hash(),conflict.Manager.Hash(),plan.Conflicts);
    }
    internal SourceSnapshot? PendingConflictSource(){
        using var lease=this.Lock();this.AssertCurrent();return this.ActiveConflict()?.Manager.Source.Freeze();
    }

    public bool ConflictPreserves(Definition editor){
        using var lease=this.Lock();
        this.AssertCurrent();
        return this.ActiveConflict() is{} conflict&&
            (editor.Hash()==conflict.Manager.Hash()||editor.Hash()==this.Current.Definition.Hash());
    }

    public string PreserveConflictEditor(Guid id,Definition editor){
        using var lease=this.Lock();
        this.AssertCurrent();
        if(this.ActiveConflict()?.Id!=id||editor.Id!=this.Current.Definition.Id){
            throw new ManagementException("Conflict","退避する作業と競合記録が一致しません。");
        }
        var directory=Path.Combine(this.Metadata,"conflict-editor-"+Guid.NewGuid().ToString("N"));
        CheckLinks(directory);
        SourceArchive.Export(directory,this.Current with{Definition=editor});
        return directory;
    }

    public DefinitionExport ResolveConflict(Guid id,string diskHash,IReadOnlyDictionary<string,string?> replacements){
        using var lease=this.Lock();
        this.AssertCurrent();
        if(this.State.PendingTablePublication is not null){throw new ManagementException("Busy","先に単独保存の結果を照会してください。");}
        var conflict=this.ActiveConflict();
        var disk=this.ReadDisk();
        if(conflict is null||conflict.Id!=id||disk.Hash()!=diskHash){
            throw new ManagementException("Conflict","確認後に競合記録か外部ファイルが変わりました。開き直して比較してください。");
        }
        var plan=this.PlanConflict(conflict,disk);
        if(!plan.Conflicts.Select(file=>file.Path).ToHashSet(StringComparer.Ordinal).SetEquals(replacements.Keys)){
            throw new ManagementException("Conflict","競合したすべてのファイルの内容を確定してください。");
        }
        var filesByPath=plan.Merged.ToDictionary(file=>file.Path,StringComparer.OrdinalIgnoreCase);
        foreach(var (path,text) in replacements){if(text is not null){filesByPath.Add(path,new(path,text));}}
        var files=ConflictPaths(conflict,plan.Manager,disk).Where(filesByPath.ContainsKey).Select(path=>filesByPath[path]).ToArray();
        var source=plan.Manager.Source with{Files=files};
        SourceEditor.Validate(source);
        var definition=plan.Manager with{Source=source};
        this.Commit(this.State with{
            Current=this.Current with{Definition=definition},Synchronized=source,
            UndoConflict=true,ResolvedConflictId=id
        },Writes(disk,source));
        this.RemoveResolvedConflict();
        return this.Current;
    }

    private void RemoveResolvedConflict(){
        // 後処理が中断しても、ジャーナルの解決済みIDで同じ競合の再表示を防ぐ。
        try{File.Delete(this.ConflictPath);}
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }

    private (Definition Manager,List<SourceFile> Merged,SourceConflictFile[] Conflicts) PlanConflict(WorkspaceConflict conflict,SourceSnapshot disk){
        var manager=conflict.Manager;
        if(conflict.ManagerBase is not null){
            manager=MergeDefinitions(conflict.ManagerBase,manager,this.Current.Definition);
        }
        var merged=new List<SourceFile>();
        var conflicts=new List<SourceConflictFile>();
        foreach(var path in ConflictPaths(conflict,manager,disk)){
            string? Text(SourceSnapshot source)=>source.Files.SingleOrDefault(file=>string.Equals(file.Path,path,StringComparison.OrdinalIgnoreCase))?.Text;
            var baseline=Text(conflict.Baseline);
            var left=Text(manager.Source);
            var right=Text(disk);
            try{if(MergeFile(path,baseline,left,right) is{} text){merged.Add(new(path,text));}}
            catch(ManagementException error)when(error.Code=="Conflict"){conflicts.Add(ConflictFile(path,baseline,left,right));}
        }
        return (manager,merged,conflicts.ToArray());
    }

    private static SourceConflictFile ConflictFile(string path,string? baseline,string? manager,string? disk){
        if(baseline is null||manager is null||disk is null){return new(path,baseline,manager,disk,manager,disk);}
        var (a,b)=FileChanges(baseline,manager,disk);
        var preferManager=ApplyFileChanges(baseline,a.Concat(b.Where(change=>!a.Any(other=>ChangesOverlap(change,other)))));
        var preferDisk=ApplyFileChanges(baseline,b.Concat(a.Where(change=>!b.Any(other=>ChangesOverlap(change,other)))));
        return new(path,baseline,manager,disk,preferManager,preferDisk);
    }

    private static IEnumerable<string> ConflictPaths(WorkspaceConflict conflict,Definition manager,SourceSnapshot disk)=>
        conflict.Baseline.Files.Concat(manager.Source.Files).Concat(disk.Files).Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase);
}
