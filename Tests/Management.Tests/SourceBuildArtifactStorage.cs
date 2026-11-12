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
