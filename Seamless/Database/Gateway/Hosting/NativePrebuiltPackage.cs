using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Database.Client;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Hosting;

/// <summary>共通ビルドのDLLを世代別に保存し、移設可能な公開メタデータを出力する。</summary>
public static class NativePrebuiltPackage{
    private const int MaximumMetadataBytes=16<<20;
    private static readonly ResourceBudget DefaultBudget=new(64L<<20,0);

    public static string Publish(string directory,INativeGeneratedQueryModule module,IReadOnlyList<NativeDatabaseAssembly> databases,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);ArgumentNullException.ThrowIfNull(module);ArgumentNullException.ThrowIfNull(databases);cancellationToken.ThrowIfCancellationRequested();
        if(databases.Count is <1 or >1024){throw new InvalidDataException("Invalid native package dependency count.");}
        var inputs=databases.ToArray();var primary=inputs.SingleOrDefault(item=>item.Database==module.DatabaseName)??throw new InvalidDataException("The native package has no primary database.");
        if(inputs.Select(static item=>item.Database).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=inputs.Length){throw new InvalidDataException("Duplicate native package databases.");}
        var declarations=module.Describe(primary.SchemaFingerprint);
        if(declarations.Count is <1 or >1024){throw new InvalidDataException("Invalid native package query count.");}
        var queries=declarations.ToArray();
        var characters=checked(inputs.Sum(static item=>(long)item.Database.Length+item.SchemaFingerprint.Length+item.AssemblyPath.Length)
            +queries.Sum(static item=>(long)item.Id.Length+item.Database.Length+item.SchemaFingerprint.Length+item.ArgumentsType.Length+item.ResultType.Length+(item.ArgumentsContract?.Length??0)+(item.ResultContract?.Length??0)));
        if(characters>2L<<20){throw new InvalidDataException("The native package metadata exceeds the supported size.");}
        using var memory=(budget??DefaultBudget).ReserveMemory(checked(262144+characters*8));
        if(queries.Select(static item=>item.Id).Distinct(StringComparer.Ordinal).Count()!=queries.Length||queries.Any(item=>item.Database!=primary.Database||item.SchemaFingerprint!=primary.SchemaFingerprint)){throw new InvalidDataException("The generated native definitions differ from the primary database.");}
        foreach(var query in queries){module.GetContract(query.Id).ValidateDefinition(query);}
        var root=Path.GetFullPath(directory);Directory.CreateDirectory(root);var id=Guid.NewGuid().ToString("N");var relative=Path.Combine("generations",id);var stage=Path.Combine(root,relative);Directory.CreateDirectory(stage);
        var moduleAssembly=module.GetType().Assembly;var moduleName=FileName(moduleAssembly.GetName().Name!);var image=Path.Combine(stage,moduleName);
        var hash=CopyImage(global::Seamless.Execution.CompilationReferences.PathFor(moduleAssembly),image,cancellationToken);
        using(var file=File.OpenRead(image)){
            using var pe=new PEReader(file);var metadata=pe.GetMetadataReader();
            if(metadata.GetGuid(metadata.GetModuleDefinition().Mvid)!=moduleAssembly.ManifestModule.ModuleVersionId){throw new InvalidDataException("The loaded native module differs from the build output.");}
        }
        var dependencies=new List<NativeAssemblyDependency>(inputs.Length);
        foreach(var input in inputs){
            if(!Path.IsPathFullyQualified(input.AssemblyPath)){throw new InvalidDataException("Native package database paths must be absolute.");}
            var name=AssemblyName.GetAssemblyName(input.AssemblyPath).Name!;var target=Path.Combine(stage,FileName(name));
            dependencies.Add(new(input.Database,input.SchemaFingerprint,name,CopyImage(input.AssemblyPath,target,cancellationToken)));
        }
        var source=new NativeQuerySourceProject(module.ModuleId,module.GetType().FullName!,[],queries,inputs.Select(static item=>item.Database).ToArray());
        var sourcePath=Path.Combine(stage,"native-source.json");WriteJson(sourcePath,source);
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var manifest=new NativeModuleManifest(module.ModuleId,Path.Combine(relative,moduleName),hash,source.EntryType,version,queries);
        var artifact=new NativeQueryBuildArtifact(id,manifest.AssemblyPath,hash,Path.Combine(relative,"native-source.json"),"",manifest,dependencies){SourceProjectSha256=Hash(sourcePath),IsPrebuilt=true};
        var temporary=Path.Combine(root,"native-build-"+id+".tmp");WriteJson(temporary,artifact);cancellationToken.ThrowIfCancellationRequested();
        var entry=Path.Combine(root,"native-build.json");File.Move(temporary,entry,true);return entry;
    }

    public static NativeQueryBuildArtifact Read(string path,ResourceBudget? budget=null,CancellationToken cancellationToken=default){
        ArgumentException.ThrowIfNullOrWhiteSpace(path);cancellationToken.ThrowIfCancellationRequested();var fullPath=Path.GetFullPath(path);var root=Path.GetDirectoryName(fullPath)!;
        using var file=File.OpenRead(fullPath);
        if(file.Length is <=0 or >MaximumMetadataBytes){throw new InvalidDataException("Invalid native package metadata size.");}
        using var memory=(budget??DefaultBudget).ReserveMemory(checked(file.Length*4+262144));
        var artifact=JsonSerializer.Deserialize<NativeQueryBuildArtifact>(file)??throw new InvalidDataException("Missing native package metadata.");
        if(!artifact.IsPrebuilt||!Guid.TryParseExact(artifact.Id,"N",out _)||artifact.Manifest is null||artifact.Dependencies is null||artifact.Dependencies.Count is <1 or >1024){throw new InvalidDataException("Invalid native package metadata.");}
        var image=Resolve(root,artifact.AssemblyPath);var source=Resolve(root,artifact.SourceProjectPath);
        if(artifact.Manifest.AssemblyPath!=artifact.AssemblyPath||artifact.Manifest.AssemblySha256!=artifact.Sha256||Hash(image)!=artifact.Sha256||Hash(source)!=artifact.SourceProjectSha256){throw new InvalidDataException("Native package payload hash mismatch.");}
        foreach(var dependency in artifact.Dependencies){
            cancellationToken.ThrowIfCancellationRequested();
            var dependencyPath=Path.Combine(Path.GetDirectoryName(image)!,FileName(dependency.AssemblyName));
            if(Hash(dependencyPath)!=dependency.Sha256){throw new InvalidDataException("Native package dependency hash mismatch.");}
        }
        return artifact with{AssemblyPath=image,SourceProjectPath=source,LogDirectory=root,Manifest=artifact.Manifest with{AssemblyPath=image}};
    }

    private static string Resolve(string root,string relative){
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathFullyQualified(relative)){throw new InvalidDataException("Native package references must be relative.");}
        var path=Path.GetFullPath(Path.Combine(root,relative));
        if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)){throw new InvalidDataException("A native package path escapes its directory.");}return path;
    }
    private static string FileName(string assemblyName){
        if(string.IsNullOrWhiteSpace(assemblyName)||assemblyName.IndexOfAny(Path.GetInvalidFileNameChars())>=0||assemblyName is "." or ".."){throw new InvalidDataException("Invalid native package assembly name.");}
        return assemblyName+".dll";
    }
    private static string CopyImage(string source,string target,CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        if(File.Exists(target)){var existing=Hash(target);if(existing!=Hash(source)){throw new InvalidDataException("Native package assemblies have conflicting names.");}return existing;}
        using(var input=File.OpenRead(source)){
            using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);var buffer=new byte[65536];int count;
            while((count=input.Read(buffer))!=0){cancellationToken.ThrowIfCancellationRequested();output.Write(buffer,0,count);}output.Flush(true);
        }
        return Hash(target);
    }
    private static void WriteJson<T>(string path,T value){
        using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(output,value);
        if(output.Length>MaximumMetadataBytes){throw new InvalidDataException("The native package metadata exceeds the supported size.");}output.Flush(true);
    }
    private static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input));}
}
