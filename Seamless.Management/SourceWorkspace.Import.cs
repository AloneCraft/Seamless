using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management;

public sealed partial class SourceWorkspace{
    public static SourceWorkspace ImportProject(SourceProjectBinding binding,SourceSnapshot source){
        SourceEditor.Validate(source);
        var project=Path.GetFullPath(binding.ProjectPath);CheckLinks(project);
        foreach(var file in binding.Files){CheckLinks(file.FullPath);}
        if(!source.Files.Select(file=>file.Path).Order(StringComparer.Ordinal).SequenceEqual(binding.Files.Select(file=>file.Path).Order(StringComparer.Ordinal))){throw new ManagementException("InvalidRequest","評価済みソースの対応が一致しません。");}
        var identity=project;if(OperatingSystem.IsWindows()){identity=identity.ToUpperInvariant();}
        var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        var directory=Path.GetDirectoryName(project)!;
        var importedMetadata=Path.Combine(directory,".seamless","projects",id);
        var metadata=importedMetadata;
        var createdMetadata=Path.Combine(directory,".seamless");
        var createdState=Path.Combine(createdMetadata,"workspace.json");
        var createdPending=Path.Combine(createdMetadata,"pending.json");
        WorkspaceState? created=null;
        if(File.Exists(createdState)){created=Read<WorkspaceState>(createdState);}
        else if(File.Exists(createdPending)){created=Read<WorkspaceJournal>(createdPending).State;}
        if(created?.ProjectBinding is{} createdBinding&&SameProjectPath(createdBinding.ProjectPath,project)){metadata=createdMetadata;}
        CheckLinks(metadata);Directory.CreateDirectory(metadata);
        var stateFile=Path.Combine(metadata,"workspace.json");var pending=Path.Combine(metadata,"pending.json");
        WorkspaceState state;
        if(File.Exists(stateFile)){state=Read<WorkspaceState>(stateFile);}
        else if(File.Exists(pending)){state=Read<WorkspaceJournal>(pending).State;}
        else{
            var definition=new Definition("source-"+id,Path.GetFileNameWithoutExtension(project),DefinitionKind.Database,"","",source);
            state=new(1,new(definition,0,0),source,source,source,null,ProjectBinding:binding);
        }
        if(state.Format!=1||state.ProjectBinding is null||!SameProjectPath(state.ProjectBinding.ProjectPath,project)){
            throw new ManagementException("Conflict","プロジェクトが同期記録と一致しません。既存の編集・同期記録を保持しています。");
        }
        var workspace=new SourceWorkspace(directory,state,metadata);
        using var lease=workspace.Lock();
        if(File.Exists(pending)){workspace.Recover(Read<WorkspaceJournal>(pending));}
        workspace.AssertCurrent();
        if(metadata==createdMetadata){
            // SDKのCompile評価はDB定義の参照DLL一覧を返さないため、既存の参照契約を引き継ぐ。
            source=source with{References=workspace.Current.Definition.Source.References.ToArray()};
            workspace.InheritImportedProject(importedMetadata,id);
        }
        if(!File.Exists(stateFile)){workspace.Commit(state,[]);}
        else if(workspace.State.ProjectBinding!.Configuration!=binding.Configuration||!workspace.State.ProjectBinding.Files.SequenceEqual(binding.Files)||!Header(workspace.Current.Definition.Source).Equals(Header(source),StringComparison.Ordinal)){
            if(workspace.ActiveConflict() is not null){throw ConflictPending();}
            SourceSnapshot Rebase(SourceSnapshot previous)=>source with{Files=source.Files.Select(file=>previous.Files.FirstOrDefault(old=>old.Path==file.Path)??file).ToArray()};
            state=workspace.State;
            var merged=Merge(state.Synchronized,state.Current.Definition.Source,source);
            // Compileから外れたファイルは同期対象から外すだけで、実ファイルを削除しない。
            workspace.Commit(state with{
                Current=state.Current with{Definition=state.Current.Definition with{Source=source with{Files=merged.Files}}},
                Published=Rebase(state.Published),Synchronized=source with{Files=merged.Files},WithoutManager=Rebase(state.WithoutManager),ProjectBinding=binding
            },[]);
        }
        return workspace;
    }

    private void InheritImportedProject(string metadata,string id){
        var path=Path.Combine(metadata,"workspace.json");
        if(!File.Exists(path)){
            if(File.Exists(Path.Combine(metadata,"pending.json"))){
                throw new ManagementException("Conflict","以前のプロジェクト同期が未完了です。復旧記録を保持しています。");
            }
            return;
        }
        CheckLinks(path);
        var previous=new SourceWorkspace(this.DirectoryPath,Read<WorkspaceState>(path),metadata);
        using var lease=previous.Lock();
        previous.AssertCurrent();
        var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        if(this.State.ImportedProjectStateHash is{} inherited){
            if(inherited!=hash||File.Exists(Path.Combine(metadata,"pending.json"))){
                throw new ManagementException("Conflict","以前のプロジェクト同期記録が変更されています。両方の記録を保持しています。");
            }
            return;
        }
        if(File.Exists(Path.Combine(metadata,"pending.json"))||previous.ActiveConflict() is not null||
            previous.State.PendingTablePublication is not null||this.ActiveConflict() is not null||this.State.PendingTablePublication is not null){
            throw new ManagementException("Conflict","プロジェクトの同期・競合・公開操作が未完了です。両方の記録を確認してください。");
        }
        if(previous.State.Format!=1||previous.Current.Definition.Id!="source-"+id||
            !SameProjectPath(previous.State.ProjectBinding?.ProjectPath,this.State.ProjectBinding?.ProjectPath)||
            previous.State.SavedDraftHash is not null||this.Current.DraftRevision!=0||
            !Same(this.Current.Definition.Source,this.State.Published)||
            !this.State.Published.Files.OrderBy(file=>file.Path,StringComparer.Ordinal).SequenceEqual(previous.State.Published.Files.OrderBy(file=>file.Path,StringComparer.Ordinal))){
            throw new ManagementException("Conflict","作成元DBとプロジェクトの同期記録が別々に更新されています。内容を保持しています。");
        }
        // 旧記録を保全し、ファイル編集・取消基準をDB定義IDと基準版へ接続する。
        var references=this.Current.Definition.Source.References;
        this.Commit(this.State with{
            Current=this.Current with{Definition=this.Current.Definition with{Source=previous.Current.Definition.Source with{References=references.ToArray()}}},
            Synchronized=previous.State.Synchronized with{References=references.ToArray()},
            WithoutManager=previous.State.WithoutManager with{References=references.ToArray()},
            UndoConflict=previous.State.UndoConflict,ImportedProjectStateHash=hash
        },[]);
    }
    private static bool SameProjectPath(string? first,string? second){
        var comparison=StringComparison.Ordinal;
        if(OperatingSystem.IsWindows()){comparison=StringComparison.OrdinalIgnoreCase;}
        return string.Equals(first,second,comparison);
    }
    private static string Header(SourceSnapshot source)=>(source with{Revision="",Files=[]}).Hash();
}
