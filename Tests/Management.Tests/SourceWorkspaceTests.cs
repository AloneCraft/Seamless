using Seamless.Management;

namespace Management.Tests;

public sealed class SourceWorkspaceTests{
    private static string DirectoryPath()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("テスト領域が必要です。"),"workspace-"+Guid.NewGuid().ToString("N"));
    private static Definition Definition()=>Seamless.Management.DatabaseDesigner.Empty("workspace","Customers") with{Source=new("1",[new("Database.cs","namespace Example;\npublic class Customer{\n    public string Name {get;set;} = \"\";\n}\n")],[])};
    [Fact]public void ExternalSaveAndManagerEditMergeAndSurviveRestart(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        var manager=original with{Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text.Replace("}\n","    public int Phone {get;set;}\n}\n"))]}};
        workspace.Synchronize(manager);
        File.WriteAllText(Path.Combine(root,"Database.cs"),File.ReadAllText(Path.Combine(root,"Database.cs")).Replace("Name","DisplayName"));
        var merged=workspace.Synchronize(manager);Assert.Contains("Phone",merged.Definition.Source.Files[0].Text);Assert.Contains("DisplayName",merged.Definition.Source.Files[0].Text);
        workspace=SourceWorkspace.Open(root);Assert.True(workspace.HasUnappliedChanges);Assert.True(workspace.NeedsDraftSave);
        var reverted=workspace.UndoManager();Assert.DoesNotContain("Phone",reverted.Definition.Source.Files[0].Text);Assert.Contains("DisplayName",reverted.Definition.Source.Files[0].Text);Assert.True(workspace.HasUnappliedChanges);
        Assert.Contains("DisplayName",File.ReadAllText(Path.Combine(root,"Database.cs")));Assert.True(File.Exists(Path.Combine(root,"Schema.csproj")));
    }
    [Fact]public void OverlapPreservesBothInputsAndDisk(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        var manager=original with{Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text.Replace("Name","Phone"))]}};
        File.WriteAllText(Path.Combine(root,"Database.cs"),original.Source.Files[0].Text.Replace("Name","Address"));
        Assert.Throws<ManagementException>(()=>workspace.Synchronize(manager));Assert.Contains("Address",File.ReadAllText(Path.Combine(root,"Database.cs")));
        Assert.True(File.Exists(Path.Combine(root,".seamless","conflict.json")));
    }
    [Fact]public void ManagerUndoWithoutManagerChangesDoesNotRewriteExternalFile(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());var file=Path.Combine(root,"Database.cs");
        File.WriteAllText(file,original.Source.Files[0].Text.Replace("Name","Label"));workspace.Synchronize(original);var timestamp=File.GetLastWriteTimeUtc(file);
        workspace.UndoManager();Assert.Equal(timestamp,File.GetLastWriteTimeUtc(file));Assert.True(workspace.HasUnappliedChanges);
    }
    [Fact]public void NoOpAndManagerOnlyUndoReturnToLiveWithoutPublishing(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,7,3),original.Source,RepositoryRoot());Assert.False(workspace.HasUnappliedChanges);
        var edited=original with{Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text+"// Manager\n")]}};
        workspace.Synchronize(edited);Assert.True(workspace.HasUnappliedChanges);workspace.UndoManager();Assert.False(workspace.HasUnappliedChanges);Assert.Equal(7,workspace.Current.BaseVersion);
    }
    [Fact]public void AddedTableFilesAreBuildInputsAndUndoPreservesExternalEdits(){
        var original=Seamless.Management.DatabaseDesigner.Empty("new-db","Example");var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        var changed=Seamless.Management.DatabaseDesigner.Apply(original,new("Customers","Sales",[new("Id","int",true),new("Name","string",false)],[]));workspace.Synchronize(changed);
        foreach(var file in changed.Source.Files){Assert.Equal(file.Text,File.ReadAllText(Path.Combine(root,file.Path)));Assert.Contains(file.Path,File.ReadAllText(Path.Combine(root,"Schema.csproj")));}
        Assert.Throws<ManagementException>(()=>workspace.UndoManager());Assert.True(workspace.HasUnappliedChanges);
    }
    [Fact]public void InterruptedWritesRecoverOnlyKnownVersions(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());var before=original.Source.Files[0].Text;var after=before+"// saved during interruption\n";
        var definition=original with{Source=original.Source with{Files=[new("Database.cs",after)]}};var state=workspace.State with{Current=workspace.Current with{Definition=definition},Synchronized=definition.Source};var pending=Path.Combine(root,".seamless","pending.json");
        File.WriteAllText(pending,System.Text.Json.JsonSerializer.Serialize(new{state,writes=new[]{new{path="Database.cs",before,after}}},ManagementJson.Options));
        workspace=SourceWorkspace.Open(root);Assert.Equal(after,File.ReadAllText(Path.Combine(root,"Database.cs")));Assert.False(File.Exists(pending));Assert.True(workspace.NeedsDraftSave);
        File.WriteAllText(pending,System.Text.Json.JsonSerializer.Serialize(new{state,writes=new[]{new{path="Database.cs",before,after}}},ManagementJson.Options));File.WriteAllText(Path.Combine(root,"Database.cs"),"// unrelated external save");
        Assert.Throws<ManagementException>(()=>SourceWorkspace.Open(root));Assert.True(File.Exists(pending));Assert.Equal("// unrelated external save",File.ReadAllText(Path.Combine(root,"Database.cs")));
    }
    [Fact]public void EditingManagerAddedDeclarationImportsButBlocksUnsafeUndo(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        var added=original with{Source=original.Source with{Files=[..original.Source.Files,new("New.cs","public class NewRecord{ public int Value {get;set;} } ")]}};workspace.Synchronize(added);
        File.WriteAllText(Path.Combine(root,"New.cs"),"public class NewRecord{ public int Amount {get;set;} } ");var imported=workspace.Synchronize(added);
        Assert.Contains("Amount",imported.Definition.Source.Files.Single(file=>file.Path=="New.cs").Text);Assert.True(workspace.State.UndoConflict);Assert.Throws<ManagementException>(()=>workspace.UndoManager());Assert.Contains("Amount",File.ReadAllText(Path.Combine(root,"New.cs")));
    }
    [Fact]public void IncompleteSyntaxIsPreservedAndNoOpDoesNotTouchFiles(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());var path=Path.Combine(root,"Database.cs");
        File.WriteAllText(path,"public class Broken { ");workspace.Synchronize(original);var timestamp=File.GetLastWriteTimeUtc(path);workspace.Synchronize(workspace.Current.Definition);
        Assert.Equal(timestamp,File.GetLastWriteTimeUtc(path));Assert.Equal("public class Broken { ",SourceWorkspace.Open(root).Current.Definition.Source.Files[0].Text);
    }
    [Fact]public void LinkedRootIsRejectedBeforeCreatingMetadataOutsideWorkspace(){
        var link=DirectoryPath();var target=DirectoryPath();Directory.CreateDirectory(target);
        if(OperatingSystem.IsWindows()){
            var script="$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path '"+link.Replace("'","''")+"' -Target '"+target.Replace("'","''")+"' | Out-Null";
            var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"WindowsPowerShell","v1.0","powershell.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var argument in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))}){start.ArgumentList.Add(argument);}
            start.Environment["PSModuleAnalysisCachePath"]=Path.Combine(Environment.GetEnvironmentVariable("NUGET_HTTP_CACHE_PATH")??throw new InvalidOperationException("Fのキャッシュ領域が必要です。"),"powershell-source-tests.cache");
            using var process=System.Diagnostics.Process.Start(start)!;var error=process.StandardError.ReadToEnd();process.WaitForExit();Assert.True(process.ExitCode==0,error);
        }else{Directory.CreateSymbolicLink(link,target);}
        var original=Definition();Assert.Throws<IOException>(()=>SourceWorkspace.Create(link,new(original,1,0),original.Source,RepositoryRoot()));Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }
    [Fact]public void SourcePathSeparatorsResolveToTheSameProjectSubdirectory(){
        var original=Definition();original=original with{Source=original.Source with{Files=[..original.Source.Files,new("Nested\\Extra.cs","public struct Extra {} ")]}};var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        Assert.True(File.Exists(Path.Combine(root,"Nested","Extra.cs")));workspace.Synchronize(original);Assert.False(workspace.HasUnappliedChanges);
    }
    private static string RepositoryRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null){if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){return directory.FullName;}directory=directory.Parent;}return Environment.GetEnvironmentVariable("SEAMLESS_SOURCE_REPOSITORY")??throw new InvalidOperationException("リポジトリの場所が必要です。");}
}
