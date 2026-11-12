using Seamless.Management;

namespace Management.Tests;

public sealed partial class SourceProjectConfigurations{
    [Theory][InlineData("Debug","DebugModel","Debug.cs","DEBUG")][InlineData("Release","ReleaseModel","Release.cs","RELEASE")][InlineData("QA Test","QaModel","Qa.cs","QA_TEST")]
    public async Task SdkConfigurationControlsDefinesCompileFilesAndNamespaces(string configuration,string ns,string file,string symbol){
        var project=CreateProject();using var service=new SourceProjectService();
        var document=await service.OpenSourceProjectAsync(new(project){Configuration=configuration});
        Assert.Equal(configuration,document.Configuration);Assert.Contains(configuration,document.Configurations);Assert.Contains("QA Test",document.Configurations);
        Assert.Equal(new[]{ns},document.Namespaces);Assert.Contains(symbol,document.Source!.Defines!);
        Assert.Equal(new[]{"Common.cs",file}.Order(StringComparer.Ordinal),document.Source.Files.Select(source=>source.Path));
        var result=await service.SynchronizeSourceProjectAsync(new(document.Id,document.Source,document.Source));Assert.Equal(configuration,result.Configuration);
    }
    [Fact]public async Task SwitchingKeepsExcludedFileEditsAndCanRestoreTheOriginalConfiguration(){
        var project=CreateProject();using var service=new SourceProjectService();var first=await service.OpenSourceProjectAsync(new(project));
        var changed=first.Source! with{Files=first.Source!.Files.Select(file=>file with{Text=file.Text.Replace("Value=1","Value=2")}).ToArray()};
        await service.SynchronizeSourceProjectAsync(new(first.Id,first.Source!,changed));
        var release=await service.OpenSourceProjectAsync(new(project){Configuration="Release"});
        Assert.DoesNotContain(release.Source!.Files,file=>file.Path=="Debug.cs");Assert.Contains("Value=2",File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!,"Debug.cs")));
        var again=await service.OpenSourceProjectAsync(new(project));Assert.Contains("Value=2",again.Source!.Files.Single(file=>file.Path=="Debug.cs").Text);
    }
    [Theory][InlineData("")][InlineData("Release;Injected=true")][InlineData("$(Configuration)")][InlineData("../Release")][InlineData(null)]
    public async Task InvalidConfigurationIsRejectedBeforeEvaluation(string? configuration){
        var project=CreateProject();using var service=new SourceProjectService();
        Assert.Equal("InvalidRequest",(await Assert.ThrowsAsync<ManagementException>(()=>service.OpenSourceProjectAsync(new(project){Configuration=configuration!}))).Code);
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!,".seamless")));
    }
    [Fact]public async Task StaleConfigurationCannotRestoreOldConditionalDefines(){
        var project=CreateProject();using var service=new SourceProjectService();var debug=await service.OpenSourceProjectAsync(new(project));
        await service.OpenSourceProjectAsync(new(project){Configuration="Release"});
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.SynchronizeSourceProjectAsync(new(debug.Id,debug.Source!,debug.Source!)))).Code);
        var release=await service.OpenSourceProjectAsync(new(project){Configuration="Release"});Assert.Equal(new[]{"ReleaseModel"},release.Namespaces);
    }
    [Fact]public async Task OwnerPipeCarriesConfigurationThroughEvaluationAndSynchronization(){
        var project=CreateProject();using var store=Seamless.Management.Server.ManagementStore.Initialize(Path.Combine(Path.GetDirectoryName(project)!,"store"),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Contains("OwnerSourceConfigurationsV1",(await client.InfoAsync()).Capabilities);
        var opened=await client.OpenSourceProjectAsync(new(project){Configuration="Release"});Assert.Equal("Release",opened.Configuration);Assert.Equal(new[]{"ReleaseModel"},opened.Namespaces);
        var synced=await client.SynchronizeSourceProjectAsync(new(opened.Id,opened.Source!,opened.Source!));Assert.Equal("Release",synced.Configuration);
    }
    private static string CreateProject(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"configuration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var project=Path.Combine(root,"Config.csproj");
        File.WriteAllText(project,"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><Configurations>Debug;Release;QA Test</Configurations><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
              <ItemGroup><Compile Include="Common.cs"/><Compile Include="Debug.cs" Condition="'$(Configuration)'=='Debug'"/><Compile Include="Release.cs" Condition="'$(Configuration)'=='Release'"/><Compile Include="Qa.cs" Condition="'$(Configuration)'=='QA Test'"/></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(root,"Common.cs"),"#if DEBUG\nnamespace DebugModel { internal static partial class DatabaseGenerator; }\n#elif RELEASE\nnamespace ReleaseModel { internal static partial class DatabaseGenerator; }\n#elif QA_TEST\nnamespace QaModel { internal static partial class DatabaseGenerator; }\n#endif\n");
        foreach(var name in new[]{"Debug","Release","Qa"}){File.WriteAllText(Path.Combine(root,name+".cs"),"namespace Helpers;public class "+name+"Value{public int Value=1;}");}
        return project;
    }
}
