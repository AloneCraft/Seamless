using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class SourceProjectConflicts{
    private const string Original="namespace Example;\ninternal static partial class DatabaseGenerator;\npublic class Row {\npublic int First=1;\npublic int Second=2;\n}\n// keep this comment\n";
    private static string Root(){var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"conflict-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private static string CreateProject(string root){var project=Path.Combine(root,"Example.csproj");File.WriteAllText(project,"<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");File.WriteAllText(Path.Combine(root,"Db.cs"),Original);return project;}
    private static SourceSnapshot Replace(SourceSnapshot source,string before,string after)=>source with{Files=source.Files.Select(file=>file with{Text=file.Text.Replace(before,after,StringComparison.Ordinal)}).ToArray()};
    private static async Task<(SourceProjectDocument Document,SourceSnapshot Editor,SourceConflict Conflict,string File)> Conflict(IManagementSourceOperations operations,string project){
        var opened=await operations.OpenSourceProjectAsync(new(project));var editor=Replace(opened.Source!,"First=1","First=10");var path=Path.Combine(Path.GetDirectoryName(project)!,"Db.cs");
        File.WriteAllText(path,Original.Replace("First=1","First=30").Replace("Second=2","Second=20"));
        Assert.Equal("SourceConflict",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SynchronizeSourceProjectAsync(new(opened.Id,opened.Source!,editor)))).Code);
        var result=await operations.InspectSourceConflictAsync(new(opened.Id,editor));Assert.False(result.AdditionalEditorChanges);return(opened,editor,result.Conflict!,path);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task EitherPreferenceKeepsNonconflictingExternalChangesAndComments(bool manager){
        using var service=new SourceProjectService();var state=await Conflict(service,CreateProject(Root()));var conflict=state.Conflict;var file=Assert.Single(conflict.Files);
        var text=file.PreferDisk;if(manager){text=file.PreferManager;}
        var result=await service.ResolveSourceConflictAsync(new(state.Document.Id,conflict.Id,conflict.DiskHash,conflict.ManagerHash,state.Editor,new(){{file.Path,text}}));
        var saved=File.ReadAllText(state.File);Assert.Contains("Second=20",saved);Assert.Contains("// keep this comment",saved);Assert.Equal(result.Document.Source!.Files.Single().Text,saved);
        var expected="First=30";if(manager){expected="First=10";}Assert.Contains(expected,saved);Assert.Null((await service.InspectSourceConflictAsync(new(state.Document.Id,result.Document.Source!))).Conflict);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ChangedDiskOrManagerHashRejectsTheStaleResolution(bool diskChanged){
        using var service=new SourceProjectService();var state=await Conflict(service,CreateProject(Root()));var conflict=state.Conflict;var hash=conflict.ManagerHash;
        if(diskChanged){File.AppendAllText(state.File,"// another IDE save\n");}else{hash="stale";}
        var before=File.ReadAllBytes(state.File);
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.ResolveSourceConflictAsync(new(state.Document.Id,conflict.Id,conflict.DiskHash,hash,state.Editor,new(){{"Db.cs",conflict.Files.Single().PreferManager}})))).Code);
        Assert.Equal(before,File.ReadAllBytes(state.File));Assert.NotNull((await service.InspectSourceConflictAsync(new(state.Document.Id,state.Editor))).Conflict);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ManualReplacementIsSavedAndCompileFileDeletionIsRejected(bool delete){
        var root=Root();var project=CreateProject(root);var file=Path.Combine(root,"Extra.cs");
        File.WriteAllText(file,"namespace Example; public class Extra { public int Value=1; }");
        using(var service=new SourceProjectService()){
            var opened=await service.OpenSourceProjectAsync(new(project));var editor=Replace(opened.Source!,"Value=1","Value=10");
            File.WriteAllText(file,"namespace Example; public class Extra { public int Value=30; }");
            Assert.Equal("SourceConflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.SynchronizeSourceProjectAsync(new(opened.Id,opened.Source!,editor)))).Code);
            var conflict=(await service.InspectSourceConflictAsync(new(opened.Id,editor))).Conflict!;
            string? replacement="namespace Example; public class Extra { public int Value=40; }";
            if(delete){
                var before=File.ReadAllBytes(file);
                Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.ResolveSourceConflictAsync(new(opened.Id,conflict.Id,conflict.DiskHash,conflict.ManagerHash,editor,new(){{"Extra.cs",null}})))).Code);
                Assert.Equal(before,File.ReadAllBytes(file));
                Assert.NotNull((await service.InspectSourceConflictAsync(new(opened.Id,editor))).Conflict);
                return;
            }
            var result=await service.ResolveSourceConflictAsync(new(opened.Id,conflict.Id,conflict.DiskHash,conflict.ManagerHash,editor,new(){{"Extra.cs",replacement}}));
            Assert.Equal(!delete,File.Exists(file));Assert.Equal(Original,File.ReadAllText(Path.Combine(root,"Db.cs")));
            if(!delete){Assert.Equal(replacement,File.ReadAllText(file));}
            Assert.False(result.Document.HasConflict);
        }
        using var reopened=new SourceProjectService();var document=await reopened.OpenSourceProjectAsync(new(project));
        Assert.False(document.HasConflict);Assert.Equal(!delete,document.Source!.Files.Any(source=>source.Path=="Extra.cs"));
    }
    [Fact]public async Task LaterEditorInputIsBackedUpBeforeApplyingTheChosenResolution(){
        using var service=new SourceProjectService();var state=await Conflict(service,CreateProject(Root()));var editor=Replace(state.Editor,"// keep this comment","// keep this comment\n// additional Manager input");
        Assert.True((await service.InspectSourceConflictAsync(new(state.Document.Id,editor))).AdditionalEditorChanges);
        var conflict=state.Conflict;var result=await service.ResolveSourceConflictAsync(new(state.Document.Id,conflict.Id,conflict.DiskHash,conflict.ManagerHash,editor,new(){{"Db.cs",conflict.Files.Single().PreferManager}}));
        Assert.NotNull(result.EditorBackup);Assert.Contains(Directory.GetFiles(result.EditorBackup!,"*.cs",SearchOption.AllDirectories),path=>File.ReadAllText(path).Contains("additional Manager input",StringComparison.Ordinal));Assert.Contains("Second=20",File.ReadAllText(state.File));
    }
    [Fact]public async Task PendingConflictCanBeReopenedWithoutLosingManagerOrDiskInput(){
        var project=CreateProject(Root());string id;string disk;
        using(var first=new SourceProjectService()){var state=await Conflict(first,project);id=state.Document.Id;disk=File.ReadAllText(state.File);}
        using var service=new SourceProjectService();var opened=await service.OpenSourceProjectAsync(new(project));Assert.True(opened.HasConflict);Assert.Equal(id,opened.Id);Assert.Contains("First=10",opened.Source!.Files.Single().Text);Assert.Equal(disk,File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!,"Db.cs")));
        var conflict=(await service.InspectSourceConflictAsync(new(id,opened.Source!))).Conflict!;
        var result=await service.ResolveSourceConflictAsync(new(id,conflict.Id,conflict.DiskHash,conflict.ManagerHash,opened.Source!,new(){{"Db.cs",conflict.Files.Single().PreferDisk}}));Assert.False(result.Document.HasConflict);
    }
    [Fact]public async Task CancellationKeepsTheConflictAndBothInputs(){
        using var service=new SourceProjectService();var state=await Conflict(service,CreateProject(Root()));var before=File.ReadAllBytes(state.File);using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.ResolveSourceConflictAsync(new(state.Document.Id,state.Conflict.Id,state.Conflict.DiskHash,state.Conflict.ManagerHash,state.Editor,new(){{"Db.cs",state.Conflict.Files.Single().PreferManager}}),cancellation.Token));
        Assert.Equal(before,File.ReadAllBytes(state.File));Assert.NotNull((await service.InspectSourceConflictAsync(new(state.Document.Id,state.Editor))).Conflict);
    }
    [Fact]public async Task OwnerPipeExposesTheSameConflictOperations(){
        var root=Root();using var store=ManagementStore.Initialize(Path.Combine(root,"store"),"admin","TestPassword-123");using var management=new Seamless.Management.Server.ManagementService(store);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(management);server.Start();using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Contains("OwnerSourceConflictsV1",(await client.InfoAsync()).Capabilities);
        var state=await Conflict(client,CreateProject(root));var conflict=state.Conflict;
        var result=await client.ResolveSourceConflictAsync(new(state.Document.Id,conflict.Id,conflict.DiskHash,conflict.ManagerHash,state.Editor,new(){{"Db.cs",conflict.Files.Single().PreferManager}}));Assert.Contains("First=10",result.Document.Source!.Files.Single().Text);
    }
}
