using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Database.Client;
using Seamless.Execution;
using Seamless.Database.Gateway.Native;
using System.Collections.Concurrent;

namespace Seamless.Database.Gateway.Hosting;

public sealed record GatewayNativeQueryReference(string Id,string Manifest,string Sha256);

/// <summary>共送Ecommit が参照する前に、Native のソースと成果物を不変領域へ保存する、E/summary>
public sealed class GatewayNativeQueryStore{
    private readonly string directory;
    private readonly GeneratedAssemblyBuilder builder;
    private readonly ResourceBudget budget;
    public GatewayNativeQueryStore(string directory,GeneratedAssemblyBuilder builder,ResourceBudget budget){
        if(!Path.IsPathFullyQualified(directory)){throw new ArgumentException("Native query storage requires an absolute directory.",nameof(directory));}
        this.directory=Path.GetFullPath(directory);this.builder=builder;this.budget=budget;Directory.CreateDirectory(this.directory);
    }
    public async Task<GatewayNativeQueryVersion> PrepareAsync(NativeQuerySourceProject source,IReadOnlyList<NativeDatabaseAssembly> databases,string productVersion,bool disableOnCompilationError=false,CancellationToken cancellationToken=default,IReadOnlyDictionary<string,Assembly>? contractAssemblies=null){
        var sourceBytes=JsonSerializer.SerializeToUtf8Bytes(source);using var memory=this.budget.ReserveMemory(checked(sourceBytes.LongLength*4+65536));
        source=JsonSerializer.Deserialize<NativeQuerySourceProject>(sourceBytes)!;NativeQueryBuildArtifact? artifact=null;string? diagnostic=null;
        try{artifact=await this.builder.BuildNativeAsync(source,databases,productVersion,cancellationToken).ConfigureAwait(false);}
        catch(InvalidDataException error)when(disableOnCompilationError&&error.Message.StartsWith("The generated project did not compile. Logs: ",StringComparison.Ordinal)){
            var log=error.Message["The generated project did not compile. Logs: ".Length..];
            using var reader=new StreamReader(Path.Combine(log,"build.stdout.log"));var characters=new char[16384];var count=await reader.ReadBlockAsync(characters.AsMemory(),cancellationToken).ConfigureAwait(false);diagnostic=error.Message+Environment.NewLine+new string(characters,0,count);
        }
        NativeModuleManifest manifest;IReadOnlyList<NativeAssemblyDependency> dependencies;
        if(artifact is not null){manifest=artifact.Manifest;dependencies=artifact.Dependencies;}
        else{
            var inputs=databases.ToDictionary(static database=>database.Database,StringComparer.OrdinalIgnoreCase);
            manifest=new(source.ModuleId,"","",source.EntryType,productVersion,source.Queries.Select(query=>query with{SchemaFingerprint=inputs[query.Database].SchemaFingerprint}).ToArray());
            dependencies=source.Dependencies.Select(name=>new NativeAssemblyDependency(name,inputs[name].SchemaFingerprint,AssemblyName.GetAssemblyName(inputs[name].AssemblyPath).Name!,Hash(inputs[name].AssemblyPath))).ToArray();
        }
        return this.Persist(sourceBytes,artifact,manifest,dependencies,diagnostic,false,cancellationToken,contractAssemblies);
    }
    /// <summary>共通ビルド済みのDLLを検査し、独立した�E開世代へ取り込む、E/summary>
    public GatewayNativeQueryVersion PreparePrebuilt(NativeQueryBuildArtifact artifact,IReadOnlyList<NativeDatabaseAssembly> databases,string productVersion,IReadOnlyDictionary<string,Assembly> contractAssemblies,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(artifact);cancellationToken.ThrowIfCancellationRequested();
        if(artifact.Manifest is null||artifact.Manifest.Queries is null||artifact.Dependencies is null||artifact.Manifest.Queries.Count is <1 or >1024||artifact.Dependencies.Count is <1 or >1024){throw new InvalidDataException("Invalid prebuilt native manifest or dependency count.");}
        if(!Path.IsPathFullyQualified(artifact.AssemblyPath)||!Path.IsPathFullyQualified(artifact.SourceProjectPath)||artifact.Manifest.ProductVersion!=productVersion||artifact.Manifest.AssemblySha256!=artifact.Sha256){throw new InvalidDataException("Invalid prebuilt native paths, version or assembly hash.");}
        artifact=artifact with{Id=Guid.NewGuid().ToString("N"),Manifest=artifact.Manifest with{Queries=Array.AsReadOnly(artifact.Manifest.Queries.ToArray())},Dependencies=Array.AsReadOnly(artifact.Dependencies.ToArray()),IsPrebuilt=true};
        using var sourceFile=File.OpenRead(artifact.SourceProjectPath);
        if(sourceFile.Length is <=0 or >16*1024*1024){throw new InvalidDataException("The prebuilt native metadata exceeds the supported size.");}
        using var memory=this.budget.ReserveMemory(checked(sourceFile.Length*4+65536));
        var sourceBytes=new byte[checked((int)sourceFile.Length)];sourceFile.ReadExactly(sourceBytes);
        if(Convert.ToHexString(SHA256.HashData(sourceBytes))!=artifact.SourceProjectSha256){throw new InvalidDataException("Prebuilt native metadata hash mismatch.");}
        var source=JsonSerializer.Deserialize<NativeQuerySourceProject>(sourceBytes)??throw new InvalidDataException("Missing prebuilt native metadata.");
        if(source.Files is null||source.Files.Count>4096||source.Files.Any(static file=>file is null||file.RelativePath is null||file.Content is null)||source.Queries is null||source.Dependencies is null||source.Queries.Any(static query=>query is null)
            ||string.IsNullOrWhiteSpace(source.ModuleId)||string.IsNullOrWhiteSpace(source.EntryType)||source.ModuleId!=artifact.Manifest.ModuleId||source.EntryType!=artifact.Manifest.EntryType
            ||source.Dependencies.Count is <1 or >1024||source.Queries.Count is <1 or >1024||source.Dependencies.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=source.Dependencies.Count
            ||source.Queries.Select(static query=>query.Id).Distinct(StringComparer.Ordinal).Count()!=source.Queries.Count||!source.Queries.SequenceEqual(artifact.Manifest.Queries)
            ||source.Queries.Any(query=>!source.Dependencies.Contains(query.Database,StringComparer.OrdinalIgnoreCase)||!ContractHash(query.ArgumentsContract)||!ContractHash(query.ResultContract))){throw new InvalidDataException("Prebuilt native source and manifest disagree.");}
        var inputs=databases.ToDictionary(static item=>item.Database,StringComparer.OrdinalIgnoreCase);
        if(artifact.Dependencies.Count!=source.Dependencies.Count||artifact.Dependencies.Select(static item=>item.Database).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=artifact.Dependencies.Count){throw new InvalidDataException("Invalid prebuilt native dependencies.");}
        foreach(var dependency in artifact.Dependencies){
            cancellationToken.ThrowIfCancellationRequested();
            if(!source.Dependencies.Contains(dependency.Database,StringComparer.OrdinalIgnoreCase)||!inputs.TryGetValue(dependency.Database,out var input)
                ||dependency.SchemaFingerprint!=input.SchemaFingerprint||AssemblyName.GetAssemblyName(input.AssemblyPath).Name!=dependency.AssemblyName||Hash(input.AssemblyPath)!=dependency.Sha256
                ||source.Queries.Any(query=>query.Database.Equals(dependency.Database,StringComparison.OrdinalIgnoreCase)&&query.SchemaFingerprint!=input.SchemaFingerprint)){
                throw new InvalidDataException("Prebuilt native database dependency mismatch: "+dependency.Database);
            }
        }
        return this.Persist(sourceBytes,artifact,artifact.Manifest,artifact.Dependencies,null,true,cancellationToken,contractAssemblies);
    }
    internal GatewayNativeQueryVersion DisablePrebuilt(NativeQuerySourceProject source,IReadOnlyList<NativeDatabaseAssembly> databases,string productVersion,CancellationToken cancellationToken){
        var inputs=databases.ToDictionary(static item=>item.Database,StringComparer.OrdinalIgnoreCase);
        source=source with{Queries=source.Queries.Select(query=>query with{SchemaFingerprint=inputs[query.Database].SchemaFingerprint}).ToArray()};
        var bytes=JsonSerializer.SerializeToUtf8Bytes(source);using var memory=this.budget.ReserveMemory(checked(bytes.LongLength*4+65536));
        var manifest=new NativeModuleManifest(source.ModuleId,"","",source.EntryType,productVersion,source.Queries);
        var dependencies=source.Dependencies.Select(name=>new NativeAssemblyDependency(name,inputs[name].SchemaFingerprint,AssemblyName.GetAssemblyName(inputs[name].AssemblyPath).Name!,Hash(inputs[name].AssemblyPath))).ToArray();
        return this.Persist(bytes,null,manifest,dependencies,"The prebuilt native module requires a new artifact for the current database generation.",true,cancellationToken,null);
    }
    private static bool ContractHash(string? value)=>value is{Length:64}&&value.All(static character=>char.IsAsciiHexDigit(character));
    private GatewayNativeQueryVersion Persist(byte[] sourceBytes,NativeQueryBuildArtifact? artifact,NativeModuleManifest manifest,IReadOnlyList<NativeAssemblyDependency> dependencies,string? diagnostic,bool prebuilt,CancellationToken cancellationToken,IReadOnlyDictionary<string,Assembly>? contractAssemblies){
        cancellationToken.ThrowIfCancellationRequested();var id=artifact?.Id??Guid.NewGuid().ToString("N");var root=this.Resolve(id);if(Directory.Exists(root)){throw new IOException("Native query generation already exists.");}Directory.CreateDirectory(root);
        var sourcePath=Path.Combine(root,"source.json");using(var output=new FileStream(sourcePath,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(sourceBytes);output.Flush(true);}
        if(artifact is not null){Copy(artifact.AssemblyPath,Path.Combine(root,"module.dll"),cancellationToken);if(Hash(Path.Combine(root,"module.dll"))!=artifact.Sha256){throw new InvalidDataException("Native query DLL hash mismatch after copying.");}manifest=manifest with{AssemblyPath="module.dll"};}
        var sourceHash=Convert.ToHexString(SHA256.HashData(sourceBytes));
        if(artifact is not null&&contractAssemblies is not null){var storedArtifact=artifact with{AssemblyPath=Path.Combine(root,"module.dll"),SourceProjectPath=sourcePath,SourceProjectSha256=sourceHash,Manifest=manifest};using var loaded=new LoadedNativeQueryModule(storedArtifact,contractAssemblies);manifest=loaded.AddContracts(manifest);}
        cancellationToken.ThrowIfCancellationRequested();var metadata=new Stored(1,id,manifest,dependencies.ToArray(),sourceHash,artifact?.LogDirectory??"",diagnostic,prebuilt);
        var path=Path.Combine(root,"manifest.json");using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,metadata);output.Flush(true);}
        return this.Read(new(id,Path.GetRelativePath(this.directory,path),Hash(path)));
    }
    public GatewayNativeQueryVersion Read(GatewayNativeQueryReference reference){
        if(!Guid.TryParseExact(reference.Id,"N",out _)){throw new InvalidDataException("Invalid native query generation ID.");}
        var root=this.Resolve(reference.Id);var path=this.Resolve(reference.Manifest);
        if(path!=Path.Combine(root,"manifest.json")||Hash(path)!=reference.Sha256){throw new InvalidDataException("Native query manifest path or hash mismatch.");}
        var stored=JsonSerializer.Deserialize<Stored>(File.ReadAllBytes(path))??throw new InvalidDataException("Missing native query manifest.");
        if(stored.Version!=1||stored.Id!=reference.Id||stored.Dependencies.Length==0||stored.Dependencies.Select(static dependency=>dependency.Database).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=stored.Dependencies.Length){throw new InvalidDataException("Invalid native query manifest.");}
        var sourcePath=Path.Combine(root,"source.json");if(Hash(sourcePath)!=stored.SourceSha256){throw new InvalidDataException("Native query source hash mismatch.");}
        stored=stored with{Manifest=stored.Manifest with{Queries=Array.AsReadOnly(stored.Manifest.Queries.ToArray())}};
        var source=JsonSerializer.Deserialize<NativeQuerySourceProject>(File.ReadAllBytes(sourcePath))??throw new InvalidDataException("Missing native source project.");
        source=source with{Files=Array.AsReadOnly(source.Files.ToArray()),Queries=Array.AsReadOnly(source.Queries.ToArray()),Dependencies=Array.AsReadOnly(source.Dependencies.ToArray())};
        if(source.ModuleId!=stored.Manifest.ModuleId||source.EntryType!=stored.Manifest.EntryType||source.Dependencies.Count!=stored.Dependencies.Length||source.Dependencies.Any(name=>!stored.Dependencies.Any(dependency=>dependency.Database.Equals(name,StringComparison.OrdinalIgnoreCase)))
            ||stored.IsPrebuilt&&!source.Queries.SequenceEqual(stored.Manifest.Queries)){throw new InvalidDataException("Native query source and manifest disagree.");}
        NativeQueryBuildArtifact? artifact=null;
        if(stored.Diagnostic is null){
            if(stored.Manifest.AssemblyPath!="module.dll"||Hash(Path.Combine(root,"module.dll"))!=stored.Manifest.AssemblySha256){throw new InvalidDataException("Native query DLL hash mismatch.");}
            artifact=new(stored.Id,Path.Combine(root,"module.dll"),stored.Manifest.AssemblySha256,sourcePath,stored.LogDirectory,stored.Manifest,Array.AsReadOnly(stored.Dependencies)){SourceProjectSha256=stored.SourceSha256,IsPrebuilt=stored.IsPrebuilt};
        }
        return new(reference,source,stored.Manifest,artifact,stored.Diagnostic,stored.IsPrebuilt);
    }
    internal GatewayGenerationStore.CollectionPlan PlanCollection(IEnumerable<GatewayNativeQueryReference> retained){
        var keep=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var reference in retained){
            if(!Guid.TryParseExact(reference.Id,"N",out _)||this.Resolve(reference.Manifest)!=Path.Combine(this.Resolve(reference.Id),"manifest.json")){throw new InvalidDataException("Invalid retained native query generation.");}keep.Add(this.Resolve(reference.Id));
        }
        var candidates=new List<string>();var deferred=new List<GatewayGenerationCollectionDiagnostic>();
        try{
            if((File.GetAttributes(this.directory)&FileAttributes.ReparsePoint)!=0){throw new IOException("Native query collection does not traverse a storage reparse point.");}
            foreach(var directory in Directory.EnumerateDirectories(this.directory)){if(Guid.TryParseExact(Path.GetFileName(directory),"N",out _)&&!keep.Contains(directory)){candidates.Add(this.Resolve(Path.GetFileName(directory)));}}
        }catch(Exception error)when(error is IOException or UnauthorizedAccessException){deferred.Add(new(this.directory,error.Message));}
        return new(candidates,deferred);
    }
    private string Resolve(string relative){var result=Path.GetFullPath(Path.Combine(this.directory,relative));if(!result.StartsWith(this.directory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Native query path escapes its storage directory.");}return result;}
    private static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input));}
    private static void Copy(string source,string target,CancellationToken cancellationToken){using var input=File.OpenRead(source);using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);var buffer=new byte[65536];int count;while((count=input.Read(buffer))!=0){cancellationToken.ThrowIfCancellationRequested();output.Write(buffer,0,count);}output.Flush(true);}
    private sealed record Stored(int Version,string Id,NativeModuleManifest Manifest,NativeAssemblyDependency[] Dependencies,string SourceSha256,string LogDirectory,string? Diagnostic,bool IsPrebuilt=false);
}

public sealed class GatewayNativeQueryVersion:IDisposable{
    private LoadedNativeQueryModule? loaded;
    private ResourceReservation? memory;
    private readonly ConcurrentDictionary<string,(NativeWireValidator? Arguments,NativeRecordQueryContract? Records,bool SchemaIndependent)> wireProfiles=new(StringComparer.Ordinal);
    internal void Account(ResourceBudget budget){
        if(this.memory is not null){return;}var bytes=checked(1024L+this.Source.Files.Sum(static file=>128L+2L*file.RelativePath.Length+2L*file.Content.Length)+this.Source.Dependencies.Sum(static name=>64L+2L*name.Length)+this.Source.Queries.Count*512L+(this.Diagnostic?.Length??0)*2L);
        this.memory=budget.ReserveMemory(bytes);
    }
    internal GatewayNativeQueryVersion(GatewayNativeQueryReference reference,NativeQuerySourceProject source,NativeModuleManifest manifest,NativeQueryBuildArtifact? artifact,string? diagnostic,bool isPrebuilt=false){this.Reference=reference;this.Source=source;this.Manifest=manifest;this.Artifact=artifact;this.Diagnostic=diagnostic;this.IsPrebuilt=isPrebuilt;}
    public GatewayNativeQueryReference Reference{get;}
    public NativeQuerySourceProject Source{get;}
    public NativeModuleManifest Manifest{get;}
    public NativeQueryBuildArtifact? Artifact{get;}
    public string? Diagnostic{get;}
    public bool IsPrebuilt{get;}
    public bool IsEnabled=>this.Artifact is not null;
    public INativeQueryModule Module=>this.loaded?.Module??throw new InvalidOperationException(this.Diagnostic??"The native module has not been bound to its database generations.");
    internal NativeQueryExecution Describe(Guid walId,NativeQueryDefinition definition,IEnumerable<(string Name,InProcessDatabaseAdapter Adapter)> databases,ResourceBudget validationBudget){
        var loaded=this.loaded??throw new ObjectDisposedException(nameof(GatewayNativeQueryVersion));
        if(this.Artifact is null||!this.Manifest.Queries.Any(query=>ReferenceEquals(query,definition))){throw new InvalidDataException("The native execution definition does not belong to this version.");}
        var registrations=databases.ToDictionary(static item=>item.Name,static item=>item.Adapter,StringComparer.OrdinalIgnoreCase);
        var bindings=new List<NativeQueryDatabaseBinding>();
        foreach(var dependency in this.Artifact.Dependencies){
            if(!registrations.TryGetValue(dependency.Database,out var adapter)||adapter.Database.Options.GatewayProfile is not{} profile||profile.WalId!=walId
                ||!loaded.UsesDatabaseAssembly(dependency.AssemblyName,adapter.Database.GetType().Assembly)){
                throw new NativeQueryException("The native module is not bound to the visible database generation.",true);
            }
            bindings.Add(new(dependency.Database,profile.DatabaseId,profile.GenerationId,dependency.SchemaFingerprint,dependency.Sha256));
        }
        var wire=this.wireProfiles.GetOrAdd(definition.Id,_=>{
            if(loaded.Module is INativeRecordQueryModule records){var contract=records.GetContract(definition.Id);contract.ValidateDefinition(definition);return(null,contract,false);}
            var arguments=loaded.ResolveContractType(definition.ArgumentsType);var result=loaded.ResolveContractType(definition.ResultType);
            return(NativeWireValidation.For(arguments),null,NativeQueryExecution.HasFixedWireContract(arguments)&&NativeQueryExecution.HasFixedWireContract(result));
        });
        return new(walId,Guid.ParseExact(this.Reference.Id,"N"),this.Reference.Sha256,this.Manifest.AssemblySha256,this.Manifest.ModuleId,this.Manifest.EntryType,definition,loaded.Module,bindings,wire.Arguments,wire.Records,wire.SchemaIndependent,validationBudget);
    }
    public void Bind(IReadOnlyDictionary<string,Assembly> databases){
        {
            this.ReleaseBinding();
            if(this.Artifact is not null){this.loaded=new(this.Artifact,databases);}
        }
    }
    private void ReleaseBinding(){
        try{this.loaded?.Dispose();}
        finally{this.loaded=null;this.wireProfiles.Clear();}
    }
    public void Dispose(){
        {
            try{this.ReleaseBinding();}
            finally{this.memory?.Dispose();this.memory=null;}
        }
    }
}
