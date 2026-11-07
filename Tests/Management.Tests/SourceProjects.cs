using Seamless.Management;
using Seamless.Management.Server;
using System.Net;

namespace Management.Tests;

public sealed class SourceProjects{
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
        Assert.Contains("OwnerSourceProjectsV1",(await local.InfoAsync()).Capabilities);
        Assert.Equal([project],(await local.OpenSourceLocationAsync(project)).Projects);
        using var remote=new ManagementClient("localhost",server.Port,certificate);
        Assert.DoesNotContain("OwnerSourceProjectsV1",(await remote.InfoAsync()).Capabilities);
        await remote.LoginAsync("admin","TestPassword-123");
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>remote.OpenSourceLocationAsync(project))).Code);
        using var sessions=new LocalBrowserSessions(store.InstanceId);sessions.BindOrigin(new("http://127.0.0.1:25001/"));
        var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:25001");
        var browser=new BrowserManagementOperations(sessions,local,()=>Task.FromResult<string?>(session.Id));
        Assert.Equal([project],(await browser.OpenSourceLocationAsync(project)).Projects);
        sessions.RevokeAll();
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
    private static SourceSnapshot Replace(SourceSnapshot source,string before,string after)=>source with{Files=source.Files.Select(file=>file with{Text=file.Text.Replace(before,after)}).ToArray()};
    private static string Root(){var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("Test root is required"),"projects-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
}
