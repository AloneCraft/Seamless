using Seamless.Management;

namespace Management.Tests;

public sealed class SourceWorkspaceTests(Xunit.Abstractions.ITestOutputHelper output){
    [Fact]public void PackagedProjectSelectsMatchingCohortAndPreservesExplicitAndLegacyVersions(){
        var root=DirectoryPath();var feed=Path.Combine(root,"feed");Directory.CreateDirectory(feed);
        const string version="0.0.0-build.h1234567890abcdef1234567890abcdef";
        var manifest=Path.Combine(feed,"source-packages.json");
        File.WriteAllText(manifest,System.Text.Json.JsonSerializer.Serialize(new{format=1,version,assemblies=RuntimePackageHashes()}));
        var definition=Definition();
        var automatic=SourceWorkspace.CreatePackageProject(Path.Combine(root,"automatic"),new(definition,1,0),definition.Source,"Customers",packageFeed:feed);
        var references=System.Xml.Linq.XDocument.Load(automatic.ProjectPath).Descendants("PackageReference").ToArray();
        Assert.Equal(4,references.Length);
        Assert.All(references,reference=>Assert.Equal("["+version+"]",reference.Attribute("Version")!.Value));
        var explicitVersion=SourceWorkspace.CreatePackageProject(Path.Combine(root,"explicit"),new(definition,1,0),definition.Source,"Customers",packageFeed:feed,packageVersion:"0.0.0");
        Assert.All(System.Xml.Linq.XDocument.Load(explicitVersion.ProjectPath).Descendants("PackageReference"),reference=>Assert.Equal("0.0.0",reference.Attribute("Version")!.Value));
        File.Delete(manifest);
        var legacy=SourceWorkspace.CreatePackageProject(Path.Combine(root,"legacy"),new(definition,1,0),definition.Source,"Customers",packageFeed:feed);
        Assert.All(System.Xml.Linq.XDocument.Load(legacy.ProjectPath).Descendants("PackageReference"),reference=>Assert.Equal("0.0.0",reference.Attribute("Version")!.Value));
        Assert.Equal(definition.Hash(),SourceWorkspace.Open(Path.Combine(root,"automatic")).Current.Definition.Hash());
    }

    [Fact]public void PackagedProjectRejectsInvalidCohortBeforeCreatingOutput(){
        var root=DirectoryPath();var feed=Path.Combine(root,"feed");Directory.CreateDirectory(feed);
        var hashes=RuntimePackageHashes();var mismatched=new Dictionary<string,string>(hashes){["Seamless.Database.dll"]=new string('0',64)};
        var unsafeName=new Dictionary<string,string>(hashes){["../Seamless.Database.dll"]=hashes["Seamless.Database.dll"]};
        var cases=new[]{
            "{}",
            System.Text.Json.JsonSerializer.Serialize(new{format=2,version="1.0.0",assemblies=hashes}),
            System.Text.Json.JsonSerializer.Serialize(new{format=1,version="1.0.0] <invalid",assemblies=hashes}),
            System.Text.Json.JsonSerializer.Serialize(new{format=1,version="1.0.0",assemblies=mismatched}),
            System.Text.Json.JsonSerializer.Serialize(new{format=1,version="1.0.0",assemblies=unsafeName}),
            new string('x',65537)
        };
        var definition=Definition();var index=0;
        foreach(var text in cases){
            File.WriteAllText(Path.Combine(feed,"source-packages.json"),text);
            var destination=Path.Combine(root,"rejected"+index++);
            var error=Assert.Throws<ManagementException>(()=>SourceWorkspace.CreatePackageProject(destination,new(definition,1,0),definition.Source,"Customers",packageFeed:feed));
            Assert.Equal("PackageFeed",error.Code);Assert.False(Directory.Exists(destination));
        }
    }

    private static Dictionary<string,string> RuntimePackageHashes()=>new[]{"Seamless.Database.dll","Seamless.Execution.dll"}.ToDictionary(
        name=>name,name=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,name)))));

    [Fact]public void CreatedProjectUsesTheRepositorySdkPolicy(){
        var original=Definition();
        var root=DirectoryPath();
        var repository=RepositoryRoot();
        SourceWorkspace.Create(root,new(original,1,0),original.Source,repository);
        Assert.Equal(File.ReadAllText(Path.Combine(repository,"global.json")),File.ReadAllText(Path.Combine(root,"global.json")));
    }
    [Fact]public void PendingTablePublicationSurvivesRestartAndDoesNotOverwriteExternalEdits(){
        var original=Seamless.Management.DatabaseDesigner.Empty("workspace","Customers");
        var changed=Seamless.Management.DatabaseDesigner.Apply(original,new("Customers","Sales",[new("Id","int",true)],[]));
        changed=Seamless.Management.DatabaseDesigner.Apply(changed,new("Notes","Sales",[new("Id","int",true)],[]));
        var plan=Seamless.Management.DatabaseDesigner.PrepareTablePublication(original,changed,"Sales.Customers");
        var root=DirectoryPath();
        var workspace=SourceWorkspace.Create(root,new(changed,1,2),original.Source,RepositoryRoot());
        workspace.MarkDraftSaved(2,1,changed.Hash());
        var id=Guid.NewGuid();
        workspace.BeginTablePublication(id,2,changed.Hash());
        var path=Path.Combine(root,"Tables","Sales_Notes.cs");
        var external=File.ReadAllText(path)+"\n// External edit during publication\n";
        File.WriteAllText(path,external);

        workspace=SourceWorkspace.Open(root);
        Assert.Equal(id,workspace.State.PendingTablePublication?.Id);
        Assert.Equal("Busy",Assert.Throws<ManagementException>(()=>workspace.Synchronize(changed)).Code);
        var now=DateTimeOffset.UtcNow;
        workspace.FinishTablePublication(new(id,"admin",original.Database,original.Id,OperationState.Succeeded,null,2,now),
            new(plan.Candidate,2,"admin",now),new(plan.RemainingDraft,"admin",3,2,now));
        workspace=SourceWorkspace.Open(root);
        Assert.Null(workspace.State.PendingTablePublication);
        Assert.Equal(3,workspace.Current.DraftRevision);
        Assert.Equal(2,workspace.Current.BaseVersion);
        Assert.True(workspace.HasUnappliedChanges);
        Assert.False(workspace.NeedsDraftSave);
        Assert.Equal(external,File.ReadAllText(path));
        var synchronized=workspace.Synchronize(workspace.Current.Definition);
        Assert.Contains(synchronized.Definition.Source.Files,file=>file.Text==external);
        Assert.True(workspace.NeedsDraftSave);
    }
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
    [Fact]public void AdjacentNonOverlappingEditsMergeAndUndoSeparately(){
        var original=Definition();
        const string source="namespace Example; public class Customer { public int Value => 10+20; }";
        Definition WithSource(string text)=>original with{Source=original.Source with{Files=[new("Database.cs",text)]}};
        var baseline=WithSource(source);
        var root=DirectoryPath();
        var workspace=SourceWorkspace.Create(root,new(baseline,1,0),baseline.Source,RepositoryRoot());
        var path=Path.Combine(root,"Database.cs");
        var external=source.Replace("+","-");
        File.WriteAllText(path,external);

        var merged=workspace.Synchronize(WithSource(source.Replace("10","11")));
        Assert.Contains("11-20",merged.Definition.Source.Files[0].Text);
        Assert.Equal(merged.Definition.Source.Files[0].Text,File.ReadAllText(path));

        var reopened=SourceWorkspace.Open(root);
        Assert.True(reopened.HasUnappliedChanges);
        Assert.Equal(external,reopened.UndoManager().Definition.Source.Files[0].Text);
        Assert.Equal(external,File.ReadAllText(path));
    }
    [Fact]public void OverlapPreservesBothInputsAndDisk(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        var manager=original with{Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text.Replace("Name","Phone"))]}};
        File.WriteAllText(Path.Combine(root,"Database.cs"),original.Source.Files[0].Text.Replace("Name","Address"));
        Assert.Throws<ManagementException>(()=>workspace.Synchronize(manager));Assert.Contains("Address",File.ReadAllText(Path.Combine(root,"Database.cs")));
        Assert.True(File.Exists(Path.Combine(root,".seamless","conflict.json")));
    }
    [Fact]public void SourceConflictSurvivesRestartAndKeepsUnrelatedEdits(){
        var original=Definition();
        original=original with{Source=original.Source with{Files=[..original.Source.Files,new("Other.cs","public class Other {}")]}};
        var root=DirectoryPath();
        var workspace=SourceWorkspace.Create(root,new(original,7,3),original.Source,RepositoryRoot());
        var baseline=original.Source.Files[0].Text;
        var manager=original with{Source=original.Source with{Files=[new("Database.cs",baseline.Replace("Name","ManagerName")),original.Source.Files[1]]}};
        var path=Path.Combine(root,"Database.cs");
        var other=Path.Combine(root,"Other.cs");
        File.WriteAllText(path,baseline.Replace("Name","ExternalName")+"// independent external edit\n");
        File.WriteAllText(other,"public class Other { public int Value; }");
        Assert.Equal("SourceConflict",Assert.Throws<ManagementException>(()=>workspace.Synchronize(manager,original)).Code);

        workspace=SourceWorkspace.Open(root);
        Assert.True(workspace.ConflictPreserves(manager));
        Assert.Equal("SourceConflict",Assert.Throws<ManagementException>(()=>workspace.Synchronize(workspace.Current.Definition)).Code);
        var conflict=workspace.InspectConflict()!;
        var file=Assert.Single(conflict.Files);
        Assert.Equal(baseline,file.Baseline);
        Assert.Contains("ManagerName",file.Manager);
        Assert.Contains("ExternalName",file.Disk);
        Assert.Contains("ManagerName",file.PreferManager);
        Assert.Contains("independent external edit",file.PreferManager);
        var extra=manager with{Source=manager.Source with{Files=[new("Database.cs",file.Manager+"// more Manager edits"),original.Source.Files[1]]}};
        Assert.False(workspace.ConflictPreserves(extra));
        var preserved=workspace.PreserveConflictEditor(conflict.Id,extra);
        Assert.Equal(extra.Hash(),Seamless.Management.SourceArchive.Import(Path.Combine(preserved,"definition.seamless.json")).Definition.Hash());
        var edited=baseline.Replace("Name","MergedName");
        var choice=new Dictionary<string,string?>{{"Database.cs",edited}};
        File.WriteAllText(path,file.Disk+"\n// external save after review");
        Assert.Throws<ManagementException>(()=>workspace.ResolveConflict(conflict.Id,conflict.DiskHash,choice));
        Assert.Contains("external save after review",File.ReadAllText(path));
        Assert.Equal(3,workspace.Current.DraftRevision);

        var refreshed=workspace.InspectConflict()!;
        Assert.Equal(conflict.Id,refreshed.Id);
        choice["Database.cs"]=refreshed.Files[0].PreferManager!.Replace("ManagerName","MergedName");
        Assert.Throws<ManagementException>(()=>workspace.ResolveConflict(refreshed.Id,refreshed.DiskHash,new Dictionary<string,string?>()));
        var resolved=workspace.ResolveConflict(refreshed.Id,refreshed.DiskHash,choice);
        Assert.Equal(7,resolved.BaseVersion);
        Assert.Equal(3,resolved.DraftRevision);
        Assert.Equal(choice["Database.cs"],File.ReadAllText(path));
        Assert.Equal("public class Other { public int Value; }",resolved.Definition.Source.Files.Single(file=>file.Path=="Other.cs").Text);
        workspace=SourceWorkspace.Open(root);
        Assert.Null(workspace.InspectConflict());
        Assert.Equal(original.Source.Hash(),workspace.State.Published.Hash());
        Assert.True(workspace.NeedsDraftSave);
        Assert.Throws<ManagementException>(()=>workspace.UndoManager());
    }
    [Fact]public void ManagerUndoWithoutManagerChangesDoesNotRewriteExternalFile(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());var file=Path.Combine(root,"Database.cs");
        File.WriteAllText(file,original.Source.Files[0].Text.Replace("Name","Label"));workspace.Synchronize(original);var timestamp=File.GetLastWriteTimeUtc(file);
        workspace.UndoManager();Assert.Equal(timestamp,File.GetLastWriteTimeUtc(file));Assert.True(workspace.HasUnappliedChanges);
    }
    [Fact]public void DatabaseRestoreIncludesConflictCopiesAndRejectsLaterExternalSave(){
        var live=Definition();
        var root=DirectoryPath();
        var workspace=SourceWorkspace.Create(root,new(live,3,4),live.Source,RepositoryRoot());
        var path=Path.Combine(root,"Database.cs");
        var initial=live.Source.Files[0].Text;
        var manager=live with{Source=live.Source with{Files=[new("Database.cs",initial.Replace("Name","ManagerName"))]}};
        File.WriteAllText(path,initial.Replace("Name","ExternalName"));
        Assert.Equal("SourceConflict",Assert.Throws<ManagementException>(()=>workspace.Synchronize(manager,live)).Code);
        var published=new DefinitionVersion(live,3,"admin",DateTimeOffset.UtcNow);
        var preview=workspace.PreviewDatabaseRestore(published,manager,3,4);
        Assert.NotNull(preview.ConflictId);
        Assert.Contains(preview.Copies,copy=>copy.Name=="conflict-manager"&&copy.Export.Definition.Hash()==manager.Hash());
        Assert.Contains(preview.Copies,copy=>copy.Name=="conflict-external"&&copy.Export.Definition.Source.Files[0].Text.Contains("ExternalName"));

        File.AppendAllText(path,"\n// another external save\n");
        var saved=new Draft(live,"admin",5,3,DateTimeOffset.UtcNow);
        Assert.Throws<ManagementException>(()=>workspace.FinishDatabaseRestore(preview,saved));
        Assert.Contains("another external save",File.ReadAllText(path));
        preview=workspace.PreviewDatabaseRestore(published,manager,3,4);
        workspace.CheckDatabaseRestore(preview);
        workspace.FinishDatabaseRestore(preview,saved);
        workspace=SourceWorkspace.Open(root);
        Assert.Equal(initial,File.ReadAllText(path));
        Assert.Null(workspace.InspectConflict());
        Assert.Equal(5,workspace.Current.DraftRevision);
        Assert.False(workspace.HasUnappliedChanges);
        Assert.False(workspace.NeedsDraftSave);
    }
    [Fact]public void NoOpAndManagerOnlyUndoReturnToLiveWithoutPublishing(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,7,3),original.Source,RepositoryRoot());Assert.False(workspace.HasUnappliedChanges);
        var edited=original with{Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text+"// Manager\n")]}};
        workspace.Synchronize(edited);Assert.True(workspace.HasUnappliedChanges);workspace.UndoManager();Assert.False(workspace.HasUnappliedChanges);Assert.Equal(7,workspace.Current.BaseVersion);
    }
    [Fact]public void AddedTableFilesAreBuildInputsAndUndoPreservesExternalEdits(){
        var original=Seamless.Management.DatabaseDesigner.Empty("new-db","Example");
        original=original with{Source=original.Source with{Files=[..original.Source.Files,new("Other.cs","internal static class Other {}")]}};
        var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        var changed=Seamless.Management.DatabaseDesigner.Apply(original,new("Customers","Sales",[new("Id","int",true),new("Name","string",false)],[]));workspace.Synchronize(changed);
        foreach(var file in changed.Source.Files){Assert.Equal(file.Text,File.ReadAllText(Path.Combine(root,file.Path)));Assert.Contains(file.Path,File.ReadAllText(Path.Combine(root,"Schema.csproj")));}
        var unaffected=Path.Combine(root,"Other.cs");
        File.AppendAllText(unaffected,"\n// first external save\n");
        var preview=workspace.PreviewManagerUndo(changed);
        Assert.Empty(Seamless.Management.DatabaseDesigner.Tables(preview.After));
        Assert.Null(preview.After.Migration);
        Assert.NotNull(preview.Before.Migration);
        File.AppendAllText(unaffected,"// second external save\n");
        Assert.Throws<ManagementException>(()=>workspace.ConfirmManagerUndo(changed,null,preview));
        Assert.Contains("Customers",string.Join("\n",workspace.Current.Definition.Source.Files.Select(file=>file.Text)));
        Assert.Contains("second external save",File.ReadAllText(unaffected));
        Assert.Empty(Directory.GetDirectories(Path.Combine(root,".seamless"),"manager-undo-*"));

        var result=workspace.ConfirmManagerUndo(changed,null,workspace.PreviewManagerUndo(changed));
        Assert.Empty(Seamless.Management.DatabaseDesigner.Tables(result.Draft.Definition));
        Assert.Null(result.Draft.Definition.Migration);
        Assert.Contains("first external save",File.ReadAllText(unaffected));
        Assert.Contains("second external save",File.ReadAllText(unaffected));
        Assert.False(File.Exists(Path.Combine(root,"Tables","Sales_Customers.cs")));
        Assert.DoesNotContain("Sales_Customers.cs",File.ReadAllText(Path.Combine(root,"Schema.csproj")));
        var backup=Seamless.Management.SourceArchive.Import(Path.Combine(result.BackupDirectory!,"definition.seamless.json"));
        Assert.Contains("Sales.Customers",Seamless.Management.DatabaseDesigner.Tables(backup.Definition));
        Assert.True(workspace.HasUnappliedChanges);
    }
    [Fact]public void InterruptedWritesRecoverOnlyKnownVersions(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());var before=original.Source.Files[0].Text;var after=before+"// saved during interruption\n";
        var definition=original with{Source=original.Source with{Files=[new("Database.cs",after)]}};var state=workspace.State with{Current=workspace.Current with{Definition=definition},Synchronized=definition.Source};var pending=Path.Combine(root,".seamless","pending.json");
        File.WriteAllText(pending,System.Text.Json.JsonSerializer.Serialize(new{state,writes=new[]{new{path="Database.cs",before,after}}},ManagementJson.Options));
        workspace=SourceWorkspace.Open(root);Assert.Equal(after,File.ReadAllText(Path.Combine(root,"Database.cs")));Assert.False(File.Exists(pending));Assert.True(workspace.NeedsDraftSave);
        File.WriteAllText(pending,System.Text.Json.JsonSerializer.Serialize(new{state,writes=new[]{new{path="Database.cs",before,after}}},ManagementJson.Options));File.WriteAllText(Path.Combine(root,"Database.cs"),"// unrelated external save");
        Assert.Throws<ManagementException>(()=>SourceWorkspace.Open(root));Assert.True(File.Exists(pending));Assert.Equal("// unrelated external save",File.ReadAllText(Path.Combine(root,"Database.cs")));
    }
    [Fact]public void UndoKeepsMigrationAlreadyPresentWhenTheWorkspaceWasLinked(){
        var empty=Seamless.Management.DatabaseDesigner.Empty("migration-baseline","Example");
        var existing=Seamless.Management.DatabaseDesigner.Apply(empty,new("Existing","",[new("Id","int",true)],[]));
        var root=DirectoryPath();
        var workspace=SourceWorkspace.Create(root,new(existing,1,2),empty.Source,RepositoryRoot());
        var extra=Seamless.Management.DatabaseDesigner.Apply(existing,new("Extra","",[new("Id","int",true)],[]));
        workspace.Synchronize(extra);
        workspace=SourceWorkspace.Open(root);
        var restored=workspace.UndoManager();
        Assert.Equal(existing.Hash(),restored.Definition.Hash());
        Assert.Equal(2,restored.DraftRevision);
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
    [Fact]public void EditorBasedOnOlderSnapshotKeepsChangesImportedWhileItWasTyping(){
        var original=Definition();var root=DirectoryPath();var workspace=SourceWorkspace.Create(root,new(original,1,0),original.Source,RepositoryRoot());
        File.WriteAllText(Path.Combine(root,"Database.cs"),original.Source.Files[0].Text.Replace("Name","DisplayName"));workspace.Synchronize(original);
        var typing=original with{Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text+"// typed while synchronization was pending\n")]}};
        var merged=workspace.Synchronize(typing,original);Assert.Contains("DisplayName",merged.Definition.Source.Files[0].Text);Assert.Contains("typed while",merged.Definition.Source.Files[0].Text);Assert.DoesNotContain(" string Name ",merged.Definition.Source.Files[0].Text);
    }
    [Fact]public void IndependentEditorMigrationMappingsMergeWithoutDroppingOtherTables(){
        var original=Definition();var left=original with{Migration=new(1,[new("A","A",[new("Name","Name")],[])],[])};var right=original with{Migration=new(1,[new("B","B",[new("Id","Id")],[])],[])};
        var merged=SourceWorkspace.MergeDefinitions(original,left,right);Assert.Equal(new[]{"A","B"},merged.Migration!.Tables.Select(table=>table.Source));
        var conflict=right with{Migration=new(1,[new("A","A",[new("Value","Name")],[])],[])};Assert.Throws<ManagementException>(()=>SourceWorkspace.MergeDefinitions(original,left,conflict));
    }
    [Fact]public void AcknowledgementOfEarlierDraftDoesNotMarkNewerFilesSaved(){
        var original=Definition();var workspace=SourceWorkspace.Create(DirectoryPath(),new(original,1,0),original.Source,RepositoryRoot());
        var saved=original with{Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text+"// saved\n")]}};workspace.Synchronize(saved);
        var newer=saved with{Source=saved.Source with{Files=[new("Database.cs",saved.Source.Files[0].Text+"// pending\n")]}};workspace.Synchronize(newer);
        workspace.MarkDraftSaved(1,1,saved.Hash());Assert.True(workspace.NeedsDraftSave);Assert.Equal(newer.Hash(),workspace.Current.Definition.Hash());workspace.MarkDraftSaved(2,1,newer.Hash());workspace.MarkDraftSaved(1,1,saved.Hash());Assert.Equal(2,workspace.Current.DraftRevision);Assert.False(workspace.NeedsDraftSave);
    }
    [Fact]public void ServerDraftMergeKeepsNonOverlappingEditsAndChecksExternalFileBeforeCommit(){
        var original=Definition();
        var folder=DirectoryPath();
        var workspace=SourceWorkspace.Create(folder,new(original,1,0),original.Source,RepositoryRoot());
        workspace.MarkDraftSaved(new(original,"admin",1,1,DateTimeOffset.UtcNow));
        var local=original with{Source=original.Source with{Files=[..original.Source.Files,new("Local.cs","public class Local {}")]}};
        workspace.Synchronize(local);
        workspace=SourceWorkspace.Open(folder);
        var remote=original with{Source=original.Source with{Files=[..original.Source.Files,new("Remote.cs","public class Remote {}")]}};
        var plan=workspace.PreviewServerDraftMerge(new(original,1,"admin",DateTimeOffset.UtcNow),new(remote,"admin",2,1,DateTimeOffset.UtcNow),local,local,1,1);
        Assert.All(plan.Comparison.Files,file=>Assert.True(file.Automatic));
        var candidate=plan.Resolve(plan.Comparison.Files.ToDictionary(file=>file.Path,file=>file.PreferManager));
        Assert.Equal(new[]{"Database.cs","Local.cs","Remote.cs"},candidate.Source.Files.Select(file=>file.Path));
        var backup=workspace.PrepareServerDraftMerge(plan,candidate);
        Assert.Equal(remote.Hash(),Seamless.Management.SourceArchive.Import(Path.Combine(backup,"server","definition.seamless.json")).Definition.Hash());
        File.WriteAllText(Path.Combine(folder,"Local.cs"),"public class ChangedAfterPreview {}");
        var saved=new Draft(candidate,"admin",3,1,DateTimeOffset.UtcNow);
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>workspace.FinishServerDraftMerge(plan,candidate,saved)).Code);
        Assert.Equal("public class ChangedAfterPreview {}",File.ReadAllText(Path.Combine(folder,"Local.cs")));
        Assert.False(File.Exists(Path.Combine(folder,"Remote.cs")));
        File.WriteAllText(Path.Combine(folder,"Local.cs"),local.Source.Files[1].Text);
        workspace.FinishServerDraftMerge(plan,candidate,saved);
        Assert.Equal(candidate.Hash(),SourceWorkspace.Open(folder).Current.Definition.Hash());
        Assert.False(workspace.NeedsDraftSave);
    }

    [Fact]public void ServerDraftMergeWithoutKnownAncestorRequiresExplicitFileAndMigrationChoice(){
        var original=Definition();
        var workspace=SourceWorkspace.Create(DirectoryPath(),new(original,1,1),original.Source,RepositoryRoot());
        var remote=original with{
            Source=original.Source with{Files=[new("Database.cs",original.Source.Files[0].Text+"// server\n")]},
            Migration=new(1,[],["Removed"])
        };
        var plan=workspace.PreviewServerDraftMerge(new(original,1,"admin",DateTimeOffset.UtcNow),new(remote,"admin",2,1,DateTimeOffset.UtcNow),original,null,1,1);
        Assert.Equal(2,plan.Comparison.Files.Length);
        Assert.All(plan.Comparison.Files,file=>{Assert.False(file.Automatic);Assert.Null(file.Baseline);});
        Assert.Throws<ManagementException>(()=>plan.Resolve(new Dictionary<string,string?>()));
        var chosen=plan.Resolve(plan.Comparison.Files.ToDictionary(file=>file.Path,file=>file.PreferDisk));
        Assert.Equal(remote.Hash(),chosen.Hash());
    }

    [Fact]public void PublishedRebaseRetainsIndependentSourceAndMigrationAndUpdatesTheWorkspaceBase(){
        var original=Seamless.Management.DatabaseDesigner.Apply(Seamless.Management.DatabaseDesigner.Empty("rebase","Customers"),new("Customers","",[new("Id","int",true)],[])) with{Migration=null};
        var local=Seamless.Management.DatabaseDesigner.Apply(original,Seamless.Management.DatabaseDesigner.Read(original,"Customers") with{Name="Renamed"});
        var latest=original with{Source=original.Source with{Files=[..original.Source.Files,new("Remote.cs","public class Remote {}")]}};
        var folder=DirectoryPath();
        var workspace=SourceWorkspace.Create(folder,new(local,1,1),original.Source,RepositoryRoot());
        var now=DateTimeOffset.UtcNow;
        var remote=new Draft(local,"admin",1,1,now);
        var plan=workspace.PreviewPublishedRebase(new(original,1,"admin",now),new(latest,2,"admin",now),remote,local,local,1);
        var candidate=plan.Resolve(plan.Comparison.Files.ToDictionary(file=>file.Path,file=>file.PreferManager));
        Assert.Contains(candidate.Source.Files,file=>file.Path=="Remote.cs");
        Assert.Equal("Customers",Assert.Single(candidate.Migration!.Tables).Source);
        Assert.Equal("Renamed",Assert.Single(candidate.Migration.Tables).Target);
        Assert.Contains("Renamed",Seamless.Management.DatabaseDesigner.Tables(candidate));
        workspace.PrepareServerDraftMerge(plan,candidate);
        workspace.FinishServerDraftMerge(plan,candidate,new(candidate,"admin",2,2,now));
        var reopened=SourceWorkspace.Open(folder);
        Assert.Equal(2,reopened.Current.BaseVersion);
        Assert.Equal(latest.Source.Hash(),reopened.State.Published.Hash());
        Assert.True(reopened.HasUnappliedChanges);
    }

    [Fact]public void PublishedRebaseRejectsChangedMigrationSourcesWithoutChangingTheDraft(){
        var original=Seamless.Management.DatabaseDesigner.Apply(Seamless.Management.DatabaseDesigner.Empty("rebase","Customers"),new("Customers","",[new("Id","int",true)],[])) with{Migration=null};
        var design=Seamless.Management.DatabaseDesigner.Read(original,"Customers");
        var local=Seamless.Management.DatabaseDesigner.Apply(original,design with{Name="Renamed"});
        var latest=Seamless.Management.DatabaseDesigner.Apply(original,design with{Columns=[..design.Columns,new("Number","int",false)]});
        var workspace=SourceWorkspace.Create(DirectoryPath(),new(local,1,1),original.Source,RepositoryRoot());
        var now=DateTimeOffset.UtcNow;
        var remote=new Draft(local,"admin",1,1,now);
        var error=Assert.Throws<ManagementException>(()=>workspace.PreviewPublishedRebase(new(original,1,"admin",now),new(latest,2,"admin",now),remote,local,local,1));
        Assert.Equal("MigrationRequired",error.Code);
        Assert.Equal(1,workspace.Current.BaseVersion);
        Assert.Equal(local.Hash(),workspace.Current.Definition.Hash());
    }

    [Fact]public void InterruptedPublishedRebaseRecoversOnlyTheRecordedServerDraft(){
        var original=Definition();
        var latest=original with{Source=original.Source with{Files=[..original.Source.Files,new("Remote.cs","public class Remote {}")]}};
        var folder=DirectoryPath();
        var workspace=SourceWorkspace.Create(folder,new(original,1,1),original.Source,RepositoryRoot());
        var now=DateTimeOffset.UtcNow;
        var published=new DefinitionVersion(latest,2,"admin",now);
        var plan=workspace.PreviewPublishedRebase(new(original,1,"admin",now),published,new(original,"admin",1,1,now),original,original,1);
        var candidate=plan.Resolve(plan.Comparison.Files.ToDictionary(file=>file.Path,file=>file.PreferManager));
        workspace.PrepareServerDraftMerge(plan,candidate);
        var path=Path.Combine(folder,"Database.cs");
        File.AppendAllText(path,"// after the server saved\n");
        workspace.Synchronize(original);
        workspace=SourceWorkspace.Open(folder);
        var saved=new Draft(candidate,"admin",2,2,now);
        Assert.Throws<ManagementException>(()=>workspace.PreviewServerDraftMerge(published,saved with{Revision=3},original,original,1,1));
        var recovery=workspace.PreviewServerDraftMerge(published,saved,original,original,1,1);
        var result=recovery.Resolve(recovery.Comparison.Files.ToDictionary(file=>file.Path,file=>file.PreferManager));
        Assert.Contains(result.Source.Files,file=>file.Path=="Remote.cs");
        Assert.Contains("after the server saved",result.Source.Files[0].Text);
        workspace.FinishServerDraftMerge(recovery,result,new(result,"admin",3,2,now));
        Assert.Equal(2,SourceWorkspace.Open(folder).Current.BaseVersion);
        Assert.Contains("after the server saved",File.ReadAllText(path));
    }

    [Fact]public async Task ACompletedOlderDelayCannotDispatchANewerSaveBeforeItsDebounce(){
        var clock=new DebounceClock();
        var count=0;
        Func<IReadOnlyList<string>,Task> callback=_=>{Interlocked.Increment(ref count);return Task.CompletedTask;};
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var folder=DirectoryPath();Directory.CreateDirectory(folder);
        using var watcher=(SourceWatcher)Activator.CreateInstance(typeof(SourceWatcher),flags,null,[folder,callback,300,clock],null)!;
        var gate=typeof(SourceWatcher).GetField("gate",flags)!.GetValue(watcher)!;
        var schedule=typeof(SourceWatcher).GetMethod("ScheduleDebounce",flags)!;
        Task previous,latest;
        var path=Path.Combine(folder,"Database.cs");
        lock(gate){
            previous=(Task)schedule.Invoke(watcher,[path])!;
            clock.CompleteTimers();
            latest=(Task)schedule.Invoke(watcher,[path])!;
        }
        await previous.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0,Volatile.Read(ref count));
        clock.CompleteTimers();
        await latest.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1,Volatile.Read(ref count));
        var next=(Task)schedule.Invoke(watcher,[path])!;
        clock.CompleteTimers();
        await next.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2,Volatile.Read(ref count));
    }
    private sealed class DebounceClock:TimeProvider{
        private readonly List<DebounceTimer> timers=[];
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period){
            var timer=new DebounceTimer(callback,state);
            this.timers.Add(timer);
            return timer;
        }
        internal void CompleteTimers(){
            var pending=this.timers.ToArray();this.timers.Clear();
            foreach(var timer in pending){timer.Complete();}
        }
        private sealed class DebounceTimer(TimerCallback callback,object? state):ITimer{
            private int completed;
            public bool Change(TimeSpan dueTime,TimeSpan period)=>throw new NotSupportedException();
            internal void Complete(){if(Interlocked.Exchange(ref this.completed,1)==0){callback(state);}}
            public void Dispose()=>Interlocked.Exchange(ref this.completed,1);
            public ValueTask DisposeAsync(){this.Dispose();return ValueTask.CompletedTask;}
        }
    }
    [Fact]public async Task ExternalFileChangesReachTheWorkspaceAfterDebounce(){
        var original=Definition();
        var folder=DirectoryPath();
        var workspace=SourceWorkspace.Create(folder,new(original,1,0),original.Source,RepositoryRoot());
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        var notifications=new System.Collections.Concurrent.ConcurrentQueue<long>();
        var writes=new List<long>();
        var path=Path.Combine(folder,"Database.cs");
        var latest=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher=workspace.CreateWatcher(files=>{
            notifications.Enqueue(elapsed.ElapsedMilliseconds);
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var reader=new StreamReader(stream);
            if(files.Contains(path)&&reader.ReadToEnd().Contains("// mod 3\n",StringComparison.Ordinal)){latest.TrySetResult(true);}
            return Task.CompletedTask;
        },debounceMilliseconds:60);
        workspace.ActiveWatcher=watcher;

        // OS通知や継続の実行間隔を固定せず、最後の保存内容の通知を確認する。
        File.AppendAllText(path,"// mod 1\n");
        writes.Add(elapsed.ElapsedMilliseconds);
        await Task.Delay(20);
        File.AppendAllText(path,"// mod 2\n");
        writes.Add(elapsed.ElapsedMilliseconds);
        await Task.Delay(20);
        File.AppendAllText(path,"// mod 3\n");
        writes.Add(elapsed.ElapsedMilliseconds);

        await latest.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var timing=$"Writes(ms)=[{string.Join(',',writes)}], notifications(ms)=[{string.Join(',',notifications)}]";
        output.WriteLine(timing);
        Assert.True(watcher.ExternalChangeTriggerCount>=1);

        var synchronized=workspace.Synchronize(original);
        Assert.Contains("// mod 3",synchronized.Definition.Source.Files[0].Text);
        Assert.True(workspace.NeedsDraftSave);
    }

    [Fact]public async Task ManagerSelfWrite_IgnoredByWatcher_PreventsCycleLoop(){
        var original=Definition();
        var folder=DirectoryPath();
        var workspace=SourceWorkspace.Create(folder,new(original,1,0),original.Source,RepositoryRoot());
        var externalTriggerCount=0;
        using var watcher=workspace.CreateWatcher(_=>{
            Interlocked.Increment(ref externalTriggerCount);
            return Task.CompletedTask;
        },debounceMilliseconds:60);
        workspace.ActiveWatcher=watcher;

        var path=Path.Combine(folder,"Database.cs");
        var selfText=original.Source.Files[0].Text+"// written by manager\n";

        // Manager 自前のファイル書き込みを登録して書き込む
        watcher.RecordSelfWritten("Database.cs",selfText);
        File.WriteAllText(path,selfText);

        // デバウンス時間を超えて待機
        await Task.Delay(200);

        // 自前書き込みは無視され、外部変更コールバックは呼ばれない
        Assert.Equal(0,externalTriggerCount);
        Assert.True(watcher.IgnoredSelfWriteCount>0,"自前書き込みが除外されていません。");
    }

    [Fact]public void ThreeAxisState_AccuratelyVisualized(){
        var original=Definition();
        var folder=DirectoryPath();
        var workspace=SourceWorkspace.Create(folder,new(original,1,0),original.Source,RepositoryRoot());

        // ファイル作成だけではサーバーの下書き保管を確認できない。保管応答を明示する。
        Assert.True(workspace.NeedsDraftSave);
        workspace.MarkDraftSaved(0,1,original.Hash());

        // 状態1: 初期状態（DB 作成直後、稼働版適用済み）
        // 軸1: ファイル保存済み（Disk == Workspace）
        // 軸2: 下書き保存済み（NeedsDraftSave == false）
        // 軸3: 稼働DB適用済み（HasUnappliedChanges == false）
        Assert.False(workspace.NeedsDraftSave);
        Assert.False(workspace.HasUnappliedChanges);

        // 状態2: 外部 IDE でファイルを変更
        var path=Path.Combine(folder,"Database.cs");
        File.WriteAllText(path,original.Source.Files[0].Text.Replace("Name","DisplayName"));
        var syncResult=workspace.Synchronize(original);

        // 軸1: ファイル保存済み（外部で保存済み）
        // 軸2: 下書き保存待ち（NeedsDraftSave == true）
        // 軸3: 稼働DBへ未適用（HasUnappliedChanges == true）
        Assert.True(workspace.NeedsDraftSave);
        Assert.True(workspace.HasUnappliedChanges);
        Assert.Contains("DisplayName",syncResult.Definition.Source.Files[0].Text);

        // 状態3: 下書きを保存（MarkDraftSaved）
        workspace.MarkDraftSaved(1,1,syncResult.Definition.Hash());

        // 軸1: ファイル保存済み
        // 軸2: 下書き保存済み（NeedsDraftSave == false）
        // 軸3: 稼働DBへ未適用（HasUnappliedChanges == true）
        Assert.False(workspace.NeedsDraftSave);
        Assert.True(workspace.HasUnappliedChanges);

        // 状態4: 稼働 DB へ適用（MarkPublished）
        workspace.MarkPublished(syncResult.Definition,2);

        // 公開後の基準版で保管した下書きの応答を記録するまで、保管済みとは表示しない。
        Assert.True(workspace.NeedsDraftSave);
        workspace.MarkDraftSaved(2,2,syncResult.Definition.Hash());

        // 軸1: ファイル保存済み
        // 軸2: 下書き保存済み（NeedsDraftSave == false）
        // 軸3: 稼働DB適用済み（HasUnappliedChanges == false）
        Assert.False(workspace.NeedsDraftSave);
        Assert.False(workspace.HasUnappliedChanges);
        Assert.Equal(2,workspace.Current.BaseVersion);
    }

    private static string RepositoryRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null){if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){return directory.FullName;}directory=directory.Parent;}return Environment.GetEnvironmentVariable("SEAMLESS_SOURCE_REPOSITORY")??throw new InvalidOperationException("リポジトリの場所が必要です。");}
}
