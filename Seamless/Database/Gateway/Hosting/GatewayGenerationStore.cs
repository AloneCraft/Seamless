using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Database.Rdbms;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Hosting;

public sealed record GatewayGenerationReference(int DatabaseId,string GenerationId,string Manifest,string ManifestSha256);

/// <summary>確定時の seed と更新用 live を分離し、commit が参照するデータ・型・カタログを検証する。</summary>
public sealed partial class GatewayGenerationStore{
    private readonly string directory;
    private readonly GeneratedAssemblyBuilder builder;
    private readonly DatabaseContainerOptions options;
    private readonly ResourceBudget budget;
    private readonly Func<GatewayDatabaseProfile,GatewayVersionedBinding>? versionedBindings;
    private static readonly JsonSerializerOptions json=new(){IncludeFields=true,DefaultIgnoreCondition=global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull};
    public GatewayGenerationStore(string directory,GeneratedAssemblyBuilder builder,DatabaseContainerOptions options,ResourceBudget? budget=null):this(directory,builder,options,budget,null){}
    internal GatewayGenerationStore(string directory,GeneratedAssemblyBuilder builder,DatabaseContainerOptions options,ResourceBudget? budget,Func<GatewayDatabaseProfile,GatewayVersionedBinding>? versionedBindings){
        ArgumentNullException.ThrowIfNull(builder);ArgumentNullException.ThrowIfNull(options);if(!Path.IsPathFullyQualified(directory)){throw new ArgumentException("Generation storage requires an absolute path.",nameof(directory));}
        if(options.TransactionHistory is not null&&versionedBindings is null){throw new ArgumentException("A versioned generation store requires its common domain owner.",nameof(versionedBindings));}
        this.directory=Path.GetFullPath(directory);this.builder=builder;this.options=options;this.budget=budget??new(256L<<20,16L<<30);this.versionedBindings=versionedBindings;Directory.CreateDirectory(this.directory);
    }
    public async Task<GatewayGenerationCandidate> BuildAsync(GatewaySchemaCatalog schema,GatewayDatabaseProfile profile,GatewayTransactionOwner owner,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(schema);schema.Validate();if(profile.WalId==Guid.Empty||profile.DatabaseId<=0||owner.TransactionId==Guid.Empty){throw new ArgumentException("A WAL, database and owner identity are required.");}
        var encoded=JsonSerializer.SerializeToUtf8Bytes(schema,json);using var memory=this.budget.ReserveMemory(checked(encoded.LongLength*4));var owned=JsonSerializer.Deserialize<GatewaySchemaCatalog>(encoded,json)??throw new InvalidDataException("Missing schema.");owned.Validate();
        var generated=await this.builder.BuildAsync(owned.Definition,cancellationToken).ConfigureAwait(false);var generationId=Guid.ParseExact(generated.Id,"N");profile=profile with{GenerationId=generationId};var root=this.Resolve(Path.Combine(profile.DatabaseId.ToString(global::System.Globalization.CultureInfo.InvariantCulture),generated.Id));
        if(Directory.Exists(root)){throw new IOException("The generation directory already exists.");}Directory.CreateDirectory(root);var assembly=Path.Combine(root,"database.dll");CopyFile(generated.AssemblyPath,assembly,cancellationToken);
        var artifact=generated with{AssemblyPath=assembly};LoadedDatabaseGeneration? loaded=null;
        try{var options=this.DatabaseOptions(profile,Path.Combine(root,"seed"));if(this.options.TransactionHistory is not null){options=options with{GatewayVersionedSeed=new(profile,owner)};}loaded=artifact.Load(options);loaded.Adapter.ApplyPersistentIdentities(owned);if(this.options.TransactionHistory is null){loaded.Database.ConfigureUnpublishedGatewayGeneration(owner);}return new(this,root,owned,profile,owner,artifact,loaded);}
        catch{loaded?.Dispose();throw;}
    }
    internal GatewayPreparedGeneration Seal(GatewayGenerationCandidate candidate,IGatewayDatabaseTransaction transaction,CancellationToken token){
        if(this.options.TransactionHistory is not null){throw new InvalidOperationException("Seal a versioned private seed before opening its common live binding.");}
        var snapshot=transaction.SealGatewaySnapshot(candidate.Owner);transaction.Commit();transaction.Dispose();return this.SealPrepared(candidate,snapshot,token);
    }
    internal GatewayPreparedGeneration SealVersioned(GatewayGenerationCandidate candidate,CancellationToken token){
        token.ThrowIfCancellationRequested();var snapshot=candidate.Loaded.Database.SealGatewayVersionedSeed(candidate.Owner);return this.SealPrepared(candidate,snapshot,token);
    }
    private GatewayPreparedGeneration SealPrepared(GatewayGenerationCandidate candidate,GatewayPreparedSnapshot snapshot,CancellationToken token){
        candidate.CloseDatabase();
        var seed=Path.Combine(candidate.Directory,"seed");var live=Path.Combine(candidate.Directory,"live");var files=new List<FileEvidence>();
        foreach(var source in Directory.EnumerateFiles(seed,"*",SearchOption.AllDirectories)){
            token.ThrowIfCancellationRequested();if(Path.GetFileName(source)=="store.lock"){continue;}if(files.Count>=65536){throw new InvalidDataException("The prepared snapshot exceeds the manifest file limit.");}
            var relative=Path.GetRelativePath(seed,source);var target=Inside(live,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);CopyFile(source,target,token);files.Add(new(relative,new FileInfo(source).Length,Hash(source)));
        }
        if(files.Count==0){throw new InvalidDataException("A generation needs a persisted snapshot.");}
        var metadata=new GeneratedMetadata(candidate.Artifact.Generated.SourceDatabaseName,candidate.Artifact.Generated.DatabaseName,candidate.Artifact.Generated.SchemaFingerprint,candidate.Artifact.Generated.TypeNamespace,candidate.Artifact.Generated.Tables,candidate.Artifact.Generated.Routines);
        var manifest=new GenerationManifest(1,candidate.Profile,candidate.Schema,Path.GetFileName(candidate.Artifact.AssemblyPath),candidate.Artifact.Sha256,candidate.Artifact.TypeName,metadata,snapshot,files.ToArray());var path=Path.Combine(candidate.Directory,"manifest.json");
        using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,manifest,json);output.Flush(true);}
        var reference=new GatewayGenerationReference(candidate.Profile.DatabaseId,candidate.Artifact.Id,Path.GetRelativePath(this.directory,path),Hash(path));
        return this.Open(reference,candidate.Profile.WalId,0);
    }
    public GatewayPreparedGeneration Open(GatewayGenerationReference reference,Guid walId,long publicationLsn){
        ArgumentNullException.ThrowIfNull(reference);if(reference.DatabaseId<=0||!Guid.TryParseExact(reference.GenerationId,"N",out var id)||publicationLsn<0){throw new InvalidDataException("Invalid generation reference.");}
        var path=this.Resolve(reference.Manifest);if(!string.Equals(Hash(path),reference.ManifestSha256,StringComparison.Ordinal)){throw new InvalidDataException("Generation manifest hash mismatch.");}
        var length=new FileInfo(path).Length;if(length>32L<<20){throw new InvalidDataException("Generation manifest is too large.");}using var memory=this.budget.ReserveMemory(checked(length*4));
        var manifest=JsonSerializer.Deserialize<GenerationManifest>(File.ReadAllBytes(path),json)??throw new InvalidDataException("Missing generation manifest.");manifest.Schema.Validate();
        if(manifest.Version!=1||manifest.Profile.WalId!=walId||manifest.Profile.DatabaseId!=reference.DatabaseId||manifest.Profile.GenerationId!=id||manifest.Files.Length>65536){throw new InvalidDataException("Generation/WAL/database identity mismatch.");}
        var root=Path.GetDirectoryName(path)!;var expected=Path.Combine(this.directory,reference.DatabaseId.ToString(global::System.Globalization.CultureInfo.InvariantCulture),reference.GenerationId,"manifest.json");if(!Path.GetFullPath(path).Equals(expected,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Generation path/identity mismatch.");}
        foreach(var file in manifest.Files){var seed=Inside(Path.Combine(root,"seed"),file.Path);if(new FileInfo(seed).Length!=file.Length||Hash(seed)!=file.Sha256){throw new InvalidDataException("Prepared generation data hash mismatch: "+file.Path);}}
        var generated=new GeneratedDatabase(manifest.Generated.SourceName,manifest.Generated.DatabaseName,manifest.Generated.Fingerprint,[],manifest.Generated.Tables){RootNamespace=manifest.Generated.Namespace,Routines=manifest.Generated.Routines,IsCollectible=true};
        var artifact=new GeneratedAssemblyArtifact(reference.GenerationId,Inside(root,manifest.Assembly),manifest.AssemblySha256,manifest.TypeName,generated,string.Empty){Catalog=manifest.Schema.Definition};LoadedDatabaseGeneration? loaded=null;GatewayVersionedBinding? binding=null;
        try{
            if(!Directory.Exists(Path.Combine(root,"live"))){throw new InvalidDataException("The generation live dataset is missing; recovery cannot fall back to an old snapshot.");}
            var options=this.DatabaseOptions(manifest.Profile,Path.Combine(root,"live"));if(this.options.TransactionHistory is not null){binding=this.versionedBindings!(manifest.Profile);options=options with{GatewayVersionedBinding=binding};}
            loaded=artifact.Load(options);loaded.Adapter.ApplyPersistentIdentities(manifest.Schema);
            if(binding is not null){loaded.Database.InitializeGatewayVersionedStorage(manifest.Snapshot.DataSha256);}
            else{loaded.Database.InitializeGatewayStorage();if(loaded.Database.GatewayCheckpointLsn==0&&loaded.Database.GetGatewayDataHash()!=manifest.Snapshot.DataSha256){throw new InvalidDataException("The live data does not match the sealed snapshot.");}loaded.Database.SetGatewayReplayFloor(publicationLsn);}
            return new(reference,manifest.Schema,artifact,loaded,manifest.Snapshot);
        }catch{if(loaded is not null){loaded.Dispose();}else{binding?.Dispose();}throw;}
    }
    private DatabaseContainerOptions DatabaseOptions(GatewayDatabaseProfile profile,string directory)=>this.options with{StorageMode=DatabaseStorageMode.Storage,StorageDirectory=directory,RecordSerialization=DatabaseRecordSerialization.CanonicalV1,GatewayProfile=profile,TemporaryDirectory=Path.Combine(directory,"temporary")};
    private string Resolve(string relative)=>Inside(this.directory,relative);
    private static string Inside(string root,string relative){if(string.IsNullOrEmpty(relative)||Path.IsPathRooted(relative)){throw new InvalidDataException("An artifact path must be relative.");}var full=Path.GetFullPath(Path.Combine(root,relative));var prefix=Path.GetFullPath(root);if(!Path.EndsInDirectorySeparator(prefix)){prefix+=Path.DirectorySeparatorChar;}if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("An artifact path escapes the generation directory.");}return full;}
    private static string Hash(string path){using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexString(SHA256.HashData(input));}
    private static void CopyFile(string source,string target,CancellationToken token){using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);var buffer=new byte[65536];int count;while((count=input.Read(buffer))!=0){token.ThrowIfCancellationRequested();output.Write(buffer,0,count);}output.Flush(true);}
    private sealed record FileEvidence(string Path,long Length,string Sha256);
    private sealed record GeneratedMetadata(string SourceName,string DatabaseName,string Fingerprint,string Namespace,IReadOnlyList<GeneratedTableBinding> Tables,IReadOnlyList<GeneratedRoutineBinding> Routines);
    private sealed record GenerationManifest(int Version,GatewayDatabaseProfile Profile,GatewaySchemaCatalog Schema,string Assembly,string AssemblySha256,string TypeName,GeneratedMetadata Generated,GatewayPreparedSnapshot Snapshot,FileEvidence[] Files);
}

public sealed class GatewayGenerationCandidate:IDisposable{
    private readonly GatewayGenerationStore store;
    private LoadedDatabaseGeneration? loaded;
    internal GatewayGenerationCandidate(GatewayGenerationStore store,string directory,GatewaySchemaCatalog schema,GatewayDatabaseProfile profile,GatewayTransactionOwner owner,GeneratedAssemblyArtifact artifact,LoadedDatabaseGeneration loaded){this.store=store;this.Directory=directory;this.Schema=schema;this.Profile=profile;this.Owner=owner;this.Artifact=artifact;this.loaded=loaded;}
    internal string Directory{get;}
    public GatewaySchemaCatalog Schema{get;internal set;}
    public GatewayDatabaseProfile Profile{get;}
    public GatewayTransactionOwner Owner{get;}
    public GeneratedAssemblyArtifact Artifact{get;}
    public LoadedDatabaseGeneration Loaded=>this.loaded??throw new ObjectDisposedException(nameof(GatewayGenerationCandidate));
    public GatewayPreparedGeneration Seal(IGatewayDatabaseTransaction transaction,CancellationToken token=default)=>this.store.Seal(this,transaction,token);
    internal GatewayPreparedGeneration SealVersioned(CancellationToken token=default)=>this.store.SealVersioned(this,token);
    internal void CloseDatabase(){this.loaded?.Dispose();this.loaded=null;}
    public void Dispose()=>this.CloseDatabase();
}
public sealed class GatewayPreparedGeneration:IDisposable{
    internal GatewayPreparedGeneration(GatewayGenerationReference reference,GatewaySchemaCatalog schema,GeneratedAssemblyArtifact artifact,LoadedDatabaseGeneration loaded,GatewayPreparedSnapshot snapshot){this.Reference=reference;this.Schema=schema;this.Artifact=artifact;this.Loaded=loaded;this.Snapshot=snapshot;}
    public GatewayGenerationReference Reference{get;}
    public GatewaySchemaCatalog Schema{get;}
    public GeneratedAssemblyArtifact Artifact{get;}
    public LoadedDatabaseGeneration Loaded{get;}
    public GatewayPreparedSnapshot Snapshot{get;}
    public void Dispose()=>this.Loaded.Dispose();
}
