using System.Security.Cryptography;
using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class SourceBuildArtifactStorage{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"build-storage-"+Guid.NewGuid().ToString("N"));
    private static (SourceProjectBuild Build,Dictionary<string,byte[]> Buffers) Fixture(string directory,Definition? definition=null){
        var assembly=Path.Combine(directory,"input","db.dll");var pdb=Path.ChangeExtension(assembly,".pdb");var dependency=Path.Combine(directory,"input","helper.dll");
        byte[] image=[1,2,3],symbols=[4,5,6],helper=[7,8,9];
        var build=new SourceProjectBuild("project","stored",1,0,"definition","source",assembly,Hash(image),Hash(symbols),Guid.NewGuid(),[new("helper","1.0.0.0",dependency,Hash(helper),false)]);
        if(definition is not null){build=build with{DefinitionId=definition.Id,DefinitionHash=definition.Hash(),SourceHash=definition.Source.Hash()};}
        build=build with{BundleHash=SourceBuildArtifactStore.BundleHash(build)};
        return(build,new(StringComparer.OrdinalIgnoreCase){[assembly]=image,[pdb]=symbols,[dependency]=helper});
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));

    [Fact]public void RuntimeDependenciesUseTheSelectedAssetAndRejectAMissingSelection(){
        var root=Root();Directory.CreateDirectory(root);
        var framework=Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        byte[] Emit(string name,string source,params Microsoft.CodeAnalysis.MetadataReference[] references){
            var compilation=Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(name,
                [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source)],references,
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            using var output=new MemoryStream();var emitted=compilation.Emit(output);
            Assert.True(emitted.Success,string.Join("\n",emitted.Diagnostics));return output.ToArray();
        }
        var dependency=Emit("BoundDependency","public sealed class Marker{}",framework);
        var image=Emit("Component","public static class Component{public static Marker Create()=>new();}",framework,Microsoft.CodeAnalysis.MetadataReference.CreateFromImage(dependency));
        var assembly=Path.Combine(root,"Component.dll");File.WriteAllBytes(assembly,image);
        var selected="runtimes/"+System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier+"/lib/net10.0/BoundDependency.dll";
        var selectedPath=Path.GetFullPath(Path.Combine(root,selected));Directory.CreateDirectory(Path.GetDirectoryName(selectedPath)!);File.WriteAllBytes(selectedPath,dependency);
        var rootPath=Path.Combine(root,"BoundDependency.dll");File.WriteAllBytes(rootPath,[0]);
        const string target=".NETCoreApp,Version=v10.0";
        var manifest=new{
            runtimeTarget=new{name=target,signature=""},
            targets=new Dictionary<string,object>{[target]=new Dictionary<string,object>{
                ["Component/1.0.0"]=new{dependencies=new{BoundDependency="1.0.0"},runtime=new Dictionary<string,object>{["Component.dll"]=new{}}},
                ["BoundDependency/1.0.0"]=new{runtimeTargets=new Dictionary<string,object>{[selected]=new{rid=System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,assetType="runtime"}}}
            }},
            libraries=new Dictionary<string,object>{
                ["Component/1.0.0"]=new{type="project",serviceable=false,sha512=""},
                ["BoundDependency/1.0.0"]=new{type="package",serviceable=false,sha512=""}
            }
        };
        File.WriteAllText(Path.ChangeExtension(assembly,".deps.json"),System.Text.Json.JsonSerializer.Serialize(manifest));
        var resolver=new System.Runtime.Loader.AssemblyDependencyResolver(assembly);
        Assert.Equal(selectedPath,resolver.ResolveAssemblyToPath(new System.Reflection.AssemblyName("BoundDependency")));
        using(var capture=SourceBuildArtifactInspector.CaptureFiles(CancellationToken.None)){
            var actual=Assert.Single(SourceBuildArtifactInspector.CheckDependencies(assembly,image,capture));
            Assert.Equal(selectedPath,actual.Path);Assert.Equal(Hash(dependency),actual.Sha256);Assert.False(actual.Shared);
        }
        File.WriteAllBytes(rootPath,dependency);File.Delete(selectedPath);
        using var missing=SourceBuildArtifactInspector.CaptureFiles(CancellationToken.None);
        Assert.Equal("BuildMismatch",Assert.Throws<ManagementException>(()=>SourceBuildArtifactInspector.CheckDependencies(assembly,image,missing)).Code);
    }
    [Fact]public void TypedManagementRowsKeepTheirPersistedAssemblyIdentity(){
        var identity=typeof(global::Seamless.Execution.Store).Assembly.GetType("Seamless.Execution.PersistedTypeIdentity",true)!;
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var name=identity.GetMethod("Name",flags)!;
        var resolve=identity.GetMethod("Resolve",flags)!;
        foreach(var row in new[]{typeof(ManagementUserRow),typeof(ManagementSettingsRow),typeof(ManagementTextValue),typeof(ManagementKey)}){
            var expected=row.FullName+", Seamless.Management.Server, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
            Assert.Equal(expected,name.Invoke(null,[row]));
            Assert.Same(row,resolve.Invoke(null,[expected]));
        }
    }
    [Fact]public void SharedDependenciesAreBoundToTheHostWithoutCopiedPayloads(){
        var root=Root();var fixture=Fixture(root);
        var runtime=Seamless.Execution.CompilationReferences.PathFor(typeof(Seamless.Execution.Store).Assembly);
        var bytes=File.ReadAllBytes(runtime);
        var dependency=new SourceBuildDependency("Runtime",typeof(Seamless.Execution.Store).Assembly.GetName().Version!.ToString(),runtime,Hash(bytes),true);
        var build=fixture.Build with{Dependencies=[..fixture.Build.Dependencies,dependency],BundleHash=""};
        build=build with{BundleHash=SourceBuildArtifactStore.BundleHash(build)};
        fixture.Buffers[runtime]=bytes;
        var store=new SourceBuildArtifactStore(root,new object());
        store.Store(build,fixture.Buffers,CancellationToken.None);
        store.Verify(build);
        var package=Path.Combine(root,"source-builds",build.BundleHash);
        Assert.Equal(4,Directory.EnumerateFiles(package).Count());
        Assert.True(Directory.EnumerateFiles(package).Sum(path=>new FileInfo(path).Length)<65536);
        var changed=build with{Dependencies=[..fixture.Build.Dependencies,dependency with{Sha256=new string('0',64)}],BundleHash=""};
        changed=changed with{BundleHash=SourceBuildArtifactStore.BundleHash(changed)};
        store.Store(changed,fixture.Buffers,CancellationToken.None);
        Assert.Equal("BuildMismatch",Assert.Throws<ManagementException>(()=>store.Verify(changed)).Code);
    }

    [Fact]public void LegacyManifestRetainsItsHashAndCopiedDependencyContract(){
        var root=Root();var fixture=Fixture(root);
        var dependency=Assert.Single(fixture.Build.Dependencies);
        var files=new[]{new{File="module.dll",Sha256=fixture.Build.AssemblySha256,Name="",Version="",Shared=false},
            new{File="module.pdb",Sha256=fixture.Build.PdbSha256,Name="",Version="",Shared=false},
            new{File="dependency-000.dll",dependency.Sha256,dependency.Name,dependency.Version,dependency.Shared}};
        var legacy=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{Format=1,Files=files},ManagementJson.Options);
        var build=fixture.Build with{BundleHash=Hash(legacy)};
        Assert.Equal(build.BundleHash,SourceBuildArtifactStore.BundleHash(build));
        var store=new SourceBuildArtifactStore(root,new object());
        store.Store(build,fixture.Buffers,CancellationToken.None);
        store.Verify(build);
        var package=Path.Combine(root,"source-builds",build.BundleHash);
        Assert.Equal(legacy,File.ReadAllBytes(Path.Combine(package,"manifest.json")));
        Assert.True(File.Exists(Path.Combine(package,"dependency-000.dll")));
    }
    [Fact]public void StoresTheCapturedBytesAndRejectsCorruptedCopies(){
        var root=Root();var fixture=Fixture(root);var store=new SourceBuildArtifactStore(root,new object());
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.Build.AssemblyPath)!);
        foreach(var input in fixture.Buffers){File.WriteAllBytes(input.Key,[99]);}
        store.Store(fixture.Build,fixture.Buffers,CancellationToken.None);
        foreach(var path in fixture.Buffers.Keys){File.Delete(path);}
        new SourceBuildArtifactStore(root,new object()).Verify(fixture.Build);
        var package=Path.Combine(root,"source-builds",fixture.Build.BundleHash);
        Assert.Equal(fixture.Buffers[fixture.Build.AssemblyPath],File.ReadAllBytes(Path.Combine(package,"module.dll")));
        store.Store(fixture.Build,fixture.Buffers,CancellationToken.None);
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(root,"source-builds")));
        File.WriteAllBytes(Path.Combine(package,"module.dll"),[99]);
        Assert.Equal("BuildMismatch",Assert.Throws<ManagementException>(()=>store.Verify(fixture.Build)).Code);
        Assert.Equal("BuildMismatch",Assert.Throws<ManagementException>(()=>store.Store(fixture.Build,fixture.Buffers,CancellationToken.None)).Code);
    }
    [Fact]public void CancellationAndStorageBoundsLeaveNoPreparedDirectory(){
        var root=Root();var fixture=Fixture(root);var gate=new object();
        var limited=new SourceBuildArtifactStore(root,gate,1);
        Assert.Throws<ResourceLimitException>(()=>limited.Store(fixture.Build,fixture.Buffers,CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(root,"source-builds")));
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>new SourceBuildArtifactStore(root,gate).Store(fixture.Build,fixture.Buffers,canceled.Token));
        var bounded=new SourceBuildArtifactStore(root,gate,1L<<20,1);
        bounded.Store(fixture.Build,fixture.Buffers,CancellationToken.None);
        var other=fixture.Build with{PdbSha256=Hash([10,11,12])};
        other=other with{BundleHash=SourceBuildArtifactStore.BundleHash(other)};
        Assert.Equal("Limit",Assert.Throws<ManagementException>(()=>bounded.Store(other,fixture.Buffers,CancellationToken.None)).Code);
        bounded.Verify(fixture.Build);
        Assert.DoesNotContain(Directory.EnumerateDirectories(Path.Combine(root,"source-builds")),path=>Path.GetFileName(path).StartsWith(".incoming-",StringComparison.Ordinal));
    }
    [Fact]public void AChangedBufferAfterTheFirstFileDoesNotExposeAPartialPackage(){
        var root=Root();var fixture=Fixture(root);var store=new SourceBuildArtifactStore(root,new object());
        fixture.Buffers[Path.ChangeExtension(fixture.Build.AssemblyPath,".pdb")]=[99];
        Assert.Equal("BuildMismatch",Assert.Throws<ManagementException>(()=>store.Store(fixture.Build,fixture.Buffers,CancellationToken.None)).Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root,"source-builds")));
    }
    [Fact]public void BindingSurvivesReopenAndRemainsPrivateToTheSameDraft(){
        var root=Root();var definition=Seamless.Management.DatabaseDesigner.Empty("stored","Stored");
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);var fixture=Fixture(root,definition);
        DateTimeOffset importedAt;
        using(var storage=ManagementStore.Initialize(root,"admin","TestPassword-123")){
            using var service=new Seamless.Management.Server.ManagementService(storage,null,startAuditWorker:false);
            var draft=service.SaveDraft(owner,new(definition,0,0));
            service.SourceBuilds.Store(fixture.Build,fixture.Buffers,CancellationToken.None);
            importedAt=service.BindImportedSourceBuild(owner,fixture.Build).ImportedAt;
            Assert.Equal(importedAt,service.BindImportedSourceBuild(owner,fixture.Build).ImportedAt);
            var other=owner with{Name="other"};service.SaveDraft(other,new(definition,0,0));
            Assert.Equal("NotFound",Assert.Throws<ManagementException>(()=>service.GetImportedSourceBuild(other,new(definition.Id,draft.Revision))).Code);
        }
        using var reopened=new ManagementStore(root);
        using var current=new Seamless.Management.Server.ManagementService(reopened,null,startAuditWorker:false);
        var saved=current.GetImportedSourceBuild(owner,new(definition.Id,1));
        Assert.Equal(importedAt,saved.ImportedAt);current.SourceBuilds.Verify(saved.Build);
        var change=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text+"\n// changed\n"}).ToArray()}};
        var edited=current.SaveDraft(owner,new(change,1,0));
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>current.BindImportedSourceBuild(owner,fixture.Build)).Code);
        Assert.Equal("NotFound",Assert.Throws<ManagementException>(()=>current.GetImportedSourceBuild(owner,new(definition.Id,edited.Revision))).Code);
    }
}
