using System.Reflection;
using System.Reflection.PortableExecutable;
using Seamless.Execution;
using Xunit;

namespace Seamless.Core.Tests;

public sealed class CompilationReferenceTests{
    [Fact]
    public void ReferencePathsHaveUniqueManagedAssemblyFileNames(){
        var paths=CompilationReferences.Paths.ToArray();
        Assert.NotEmpty(paths);
        Assert.Equal(paths.Length,paths.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach(var path in paths){
            using var stream=File.OpenRead(path);
            using var image=new PEReader(stream);
            Assert.True(image.HasMetadata,path);
        }
    }

    [Fact]
    public void RuntimePackOwnershipComesFromTheActiveHostDependencyManifest(){
        var root=Path.Combine(Path.GetTempPath(),"framework-ownership-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try{
            var path=Path.Combine(root,"application.deps.json");
            File.WriteAllText(path,"""
                {"runtimeTarget":{"name":"net10/win-x64"},"targets":{"net10/win-x64":{
                "Framework/10":{"runtime":{"System.Security.Cryptography.Pkcs.dll":{}}},
                "Package/1":{"runtime":{"PackageOnly.dll":{}}}},
                "other":{"Framework/10":{"runtime":{"OtherTarget.dll":{}}}}},
                "libraries":{"Framework/10":{"type":"runtimepack"},"Package/1":{"type":"package"}}}
                """);
            var reader=typeof(CompilationReferences).GetMethod("ReadFrameworkRuntimeAssemblies",BindingFlags.Static|BindingFlags.NonPublic)!;
            var names=Assert.IsAssignableFrom<IReadOnlySet<string>>(reader.Invoke(null,[path]));
            Assert.Equal(["System.Security.Cryptography.Pkcs.dll"],names.Order(StringComparer.Ordinal));
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlySet<string>>(reader.Invoke(null,[Path.Combine(root,"missing.deps.json")])));
        }finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void FrameworkAndProductReferencesKeepTheirOwnership(){
        Assert.True(CompilationReferences.IsFramework(typeof(object).Assembly));
        Assert.True(CompilationReferences.IsFramework(typeof(HttpClient).Assembly));
        Assert.False(CompilationReferences.IsFramework(typeof(global::Seamless.Execution.Store).Assembly));
        Assert.DoesNotContain(typeof(global::Seamless.Execution.Store).Assembly.GetExportedTypes(),type=>type.Namespace=="Seamless.FLinq");
        Assert.Equal(typeof(global::Seamless.Execution.Store).Assembly.Location,CompilationReferences.PathFor(typeof(global::Seamless.Execution.Store).Assembly));
    }
}