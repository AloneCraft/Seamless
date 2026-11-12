using Seamless.Management;
using Seamless.Management.Server;
using System.Net;

namespace Management.Tests;

public sealed class SourceProjects{
    [Fact]public async Task BrowserListsOneServerDirectoryAndProjectFilesWithoutCreatingWorkspace(){
        var root=Root();
        Directory.CreateDirectory(Path.Combine(root,"Child"));
        File.WriteAllText(Path.Combine(root,"Example.slnx"),"<Solution/>");
        File.WriteAllText(Path.Combine(root,"Database.csproj"),"<Project/>");
        File.WriteAllText(Path.Combine(root,"Ignored.cs"),"class Ignored {}");
        using var service=new SourceProjectService();
        var directory=await service.BrowseSourceDirectoryAsync(root);
        Assert.Equal(Path.GetFullPath(root),directory.Path);
        Assert.Equal(Directory.GetParent(root)!.FullName,directory.Parent);
        Assert.Equal(["Child","Database.csproj","Example.slnx"],directory.Entries.Select(entry=>entry.Name));
        Assert.True(directory.Entries[0].IsDirectory);
        Assert.All(directory.Entries,entry=>Assert.Equal(Path.Combine(root,entry.Name),entry.Path));
        Assert.Equal(root,(await service.BrowseSourceDirectoryAsync(Path.Combine(root,"Example.slnx"))).Path);
        Assert.False(Directory.Exists(Path.Combine(root,".seamless")));
        var child=await service.BrowseSourceDirectoryAsync(directory.Entries[0].Path);
        Assert.Empty(child.Entries);Assert.Equal(root,child.Parent);
        Assert.Equal("NotFound",(await Assert.ThrowsAsync<ManagementException>(()=>service.BrowseSourceDirectoryAsync(Path.Combine(root,"Missing")))).Code);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.BrowseSourceDirectoryAsync(root,cancellation.Token));
        var drives=await service.BrowseSourceDirectoryAsync("");
        Assert.All(drives.Entries,entry=>Assert.True(entry.IsDirectory));
        Assert.NotEmpty(drives.Entries);
    }
    [Theory][InlineData("true",false)][InlineData("True",false)][InlineData("false",true)]
    public async Task ConditionalNamespacesUseExplicitAndSdkDefines(string disabled,bool framework){
        var root=Root();var project=Path.Combine(root,"Db.csproj");
        File.WriteAllText(project,$"<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework><DefineConstants>$(DefineConstants);CUSTOM , EXTRA</DefineConstants><DisableImplicitFrameworkDefines>{disabled}</DisableImplicitFrameworkDefines></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(root,"Db.cs"),"#if CUSTOM && EXTRA\nnamespace Explicit { class DatabaseGenerator {} }\n#endif\n#if NET10_0_OR_GREATER\nnamespace Framework { class DatabaseGenerator {} }\n#endif");
        using var service=new SourceProjectService();var opened=await service.OpenSourceProjectAsync(new(project));
        Assert.Contains("Explicit",opened.Namespaces);Assert.Equal(framework,opened.Namespaces.Contains("Framework"));
    }
    [Fact]public Task SourceFilesRequireVerifiedOwnerIpcAndBrowserSessionIsRechecked()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Root();var project=Path.Combine(root,"Db.csproj");File.WriteAllText(project,"<Project/>");
        using var store=ManagementStore.Initialize(Path.Combine(root,"store"),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service,certificate,new(IPAddress.Loopback,0));server.Start();
        using var local=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Contains((await local.BrowseSourceDirectoryAsync(root)).Entries,entry=>entry.Path==project);
        Assert.Contains("OwnerSourceProjectsV1",(await local.InfoAsync()).Capabilities);
        Assert.Contains("OwnerSourceDirectoryV1",(await local.InfoAsync()).Capabilities);
        Assert.Equal([project],(await local.OpenSourceLocationAsync(project)).Projects);
        using var remote=new ManagementClient("localhost",server.Port,certificate);
        Assert.DoesNotContain("OwnerSourceProjectsV1",(await remote.InfoAsync()).Capabilities);
        Assert.DoesNotContain("OwnerSourceDirectoryV1",(await remote.InfoAsync()).Capabilities);
        await remote.LoginAsync("admin","TestPassword-123");
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.OpenSourceLocationAsync(project))).Code);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.BrowseSourceDirectoryAsync(root))).Code);
        using var sessions=new LocalBrowserSessions(store.InstanceId);sessions.BindOrigin(new("http://127.0.0.1:25001/"));
        var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:25001");
        var browser=new BrowserManagementOperations(sessions,local,()=>Task.FromResult<string?>(session.Id));
        Assert.Contains((await browser.BrowseSourceDirectoryAsync(root)).Entries,entry=>entry.Path==project);
        Assert.Equal([project],(await browser.OpenSourceLocationAsync(project)).Projects);
        sessions.RevokeAll();
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.BrowseSourceDirectoryAsync(root))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.OpenSourceLocationAsync(project))).Code);
    });
    [Fact]public async Task SolutionAndFolderResolveProjectsAndEvaluateTheActualCompileSet(){
        var root=Root();var directory=Path.Combine(root,"Db");Directory.CreateDirectory(directory);
        var project=Path.Combine(directory,"Db.csproj");
        File.WriteAllText(project,"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            <ItemGroup><Compile Remove="Excluded.cs"/><Compile Include="../Shared.cs" Link="Shared.cs"/></ItemGroup>
            <Target Name="MustNotRun" BeforeTargets="Build"><Error Text="Opening a project must not build it"/></Target></Project>
            """);
        File.WriteAllText(Path.Combine(directory,"Db.cs"),"namespace Example { internal static partial class DatabaseGenerator; }\n#if NET10_0_OR_GREATER\nnamespace CurrentFramework { class DatabaseGenerator {} }\n#endif\n#if NEVER\nnamespace Ignored { class DatabaseGenerator {} }\n#endif");
        File.WriteAllText(Path.Combine(directory,"Excluded.cs"),"namespace Excluded; class DatabaseGenerator {}");
        File.WriteAllText(Path.Combine(root,"Shared.cs"),"namespace Shared; public class Value {}");
        var solution=Path.Combine(root,"Sample.slnx");File.WriteAllText(solution,"<Solution><Folder Name='/DB/'><Project Path='Db/Db.csproj'/></Folder></Solution>");
        using var service=new SourceProjectService();
        Assert.Equal([project],(await service.OpenSourceLocationAsync(solution)).Projects);
        Assert.Equal([project],(await service.OpenSourceLocationAsync(root)).Projects);
        var opened=await service.OpenSourceProjectAsync(new(project));
        Assert.Equal(["CurrentFramework","Example"],opened.Namespaces);
        Assert.Equal(2,opened.Source!.Files.Length);
        Assert.Contains(opened.Source.Files,file=>file.Path.StartsWith("Linked/",StringComparison.Ordinal));
        Assert.DoesNotContain(opened.Source.Files,file=>file.Path=="Excluded.cs");
    }
    [Fact]public async Task ExistingProjectSynchronizesWithoutChangingItsCompileSettingsAndPreservesConflicts(){
        var root=Root();var project=Path.Combine(root,"Db.csproj");
        const string configuration="<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
        File.WriteAllText(project,configuration);
        var path=Path.Combine(root,"Db.cs");
        const string original="namespace Example;\ninternal static partial class DatabaseGenerator;\npublic class Row {\npublic int First=1;\npublic int Second=2;\n}\n";
        File.WriteAllText(path,original);
        using var service=new SourceProjectService();
        var opened=await service.OpenSourceProjectAsync(new(project));var baseline=opened.Source!;
        var manager=Replace(baseline,"First=1","First=10");
        File.WriteAllText(path,original.Replace("Second=2","Second=20"));
        var saved=await service.SynchronizeSourceProjectAsync(new(opened.Id,baseline,manager));
        Assert.Contains("First=10",File.ReadAllText(path));Assert.Contains("Second=20",File.ReadAllText(path));
        Assert.Equal(configuration,File.ReadAllText(project));
        var external=File.ReadAllText(path).Replace("First=10","First=30");File.WriteAllText(path,external);
        var conflict=await Assert.ThrowsAsync<ManagementException>(()=>service.SynchronizeSourceProjectAsync(new(opened.Id,saved.Source!,Replace(saved.Source!,"First=10","First=40"))));
        Assert.Equal("SourceConflict",conflict.Code);Assert.Equal(external,File.ReadAllText(path));
        Assert.Equal(configuration,File.ReadAllText(project));
    }
    [Fact]public async Task ForgedFilesAndChangedCompileSetsAreRejectedBeforeWriting(){
        var root=Root();var project=Path.Combine(root,"Db.csproj");
        File.WriteAllText(project,"<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var path=Path.Combine(root,"Db.cs");const string original="namespace Example; internal static partial class DatabaseGenerator;";File.WriteAllText(path,original);
        using var service=new SourceProjectService();var opened=await service.OpenSourceProjectAsync(new(project));var baseline=opened.Source!;
        var forged=baseline with{Files=[..baseline.Files,new("Other.cs","not allowed")]};
        Assert.Equal("InvalidRequest",(await Assert.ThrowsAsync<ManagementException>(()=>service.SynchronizeSourceProjectAsync(new(opened.Id,baseline,forged)))).Code);
        Assert.False(File.Exists(Path.Combine(root,"Other.cs")));
        File.WriteAllText(Path.Combine(root,"New.cs"),"namespace Example; class New {}");
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.SynchronizeSourceProjectAsync(new(opened.Id,baseline,Replace(baseline,"Example","Changed"))))).Code);
        Assert.Equal(original,File.ReadAllText(path));
        var reopened=await service.OpenSourceProjectAsync(new(project));
        Assert.Equal(2,reopened.Source!.Files.Length);
        var saved=await service.SynchronizeSourceProjectAsync(new(reopened.Id,reopened.Source,Replace(reopened.Source,"Example","Changed")));
        Assert.Contains("Changed",File.ReadAllText(path));
        Assert.Equal(2,saved.Source!.Files.Length);
    }
    [Fact]public async Task CreateSourceProject_GeneratesProjectFilesAndBindsToService(){
        var root=Root();var destination=Path.Combine(root,"SalesApp");
        var definition=Seamless.Management.DatabaseDesigner.Empty("sales-id","Sales");
        using var service=new SourceProjectService();
        var document=await service.CreateSourceProjectAsync(new(destination,"Sales",new(definition,1,0)));
        Assert.Equal(Path.Combine(destination,"Sales.csproj"),document.ProjectPath);
        Assert.True(File.Exists(Path.Combine(destination,"Sales.csproj")));
        Assert.True(File.Exists(Path.Combine(destination,"NuGet.Config")));
        var localFeed=System.Xml.Linq.XDocument.Load(Path.Combine(destination,"NuGet.Config")).Descendants("add").Single(element=>(string?)element.Attribute("key")=="SeamlessLocal").Attribute("value")!.Value;
        Assert.True(Path.IsPathFullyQualified(localFeed),localFeed);
        Assert.True(Directory.Exists(localFeed),localFeed);
        Assert.True(File.Exists(Path.Combine(destination,"global.json")));
        Assert.True(File.Exists(Path.Combine(destination,".vscode","tasks.json")));
        Assert.True(File.Exists(Path.Combine(destination,".gitignore")));
        Assert.True(File.Exists(Path.Combine(destination,"Database.cs")));
        Assert.Contains("Seamless",File.ReadAllText(Path.Combine(destination,"Sales.csproj")));
        var project=System.Xml.Linq.XDocument.Load(document.ProjectPath);
        Assert.Equal("true",(string?)project.Root!.Element("PropertyGroup")!.Element("CopyLocalLockFileAssemblies"));
        Assert.Contains("UserCode",document.Namespaces);
        Assert.Equal("net10.0",document.TargetFramework);
        if(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_LOG") is{} log){
            Directory.CreateDirectory(log);
            await File.WriteAllTextAsync(Path.Combine(log,"created-project.workspace.json"),System.Text.Json.JsonSerializer.Serialize(new{project=document.ProjectPath}));
        }
        var baseline=document.Source!;
        var updated=Replace(baseline,"public sealed partial class ManagedDb","// updated\npublic sealed partial class ManagedDb");
        var synchronized=await service.SynchronizeSourceProjectAsync(new(document.Id,baseline,updated));
        Assert.Contains("// updated",File.ReadAllText(Path.Combine(destination,"Database.cs")));
        var workspace=SourceWorkspace.Open(destination);
        Assert.Equal(definition.Id,workspace.Current.Definition.Id);
        Assert.Equal(1,workspace.Current.BaseVersion);
        Assert.Contains("// updated",workspace.Current.Definition.Source.Files.Single(file=>file.Path=="Database.cs").Text);
        using var reopenedService=new SourceProjectService();
        var reopened=await reopenedService.OpenSourceProjectAsync(new(document.ProjectPath));
        var second=Replace(reopened.Source!,"// updated","// updated twice");
        await reopenedService.SynchronizeSourceProjectAsync(new(reopened.Id,reopened.Source!,second));
        Assert.Contains("// updated twice",SourceWorkspace.Open(destination).Current.Definition.Source.Files.Single(file=>file.Path=="Database.cs").Text);
    }
    [Theory][InlineData("Feed & cache")][InlineData("日本語フィード")]
    public void PackageProjectPreservesTheExplicitFeedPath(string feedName){
        var root=Root();var feed=Path.Combine(root,feedName);Directory.CreateDirectory(feed);
        var destination=Path.Combine(root,"Sales");
        var definition=Seamless.Management.DatabaseDesigner.Empty("sales-id","Sales");
        SourceWorkspace.CreatePackageProject(destination,new(definition,1,0),definition.Source,"Sales",packageFeed:feed);
        var config=System.Xml.Linq.XDocument.Load(Path.Combine(destination,"NuGet.Config"));
        var entry=config.Descendants("add").Single(element=>(string?)element.Attribute("key")=="SeamlessLocal");
        Assert.Equal(feed,(string?)entry.Attribute("value"));
    }
    [Fact]public void MissingExplicitFeedDoesNotCreateAPartialProject(){
        var root=Root();var destination=Path.Combine(root,"Sales");
        var definition=Seamless.Management.DatabaseDesigner.Empty("sales-id","Sales");
        var error=Assert.Throws<ManagementException>(()=>SourceWorkspace.CreatePackageProject(destination,new(definition,1,0),definition.Source,"Sales",packageFeed:Path.Combine(root,"missing")));
        Assert.Equal("PackageFeed",error.Code);
        Assert.False(Directory.Exists(destination));
    }
    [Fact]public async Task CreateSourceProject_NonEmptyDirectory_ThrowsConflictAndPreservesExistingFiles(){
        var root=Root();var destination=Path.Combine(root,"ExistingApp");
        Directory.CreateDirectory(destination);
        var existingFile=Path.Combine(destination,"Important.txt");
        File.WriteAllText(existingFile,"DO NOT OVERWRITE");
        var definition=Seamless.Management.DatabaseDesigner.Empty("sales-id","Sales");
        using var service=new SourceProjectService();
        var ex=await Assert.ThrowsAsync<ManagementException>(()=>service.CreateSourceProjectAsync(new(destination,"Sales",new(definition,1,0))));
        Assert.Equal("Conflict",ex.Code);
        Assert.Equal("DO NOT OVERWRITE",File.ReadAllText(existingFile));
        Assert.False(File.Exists(Path.Combine(destination,"Sales.csproj")));
    }
    [Theory][InlineData("inherit")][InlineData("canonical-edited")][InlineData("conflict")][InlineData("pending")][InlineData("pending-only")]
    public async Task CreatedProjectPreservesLegacyEditsUndoAndUnfinishedWork(string state){
        var destination=Path.Combine(Root(),"SalesApp");
        var definition=Seamless.Management.DatabaseDesigner.Empty("sales-id","Sales");
        var (workspace,project)=SourceWorkspace.CreatePackageProject(destination,new(definition,1,0),definition.Source,"Sales");
        var baseline=workspace.Current.Definition.Source;
        var changed=Replace(baseline,"public sealed partial class ManagedDb","// legacy edit\npublic sealed partial class ManagedDb");
        var identity=project;
        if(OperatingSystem.IsWindows()){identity=identity.ToUpperInvariant();}
        var id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        var metadata=Path.Combine(destination,".seamless","projects",id);
        Directory.CreateDirectory(metadata);
        var legacy=new WorkspaceState(1,new(new Definition("source-"+id,"Sales",DefinitionKind.Database,"","",changed),0,0),
            baseline,changed,baseline,null,ProjectBinding:workspace.State.ProjectBinding);
        var legacyFile=Path.Combine(metadata,"workspace.json");
        File.WriteAllText(legacyFile,System.Text.Json.JsonSerializer.Serialize(legacy,ManagementJson.Options));
        if(state=="canonical-edited"){
            workspace.Synchronize(definition with{Source=Replace(baseline,"ManagedDb","ChangedDb")});
        }
        var code=Path.Combine(destination,"Database.cs");
        File.WriteAllText(code,changed.Files.Single(file=>file.Path=="Database.cs").Text);
        if(state=="conflict"){
            File.WriteAllText(Path.Combine(metadata,"conflict.json"),System.Text.Json.JsonSerializer.Serialize(
                new{Id=Guid.NewGuid(),Baseline=changed,ManagerBase=(Definition?)null,Manager=legacy.Current.Definition,Disk=baseline},ManagementJson.Options));
        }
        if(state is "pending" or "pending-only"){
            File.WriteAllText(Path.Combine(metadata,"pending.json"),System.Text.Json.JsonSerializer.Serialize(new{State=legacy,Writes=Array.Empty<object>()},ManagementJson.Options));
        }
        if(state=="pending-only"){File.Delete(legacyFile);}
        byte[]? originalLegacy=null;
        if(File.Exists(legacyFile)){originalLegacy=File.ReadAllBytes(legacyFile);}
        var canonicalFile=Path.Combine(destination,".seamless","workspace.json");
        var originalCanonical=File.ReadAllBytes(canonicalFile);
        using var service=new SourceProjectService();
        if(state!="inherit"){
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.OpenSourceProjectAsync(new(project)))).Code);
            if(originalLegacy is not null){Assert.Equal(originalLegacy,File.ReadAllBytes(legacyFile));}
            else{Assert.False(File.Exists(legacyFile));}
            Assert.Equal(originalCanonical,File.ReadAllBytes(canonicalFile));
            Assert.Contains("// legacy edit",File.ReadAllText(code));
            return;
        }
        await service.OpenSourceProjectAsync(new(project));
        var inherited=SourceWorkspace.Open(destination);
        Assert.Equal(definition.Id,inherited.Current.Definition.Id);
        Assert.Equal(1,inherited.Current.BaseVersion);
        Assert.Contains("// legacy edit",inherited.Current.Definition.Source.Files.Single(file=>file.Path=="Database.cs").Text);
        Assert.Equal(originalLegacy,File.ReadAllBytes(legacyFile));
        Assert.NotNull(inherited.State.ImportedProjectStateHash);
        inherited.UndoManager();
        Assert.DoesNotContain("// legacy edit",File.ReadAllText(code));
        await service.OpenSourceProjectAsync(new(project));
        var before=File.ReadAllBytes(canonicalFile);
        await File.AppendAllTextAsync(legacyFile," ");
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.OpenSourceProjectAsync(new(project)))).Code);
        Assert.Equal(before,File.ReadAllBytes(canonicalFile));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CreatedProjectRetainsDatabaseReferencesAcrossReopen(bool legacy){
        var destination=Path.Combine(Root(),"ReferencesApp");
        var definition=Seamless.Management.DatabaseDesigner.Empty("references-id","References");
        SourceReference[] references=[new("Dependency",new string('a',64))];
        definition=definition with{Source=definition.Source with{References=references}};
        var (workspace,project)=SourceWorkspace.CreatePackageProject(destination,new(definition,1,0),definition.Source,"References");
        if(legacy){
            var identity=project;
            if(OperatingSystem.IsWindows()){identity=identity.ToUpperInvariant();}
            var id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
            var metadata=Path.Combine(destination,".seamless","projects",id);
            Directory.CreateDirectory(metadata);
            var source=workspace.Current.Definition.Source with{References=[]};
            var state=new WorkspaceState(1,new(new Definition("source-"+id,"References",DefinitionKind.Database,"","",source),0,0),
                source,source,source,null,ProjectBinding:workspace.State.ProjectBinding);
            File.WriteAllText(Path.Combine(metadata,"workspace.json"),System.Text.Json.JsonSerializer.Serialize(state,ManagementJson.Options));
        }
        using var service=new SourceProjectService();
        for(var attempt=0;attempt<2;attempt++){
            var opened=await service.OpenSourceProjectAsync(new(project));
            Assert.Equal(references,opened.Source!.References);
            var current=SourceWorkspace.Open(destination);
            Assert.Equal(references,current.Current.Definition.Source.References);
            Assert.Equal(references,current.State.Synchronized.References);
            Assert.Equal(references,current.State.WithoutManager.References);
        }
    }
    [Fact]public Task CreateSourceProject_RemoteConnectionWithoutOwner_ThrowsPermission()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Root();var destination=Path.Combine(root,"RemoteApp");
        var definition=Seamless.Management.DatabaseDesigner.Empty("sales-id","Sales");
        using var store=ManagementStore.Initialize(Path.Combine(root,"store"),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service,certificate,new(IPAddress.Loopback,0));server.Start();
        using var remote=new ManagementClient("localhost",server.Port,certificate);
        await remote.LoginAsync("admin","TestPassword-123");
        var ex=await Assert.ThrowsAsync<ManagementException>(()=>remote.CreateSourceProjectAsync(new(destination,"Sales",new(definition,1,0))));
        Assert.Equal("Permission",ex.Code);
        Assert.False(Directory.Exists(destination));
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.SaveSourceProjectDraftAsync("unknown"))).Code);
        Assert.DoesNotContain("OwnerSourceBuildsV1",(await remote.InfoAsync()).Capabilities);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.InspectSourceProjectBuildAsync(new("unknown","missing.dll","old")))).Code);
        Assert.DoesNotContain("OwnerSourceBuildImportsV1",(await remote.InfoAsync()).Capabilities);
        Assert.DoesNotContain("OwnerSourceBuildPublicationV1",(await remote.InfoAsync()).Capabilities);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.ImportSourceProjectBuildAsync(new(new("unknown","missing.dll","old"),"old")))).Code);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.GetImportedSourceBuildAsync(new("unknown",1)))).Code);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.FindImportedSourceBuildAsync(new("unknown",1)))).Code);
    });
    private static SourceSnapshot Replace(SourceSnapshot source,string before,string after)=>source with{Files=source.Files.Select(file=>file with{Text=file.Text.Replace(before,after)}).ToArray()};
    private static string Root(){var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("Test root is required"),"projects-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
}
