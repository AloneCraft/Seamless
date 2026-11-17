using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Hosting;

public sealed record NativeQuerySourceFile(string RelativePath,string Content);
public sealed record NativeQuerySourceProject(string ModuleId,string EntryType,IReadOnlyList<NativeQuerySourceFile> Files,IReadOnlyList<NativeQueryDefinition> Queries,IReadOnlyList<string> Dependencies);
public sealed record NativeDatabaseAssembly(string Database,string SchemaFingerprint,string AssemblyPath){
    public static NativeDatabaseAssembly From<TDatabase>(string database) where TDatabase:DatabaseContainer=>From(database,typeof(TDatabase).Assembly);
    public static NativeDatabaseAssembly From(string database,global::System.Reflection.Assembly assembly){
        ArgumentException.ThrowIfNullOrWhiteSpace(database);ArgumentNullException.ThrowIfNull(assembly);
        return new(database,NativeDatabaseIdentity.Fingerprint(assembly),global::Seamless.Execution.CompilationReferences.PathFor(assembly));
    }
}
public sealed record NativeAssemblyDependency(string Database,string SchemaFingerprint,string AssemblyName,string Sha256);
public sealed record NativeQueryBuildArtifact(string Id,string AssemblyPath,string Sha256,string SourceProjectPath,string LogDirectory,NativeModuleManifest Manifest,IReadOnlyList<NativeAssemblyDependency> Dependencies){
    public string SourceProjectSha256{get;init;}="";
    public bool IsPrebuilt{get;init;}
}

public sealed partial class GeneratedAssemblyBuilder{
    public async Task<NativeQueryBuildArtifact> BuildNativeAsync(NativeQuerySourceProject source,IReadOnlyList<NativeDatabaseAssembly> databases,string productVersion,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(databases);cancellationToken.ThrowIfCancellationRequested();
        if(string.IsNullOrWhiteSpace(source.ModuleId)||string.IsNullOrWhiteSpace(source.EntryType)||source.Files.Count==0||source.Queries.Count==0){throw new InvalidDataException("A native source project requires an ID, entry point, sources and queries.");}
        // Capture caller-owned lists before awaiting the compiler; only plain metadata enters the source manifest.
        source=JsonSerializer.Deserialize<NativeQuerySourceProject>(JsonSerializer.Serialize(source))!;
        var inputs=databases.ToDictionary(static item=>item.Database,StringComparer.OrdinalIgnoreCase);
        if(source.Dependencies.Count==0||source.Dependencies.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=source.Dependencies.Count||source.Dependencies.Any(name=>!inputs.ContainsKey(name))||source.Queries.Select(static query=>query.Id).Distinct(StringComparer.Ordinal).Count()!=source.Queries.Count||source.Queries.Any(query=>!source.Dependencies.Contains(query.Database,StringComparer.OrdinalIgnoreCase))){throw new InvalidDataException("Invalid native query dependencies or query IDs.");}
        var evidence=new List<NativeAssemblyDependency>();
        foreach(var name in source.Dependencies){var input=inputs[name];if(!Path.IsPathFullyQualified(input.AssemblyPath)){throw new InvalidDataException("A native database reference requires an absolute assembly path.");}using var file=File.OpenRead(input.AssemblyPath);evidence.Add(new(name,input.SchemaFingerprint,global::System.Reflection.AssemblyName.GetAssemblyName(input.AssemblyPath).Name!,Convert.ToHexString(SHA256.HashData(file))));}
        var id=Guid.NewGuid().ToString("N");var root=Path.GetFullPath(Path.Combine(this.options.WorkingDirectory,id));var cache=Path.GetFullPath(Path.Combine(this.options.CacheDirectory,id));Directory.CreateDirectory(root);Directory.CreateDirectory(cache);
        var sourceRoot=Path.Combine(root,"Sources");Directory.CreateDirectory(sourceRoot);var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in source.Files){
            var path=Path.GetFullPath(Path.Combine(sourceRoot,file.RelativePath));
            if(!path.StartsWith(sourceRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)||!paths.Add(path)){throw new InvalidDataException("Invalid native source path: "+file.RelativePath);}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,file.Content,cancellationToken).ConfigureAwait(false);
        }
        var assemblyName="Seamless.Generated.Native_"+id;
        var properties=new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),new XElement("ImplicitUsings","enable"),new XElement("AssemblyName",assemblyName),new XElement("OutputPath",Path.Combine(root,"assembly")+Path.DirectorySeparatorChar),new XElement("AppendTargetFrameworkToOutputPath","false"),new XElement("UseSharedCompilation","false"),new XElement("NuGetAudit","false"));
        // 設定済みFLinq analyzerが通常Tableを展開できるよう、Nativeの独立ビルドにも同じcompiler契約を渡す。
        properties.Add(new XElement("Features","InterceptorsPreview"),new XElement("InterceptorsNamespaces","Intercept"),new XElement("InterceptorsPreviewNamespaces","Intercept"),new XElement("EmitCompilerGeneratedFiles","true"),new XElement("CompilerGeneratedFilesOutputPath",Path.Combine(cache,"generated")));
        var references=new XElement("ItemGroup",this.options.ReferencePaths.Concat(source.Dependencies.Select(name=>inputs[name].AssemblyPath)).Distinct(StringComparer.OrdinalIgnoreCase).Select(path=>new XElement("Reference",new XAttribute("Include",global::System.Reflection.AssemblyName.GetAssemblyName(path).Name!),new XElement("HintPath",path),new XElement("Private","false"))));
        foreach(var analyzer in this.options.NativeAnalyzerPaths){references.Add(new XElement("Analyzer",new XAttribute("Include",analyzer)));}
        var project=Path.Combine(root,"Native.csproj");new XDocument(new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),properties,references)).Save(project);
        new XDocument(new XElement("configuration",new XElement("packageSources",new XElement("clear")))).Save(Path.Combine(root,"NuGet.Config"));
        var sourceManifest=Path.Combine(root,"native-source.json");await File.WriteAllTextAsync(sourceManifest,JsonSerializer.Serialize(source),cancellationToken).ConfigureAwait(false);
        await this.CompileProjectAsync(root,cache,project,cancellationToken).ConfigureAwait(false);
        foreach(var dependency in evidence){using var file=File.OpenRead(inputs[dependency.Database].AssemblyPath);if(Convert.ToHexString(SHA256.HashData(file))!=dependency.Sha256){throw new InvalidDataException("A native database reference changed during compilation.");}}
        var assembly=Path.Combine(root,"assembly",assemblyName+".dll");string hash;using(var file=File.OpenRead(assembly)){hash=Convert.ToHexString(SHA256.HashData(file));}
        var manifest=new NativeModuleManifest(source.ModuleId,Path.GetFileName(assembly),hash,source.EntryType,productVersion,source.Queries.Select(query=>query with{SchemaFingerprint=inputs[query.Database].SchemaFingerprint}).ToArray());
        string sourceHash;using(var file=File.OpenRead(sourceManifest)){sourceHash=Convert.ToHexString(SHA256.HashData(file));}
        var artifact=new NativeQueryBuildArtifact(id,assembly,hash,sourceManifest,cache,manifest,evidence.AsReadOnly()){SourceProjectSha256=sourceHash};
        await File.WriteAllTextAsync(Path.Combine(root,"native-build.json"),JsonSerializer.Serialize(artifact),cancellationToken).ConfigureAwait(false);return artifact;
    }
}
