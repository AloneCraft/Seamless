using System.Text.Json.Nodes;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Tests.PrebuiltPublication;

namespace Seamless.Database.Gateway.Tests.Exports;

public sealed class NativePrebuiltPackage{
    [Fact]
    public void Publish_GeneratesPortableMetadataAndCanPublishAgain(){
        var root=Root();var output=Path.Combine(root,"package");
        var database=NativeDatabaseAssembly.From<RuntimeSqlGeneratedRoot>("fixture");
        INativeGeneratedQueryModule module=new PrebuiltQueriesModule();
        var path=global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Publish(output,module,[database]);
        var first=global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Read(path);
        Assert.True(first.IsPrebuilt);Assert.Equal(module.ModuleId,first.Manifest.ModuleId);
        Assert.Equal("Echo",Assert.Single(first.Manifest.Queries).Id);Assert.False(Assert.Single(first.Manifest.Queries).IsReadOnly);
        Assert.Equal("fingerprint",Assert.Single(first.Dependencies).SchemaFingerprint);
        Assert.Equal(first.Sha256,first.Manifest.AssemblySha256);
        Assert.True(File.Exists(first.AssemblyPath));Assert.True(File.Exists(first.SourceProjectPath));
        Assert.Equal(path,global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Publish(output,module,[database]));
        Assert.NotEqual(first.Id,global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Read(path).Id);
        var moved=Path.Combine(root,"moved");Directory.Move(output,moved);
        var relocated=global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Read(Path.Combine(moved,Path.GetFileName(path)));
        Assert.StartsWith(moved,relocated.AssemblyPath,StringComparison.OrdinalIgnoreCase);Assert.True(File.Exists(relocated.AssemblyPath));
    }

    [Fact]
    public void Publish_PreservesThePreviousEntryWhenAnInputIsMissing(){
        var output=Path.Combine(Root(),"package");var database=NativeDatabaseAssembly.From<RuntimeSqlGeneratedRoot>("fixture");var module=new PrebuiltQueriesModule();
        var path=global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Publish(output,module,[database]);var original=File.ReadAllText(path);
        Assert.Throws<FileNotFoundException>(()=>global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Publish(output,module,[database with{AssemblyPath=Path.Combine(output,"missing.dll")}]));
        Assert.Equal(original,File.ReadAllText(path));Assert.True(File.Exists(global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Read(path).AssemblyPath));
    }
    [Fact]
    public void Read_RejectsEscapingPathsAndChangedImages(){
        var output=Path.Combine(Root(),"package");
        var path=global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Publish(output,new PrebuiltQueriesModule(),[NativeDatabaseAssembly.From<RuntimeSqlGeneratedRoot>("fixture")]);
        var original=File.ReadAllText(path);var node=JsonNode.Parse(original)!.AsObject();node["AssemblyPath"]="../outside.dll";File.WriteAllText(path,node.ToJsonString());
        Assert.Throws<InvalidDataException>(()=>global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Read(path));
        File.WriteAllText(path,original);var artifact=global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Read(path);
        using(var stream=new FileStream(artifact.AssemblyPath,FileMode.Append,FileAccess.Write)){stream.WriteByte(0);}
        Assert.Throws<InvalidDataException>(()=>global::Seamless.Database.Gateway.Hosting.NativePrebuiltPackage.Read(path));
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing test root."),"export-"+Guid.NewGuid().ToString("N"));
}
