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
        var metadata=Path.Combine(directory,".seamless","projects",id);CheckLinks(metadata);Directory.CreateDirectory(metadata);
        var stateFile=Path.Combine(metadata,"workspace.json");var pending=Path.Combine(metadata,"pending.json");
        WorkspaceState state;
        if(File.Exists(stateFile)){state=Read<WorkspaceState>(stateFile);}
        else if(File.Exists(pending)){state=Read<WorkspaceJournal>(pending).State;}
        else{
            var definition=new Definition("source-"+id,Path.GetFileNameWithoutExtension(project),DefinitionKind.Database,"","",source);
            state=new(1,new(definition,0,0),source,source,source,null,ProjectBinding:binding);
        }
        if(state.Format!=1||state.ProjectBinding is null||state.ProjectBinding.ProjectPath!=project){
            throw new ManagementException("Conflict","プロジェクトが同期記録と一致しません。既存の編集・同期記録を保持しています。");
        }
        var workspace=new SourceWorkspace(directory,state,metadata);
        using var lease=workspace.Lock();
        if(File.Exists(pending)){workspace.Recover(Read<WorkspaceJournal>(pending));}
        if(!File.Exists(stateFile)){workspace.Commit(state,[]);}
        else if(!workspace.State.ProjectBinding!.Files.SequenceEqual(binding.Files)||!Header(workspace.Current.Definition.Source).Equals(Header(source),StringComparison.Ordinal)){
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
    private static string Header(SourceSnapshot source)=>(source with{Revision="",Files=[]}).Hash();
}
