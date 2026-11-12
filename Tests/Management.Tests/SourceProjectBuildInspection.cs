using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed partial class SourceProjectBuildInspection{
    [Theory][InlineData("8.0.0.0","10.0.0.0",true)][InlineData("10.0.0.0","10.0.0.0",true)][InlineData("11.0.0.0","10.0.0.0",false)]
    public void ReferenceBindingUsesRuntimeVersionRules(string requested,string loaded,bool expected){
        var reference=new AssemblyName("System.Runtime, Version="+requested+", Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        var runtime=new AssemblyName("System.Runtime, Version="+loaded+", Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        Assert.Equal(expected,SourceBuildArtifactInspector.CanBindReference(reference,runtime));
        runtime.SetPublicKeyToken(new byte[8]);
        Assert.False(SourceBuildArtifactInspector.CanBindReference(reference,runtime));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SdkBuildIsInspectedWithoutExecutionThenLoadedFromCapturedBytes(bool pipe){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"source-build-"+Guid.NewGuid().ToString("N"));
        if(pipe){
            using var store=ManagementStore.Initialize(Path.Combine(root,"store"),"admin","TestPassword-123");
            using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();
            using var client=ManagementClient.ForLocalPipe(server.PipeName!);
            await Verify(client,client,root,async id=>{_=await client.CallAsync<System.Text.Json.JsonElement>("seedBatch","",new SeedBatchRequest(id,[]));});
        }else{
            using var host=InProcessManagementHost.Create(Path.Combine(root,"store"));
            await Verify(host,host,root,id=>{host.Invoke((service,user)=>{service.SeedBatch(user,new(id,[]));return true;});return Task.CompletedTask;});
        }
    }
    private static async Task Verify(IManagementOperations operations,IManagementSourceOperations sources,string root,Func<string,Task> reopenValidation){
        Assert.Contains("OwnerSourceBuildsV1",(await operations.InfoAsync()).Capabilities);
        Assert.Contains("OwnerSourceBuildPublicationV1",(await operations.InfoAsync()).Capabilities);
        var definition=Seamless.Management.DatabaseDesigner.Empty("built","Built");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Rows","",[new("Id","int",true),new("Value","int",false)],[]));
        var draft=await operations.SaveDraftAsync(new(definition,0,0));
        var preview=await operations.PreviewAsync(new(definition.Id,draft.Revision));
        Assert.True(preview.CanApply);
        Assert.Equal(OperationState.Succeeded,(await operations.ApplyAsync(new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()))).State);
        var published=await operations.GetDefinitionAsync(definition.Id);
        await operations.CommitRowsAsync(new(definition.Database,published.Version,Guid.NewGuid(),[
            new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=7}))]));
        var document=await sources.CreateSourceProjectAsync(new(Path.Combine(root,"project"),"Built",new(published.Definition,published.Version,0)));
        var sentinel=Path.Combine(root,"assembly-executed.txt");
        var code="\npublic static class InspectionSentinel{[System.Runtime.CompilerServices.ModuleInitializer]public static void Initialize()=>System.IO.File.WriteAllText("+
            System.Text.Json.JsonSerializer.Serialize(sentinel)+",ExternalBuildHelper.Marker.Value);}\n";
        var edited=document.Source! with{Files=document.Source!.Files.Select(file=>{if(file.Path=="Database.cs"){return file with{Text=file.Text+code};}return file;}).ToArray()};
        document=await sources.SynchronizeSourceProjectAsync(new(document.Id,document.Source,edited));
        draft=await sources.SaveSourceProjectDraftAsync(document.Id);
        var references=Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Select(path=>
            new XElement("Reference",new XAttribute("Include",Path.GetFileNameWithoutExtension(path)),new XElement("HintPath",path),new XElement("Private","true")));
        var helperDirectory=Path.Combine(root,"helper");Directory.CreateDirectory(helperDirectory);
        var helperProject=Path.Combine(helperDirectory,"ExternalBuildHelper.csproj");
        await File.WriteAllTextAsync(helperProject,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(helperDirectory,"Marker.cs"),"namespace ExternalBuildHelper;public static class Marker{public static string Value=>\"private dependency executed\";}");
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),
                new XElement("DebugType","portable"),new XElement("EnableNETAnalyzers","false"),new XElement("DefineConstants",string.Join(";",document.Source!.Defines??[]))),
            new XElement("ItemGroup",references,new XElement("ProjectReference",new XAttribute("Include",helperProject)),
                new XElement("Analyzer",new XAttribute("Include",Path.Combine(AppContext.BaseDirectory,"Seamless.Database.Generator.dll")))));
        await File.WriteAllTextAsync(document.ProjectPath,project.ToString());
        var artifacts=Path.Combine(root,"build");
        await Build(document.ProjectPath,artifacts);
        var image=Path.Combine(artifacts,"bin","Built","debug","Built.dll");
        var request=new SourceProjectBuildRequest(document.Id,image,draft.Definition.Source.Hash());
        var result=await sources.InspectSourceProjectBuildAsync(request);
        Assert.Equal(definition.Id,result.DefinitionId);
        Assert.Equal(draft.Revision,result.DraftRevision);
        Assert.Equal(draft.Definition.Hash(),result.DefinitionHash);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(image))),result.AssemblySha256);
        Assert.NotEqual(Guid.Empty,result.ModuleVersionId);
        Assert.Contains(result.Dependencies,item=>item.Name=="Seamless.Database"&&item.Shared);
        Assert.Contains(result.Dependencies,item=>item.Name=="ExternalBuildHelper"&&!item.Shared);
        Assert.False(File.Exists(sentinel));
        Assert.Equal(64,result.BundleHash.Length);
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.ImportSourceProjectBuildAsync(new(request,new string('0',64))))).Code);
        var imported=await sources.ImportSourceProjectBuildAsync(new(request,result.BundleHash));
        Assert.Equal(result.AssemblySha256,imported.Build.AssemblySha256);
        Assert.Equal(result.BundleHash,imported.Build.BundleHash);
        Assert.Equal(imported.ImportedAt,(await sources.ImportSourceProjectBuildAsync(new(request,result.BundleHash))).ImportedAt);
        Assert.Equal(result.BundleHash,(await sources.GetImportedSourceBuildAsync(new(definition.Id,draft.Revision)))?.Build.BundleHash);
        var originalImage=await File.ReadAllBytesAsync(image);
        try{
            await File.WriteAllBytesAsync(image,[..originalImage,0]);
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.ImportSourceProjectBuildAsync(new(request,result.BundleHash)))).Code);
            Assert.Equal(result.BundleHash,(await sources.GetImportedSourceBuildAsync(new(definition.Id,draft.Revision))).Build.BundleHash);
        }finally{await File.WriteAllBytesAsync(image,originalImage);}
        var budget=(Seamless.Execution.ResourceBudget)typeof(SourceBuildArtifactInspector).GetField("SharedBudget",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        Assert.Equal(0,budget.MemoryBytes);
        using(budget.ReserveMemory(budget.MemoryLimitBytes)){
            Assert.Equal("Limit",(await Assert.ThrowsAsync<ManagementException>(()=>sources.InspectSourceProjectBuildAsync(request))).Code);
        }
        Assert.Equal(0,budget.MemoryBytes);

        // 古い表示、別PDB、別ビルドの共有DLLを成功へ含めない。
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.InspectSourceProjectBuildAsync(request with{ExpectedSourceHash="old"}))).Code);
        var pdb=Path.ChangeExtension(image,".pdb");var originalPdb=await File.ReadAllBytesAsync(pdb);
        try{
            await File.WriteAllBytesAsync(pdb,[0,1,2]);
            Assert.Equal("BuildMismatch",(await Assert.ThrowsAsync<ManagementException>(()=>sources.InspectSourceProjectBuildAsync(request))).Code);
        }finally{await File.WriteAllBytesAsync(pdb,originalPdb);}
        var dependency=Path.Combine(Path.GetDirectoryName(image)!,"Seamless.Database.dll");var originalDependency=await File.ReadAllBytesAsync(dependency);
        try{
            await File.WriteAllBytesAsync(dependency,[..originalDependency,1]);
            Assert.Equal("BuildMismatch",(await Assert.ThrowsAsync<ManagementException>(()=>sources.InspectSourceProjectBuildAsync(request))).Code);
        }finally{await File.WriteAllBytesAsync(dependency,originalDependency);}
        Assert.Equal(0,budget.MemoryBytes);
        var sourcePath=Path.Combine(Path.GetDirectoryName(document.ProjectPath)!,document.Source!.Files[0].Path);
        var originalSource=await File.ReadAllBytesAsync(sourcePath);
        try{
            await File.AppendAllTextAsync(sourcePath,"\n// changed after build\n");
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.InspectSourceProjectBuildAsync(request))).Code);
        }finally{await File.WriteAllBytesAsync(sourcePath,originalSource);}
        Assert.False(File.Exists(sentinel));
        Assert.Equal(result.AssemblySha256,(await sources.InspectSourceProjectBuildAsync(request)).AssemblySha256);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>sources.InspectSourceProjectBuildAsync(request,cancellation.Token));
        Assert.Equal(0,budget.MemoryBytes);
        // 検査時の出力がなくても、選択した保管済みバイト列で計画取得と検証DBの再オープンを行う。
        var selected=System.Text.Json.JsonSerializer.Deserialize<DraftKey>(System.Text.Json.JsonSerializer.Serialize(new{Id=definition.Id,Revision=draft.Revision,BundleHash=result.BundleHash},ManagementJson.Options),ManagementJson.Options)!;
        var outputDirectory=Path.GetDirectoryName(image)!;var movedDirectory=outputDirectory+"-unavailable";
        Directory.Move(outputDirectory,movedDirectory);
        try{
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>operations.BuildAsync(selected with{BundleHash=new string('0',64)}))).Code);
            using(budget.ReserveMemory(budget.MemoryLimitBytes)){
                Assert.Equal("Limit",(await Assert.ThrowsAsync<ManagementException>(()=>operations.BuildAsync(selected))).Code);
                Assert.Equal("Limit",(await Assert.ThrowsAsync<ManagementException>(()=>operations.CreateValidationDraftAsync(selected))).Code);
            }
            var build=await operations.BuildAsync(selected);
            Assert.DoesNotContain(build.Diagnostics,item=>item.Severity=="Error");
            Assert.Equal(result.BundleHash,build.ArtifactHash);
            Assert.False(File.Exists(sentinel));
            Assert.Equal(0,budget.MemoryBytes);
            var validation=await operations.CreateValidationDraftAsync(selected);
            Assert.Equal("private dependency executed",await File.ReadAllTextAsync(sentinel));
            Assert.True(validation.FromDraft);
            Assert.Equal(draft.Definition.Source.Hash(),validation.SourceHash);
            Assert.Equal(result.BundleHash,validation.BundleHash);
            Assert.Equal(0,budget.MemoryBytes);
            File.Delete(sentinel);
            await reopenValidation(validation.Id);
            Assert.Equal("private dependency executed",await File.ReadAllTextAsync(sentinel));
            Assert.Equal(0,budget.MemoryBytes);
            await operations.DropValidationAsync(validation.Id);
            var selectedPreview=await operations.PreviewAsync(selected);
            Assert.True(selectedPreview.CanApply,string.Join("\n",selectedPreview.Diagnostics.Select(item=>item.Message)));
            Assert.Equal(result.BundleHash,selectedPreview.BundleHash);
            Assert.Equal(result.BundleHash,selectedPreview.Validation?.BundleHash);
            var package=Path.Combine(root,"store","source-builds",result.BundleHash);
            foreach(var file in new[]{"module.dll","module.pdb","dependency-000.dll","manifest.json"}){
                var path=Path.Combine(package,file);var original=await File.ReadAllBytesAsync(path);
                try{
                    await File.WriteAllBytesAsync(path,[..original,1]);
                    Assert.Equal("BuildMismatch",(await Assert.ThrowsAsync<ManagementException>(()=>operations.BuildAsync(selected))).Code);
                    Assert.Equal(0,budget.MemoryBytes);
                }finally{await File.WriteAllBytesAsync(path,original);}
            }
            File.Delete(sentinel);
            var storedBuild=new SourceBuildArtifactStore(Path.Combine(root,"store"),new object());
            Assert.ThrowsAny<OperationCanceledException>(()=>storedBuild.Read(result,cancellation.Token));
            Assert.Equal(0,budget.MemoryBytes);
            using(var captured=storedBuild.Read(result)){
                var rootImage=Path.Combine(package,"module.dll");var original=await File.ReadAllBytesAsync(rootImage);
                try{
                    await File.WriteAllBytesAsync(rootImage,[0,1,2]);
                    using var compiled=NativeRuntime.FromImported(captured);
                    Assert.Equal(result.BundleHash,compiled.Result.ArtifactHash);
                    compiled.InvokeStatic("UserCode.InspectionSentinel","Initialize");
                    Assert.Equal("private dependency executed",await File.ReadAllTextAsync(sentinel));
                }finally{await File.WriteAllBytesAsync(rootImage,original);}
            }
            Assert.Equal(0,budget.MemoryBytes);
            using(var compiled=NativeRuntime.FromImported(storedBuild.Read(result))){
                using var failed=new DatabaseInstance(draft.Definition with{EntryType="Missing.Database"},Path.Combine(root,"never-opened"),compiled);
                Assert.Throws<TypeLoadException>(()=>failed.Open());
            }
            Assert.Equal(0,budget.MemoryBytes);
            var apply=System.Text.Json.JsonSerializer.Deserialize<ApplyRequest>(System.Text.Json.JsonSerializer.Serialize(new{Id=definition.Id,DraftRevision=draft.Revision,PreviewToken=selectedPreview.Token,OperationId=Guid.NewGuid(),BundleHash=result.BundleHash},ManagementJson.Options),ManagementJson.Options)!;
            var operation=await operations.ApplyAsync(apply);
            Assert.True(operation.State==OperationState.Succeeded,operation.Error);
            Assert.Equal(operation.Id,(await operations.ApplyAsync(apply)).Id);
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>operations.ApplyAsync(apply with{BundleHash="different"}))).Code);
            var active=await operations.GetDefinitionAsync(definition.Id);
            Assert.Equal(result.BundleHash,active.ArtifactHash);
            Assert.Equal(2,active.Version);
            var row=Assert.Single((await operations.ReadRowsAsync(new(definition.Database,"Rows"))).Rows);
            Assert.Equal(7,row.Values.GetProperty("value").GetInt32());
            await operations.CommitRowsAsync(new(definition.Database,active.Version,Guid.NewGuid(),[
                new("Rows",TableRowChangeKind.Replace,row.Key,row.Stamp,ManagementJson.Element(new{Id=1,Value=8})),
                new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=2,Value=42}))]));
            var updated=await operations.ReadRowsAsync(new(definition.Database,"Rows"));
            Assert.Equal(2,updated.Rows.Length);
            Assert.Contains(updated.Rows,item=>item.Values.GetProperty("id").GetInt32()==1&&item.Values.GetProperty("value").GetInt32()==8);
            Assert.Contains(updated.Rows,item=>item.Values.GetProperty("id").GetInt32()==2&&item.Values.GetProperty("value").GetInt32()==42);
        }finally{Directory.Move(movedDirectory,outputDirectory);}
        var competing=draft.Definition with{Source=draft.Definition.Source with{Files=draft.Definition.Source.Files.Select(file=>file with{Text=file.Text+"\n// server draft edit\n"}).ToArray()}};
        var latest=await operations.GetDraftAsync(definition.Id);
        var activeVersion=await operations.GetDefinitionAsync(definition.Id);
        await operations.SaveDraftAsync(new(competing,latest.Revision,activeVersion.Version,activeVersion.Version));
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.InspectSourceProjectBuildAsync(request))).Code);
    }
    private static async Task Build(string project,string artifacts,string configuration="Debug"){
        var executable=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        using var process=new Process{StartInfo=new(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        foreach(var argument in new[]{"build",project,"-c",configuration,"--artifacts-path",artifacts,"-m:1","-p:UseSharedCompilation=false","-p:NuGetAudit=false","-nodeReuse:false"}){process.StartInfo.ArgumentList.Add(argument);}
        Assert.True(process.Start());
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var output=process.StandardOutput.ReadToEndAsync(timeout.Token);var error=process.StandardError.ReadToEndAsync(timeout.Token);
        try{await process.WaitForExitAsync(timeout.Token);Assert.True(process.ExitCode==0,await output+await error);}
        finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
    }
}
