using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Hosting;

public sealed record GeneratedAssemblyBuildOptions(string WorkingDirectory,string CacheDirectory,string DotNetExecutable,string GeneratorAssemblyPath,IReadOnlyList<string> ReferencePaths){
    public TimeSpan Timeout{get;init;}=TimeSpan.FromMinutes(10);
    public IReadOnlyList<string> NativeAnalyzerPaths{get;init;}=[];
}

/// <summary>配布された runtime / generator だけで、世代ごとの隔離プロジェクトをビルドする。</summary>
public sealed partial class GeneratedAssemblyBuilder{
    private readonly GeneratedAssemblyBuildOptions options;
    public GeneratedAssemblyBuilder(GeneratedAssemblyBuildOptions options){
        ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(options.ReferencePaths);this.options=options with{ReferencePaths=Array.AsReadOnly(options.ReferencePaths.ToArray()),NativeAnalyzerPaths=Array.AsReadOnly(options.NativeAnalyzerPaths.ToArray())};
        foreach(var path in new[]{options.WorkingDirectory,options.CacheDirectory,options.DotNetExecutable,options.GeneratorAssemblyPath}.Concat(options.ReferencePaths).Concat(options.NativeAnalyzerPaths)){if(!Path.IsPathFullyQualified(path)){throw new ArgumentException("Generation build paths must be absolute.",nameof(options));}}
        if(!File.Exists(options.DotNetExecutable)||!File.Exists(options.GeneratorAssemblyPath)||options.ReferencePaths.Concat(options.NativeAnalyzerPaths).Any(static path=>!File.Exists(path))){throw new FileNotFoundException("A configured generation build dependency is missing.");}
        if(options.Timeout<=TimeSpan.Zero){throw new ArgumentOutOfRangeException(nameof(options));}
    }
    public async Task<GeneratedAssemblyArtifact> BuildAsync(DatabaseCatalog catalog,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(catalog);cancellationToken.ThrowIfCancellationRequested();
        var generated=CSharpDatabaseGenerator.Generate(catalog,true,true,false,true);var id=Guid.NewGuid().ToString("N");
        var root=Path.Combine(this.options.WorkingDirectory,id);var cache=Path.Combine(this.options.CacheDirectory,id);Directory.CreateDirectory(root);Directory.CreateDirectory(cache);
        foreach(var source in generated.Files){
            var path=Path.GetFullPath(Path.Combine(root,"Sources",source.RelativePath));var prefix=Path.GetFullPath(Path.Combine(root,"Sources"))+Path.DirectorySeparatorChar;
            if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("A generated source path escapes the generation directory.");}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,source.Content,cancellationToken).ConfigureAwait(false);
        }
        var assemblyName="Seamless.Generated.Generation_"+id;
        var properties=new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),new XElement("AssemblyName",assemblyName),new XElement("BaseIntermediateOutputPath",Path.Combine(cache,"obj")+Path.DirectorySeparatorChar),new XElement("OutputPath",Path.Combine(root,"assembly")+Path.DirectorySeparatorChar),new XElement("AppendTargetFrameworkToOutputPath","false"),new XElement("UseSharedCompilation","false"),new XElement("NuGetAudit","false"));
        var references=new XElement("ItemGroup",this.options.ReferencePaths.Distinct(StringComparer.OrdinalIgnoreCase).Select(path=>new XElement("Reference",new XAttribute("Include",Path.GetFileNameWithoutExtension(path)),new XElement("HintPath",path),new XElement("Private","false"))));
        references.Add(new XElement("Analyzer",new XAttribute("Include",this.options.GeneratorAssemblyPath)));
        var project=Path.Combine(root,"Generation.csproj");new XDocument(new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),properties,references)).Save(project);
        new XDocument(new XElement("configuration",new XElement("packageSources",new XElement("clear")))).Save(Path.Combine(root,"NuGet.Config"));
        await this.CompileProjectAsync(root,cache,project,cancellationToken).ConfigureAwait(false);
        var assembly=Path.Combine(root,"assembly",assemblyName+".dll");string hash;using(var input=File.OpenRead(assembly)){hash=Convert.ToHexString(SHA256.HashData(input));}
        var typeName=generated.TypeNamespace+"."+generated.DatabaseName;
        var manifest=Path.Combine(root,"assembly.json");
        await using(var file=new FileStream(manifest,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){
            await JsonSerializer.SerializeAsync(file,new Manifest(1,id,Path.GetRelativePath(root,assembly),hash,typeName,generated.SchemaFingerprint),cancellationToken:cancellationToken).ConfigureAwait(false);file.Flush(true);
        }
        return new(id,assembly,hash,typeName,generated,cache){Catalog=catalog};
    }
    private async Task CompileProjectAsync(string root,string cache,string project,CancellationToken cancellationToken){
        var outputLog=Path.Combine(cache,"build.stdout.log");var errorLog=Path.Combine(cache,"build.stderr.log");
        var start=new ProcessStartInfo(this.options.DotNetExecutable){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{"build",project,"--configuration","Release","--nologo","-m:1","-p:UseSharedCompilation=false"}){start.ArgumentList.Add(argument);}
        var intermediate=Path.Combine(cache,"obj")+Path.DirectorySeparatorChar;
        start.ArgumentList.Add("-p:BaseIntermediateOutputPath="+intermediate);start.ArgumentList.Add("-p:MSBuildProjectExtensionsPath="+intermediate);
        var temporary=Path.Combine(root,"tmp");Directory.CreateDirectory(temporary);start.Environment["TEMP"]=temporary;start.Environment["TMP"]=temporary;
        start.Environment["DOTNET_CLI_HOME"]=Path.Combine(this.options.CacheDirectory,"cli");start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]="1";start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"]="1";start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"]="0";start.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"]="false";start.Environment["NUGET_PACKAGES"]=Path.Combine(this.options.CacheDirectory,"packages");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(this.options.Timeout);
        using var process=Process.Start(start)??throw new InvalidOperationException("The generation compiler could not be started.");
        await using var output=new FileStream(outputLog,FileMode.Create,FileAccess.Write,FileShare.Read);await using var error=new FileStream(errorLog,FileMode.Create,FileAccess.Write,FileShare.Read);
        var copyOutput=process.StandardOutput.BaseStream.CopyToAsync(output);var copyError=process.StandardError.BaseStream.CopyToAsync(error);
        try{await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);}
        catch(OperationCanceledException){
            if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);}
            await Task.WhenAll(copyOutput,copyError).ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();throw new TimeoutException("The generation build exceeded its timeout. Logs: "+cache);
        }
        await Task.WhenAll(copyOutput,copyError).ConfigureAwait(false);await output.FlushAsync(cancellationToken).ConfigureAwait(false);await error.FlushAsync(cancellationToken).ConfigureAwait(false);
        if(process.ExitCode!=0){throw new InvalidDataException("The generated project did not compile. Logs: "+cache);}
    }
    private sealed record Manifest(int Version,string Id,string Assembly,string Sha256,string TypeName,string SchemaFingerprint);
}

public sealed record GeneratedAssemblyArtifact(string Id,string AssemblyPath,string Sha256,string TypeName,GeneratedDatabase Generated,string LogDirectory){
    public DatabaseCatalog? Catalog{get;init;}
    public LoadedDatabaseGeneration Load(DatabaseContainerOptions options)=>new(this,options);
}

/// <summary>Database / adapter と ALC を同じ世代として所有する。外部の参照は lease 側で管理する。</summary>
public sealed class LoadedDatabaseGeneration:IDisposable{
    private GenerationLoadContext? context;
    private DatabaseContainer? database;
    private InProcessDatabaseAdapter? adapter;
    internal LoadedDatabaseGeneration(GeneratedAssemblyArtifact artifact,DatabaseContainerOptions options){
        this.Generated=artifact.Generated;
        if(!artifact.Generated.IsCollectible){throw new InvalidDataException("The generated database is not in the collectible profile.");}
        using(var input=File.OpenRead(artifact.AssemblyPath)){if(!string.Equals(Convert.ToHexString(SHA256.HashData(input)),artifact.Sha256,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("The generated assembly hash does not match.");}}
        var loading=new GenerationLoadContext();this.context=loading;this.UnloadReference=new(loading);
        try{
            var assembly=loading.LoadFromAssemblyPath(artifact.AssemblyPath);var type=assembly.GetType(artifact.TypeName,true)!;
            if(type.GetField("GeneratedSchemaFingerprint")?.GetRawConstantValue() as string!=artifact.Generated.SchemaFingerprint){throw new InvalidDataException("The generated schema fingerprint does not match.");}
            options=options with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1};
            this.database=(DatabaseContainer)Activator.CreateInstance(type,[options])!;
            var initialize=type.GetMethod("EnsureTablesCreated",Type.EmptyTypes)??throw new InvalidDataException("The generated database has no table initializer.");initialize.Invoke(this.database,null);this.adapter=new(this.database,artifact.Generated.Tables,artifact.Catalog);
        }catch(TargetInvocationException error)when(error.InnerException is not null){this.Dispose();global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        catch{this.Dispose();throw;}
    }
    public WeakReference UnloadReference{get;}
    public GeneratedDatabase Generated{get;}
    public DatabaseContainer Database=>this.database??throw new ObjectDisposedException(nameof(LoadedDatabaseGeneration));
    public InProcessDatabaseAdapter Adapter=>this.adapter??throw new ObjectDisposedException(nameof(LoadedDatabaseGeneration));
    public void Dispose(){
        this.database?.Dispose();this.database=null;this.adapter=null;var loading=this.context;this.context=null;if(loading is not null){CollectibleFormatterCache.Release(loading);loading.Unload();}
    }
    private sealed class GenerationLoadContext():AssemblyLoadContext(isCollectible:true){
        protected override Assembly? Load(AssemblyName name)=>Default.Assemblies.FirstOrDefault(assembly=>AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name));
    }
}
