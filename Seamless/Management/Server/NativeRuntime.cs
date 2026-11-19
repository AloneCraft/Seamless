using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Database;

namespace Seamless.Management.Server;

internal sealed record PreparedDefinition(Definition Definition,long ExpectedVersion,string ArtifactHash,string StoragePath){public string? PendingPath{get;init;}public AuditOrigin? AuditOrigin{get;init;}public string Database{get;init;}=Definition.Database;}
internal sealed record PreparedPublication(PreparedDefinition Primary,PreparedDefinition[] Dependents);
internal readonly record struct ReadAuditObservation(DateTimeOffset At,string SubjectId,string Database,string QueryId,string CodeVersion);
internal sealed record ArtifactManifest(Definition Definition,string Sha256,string? DatabaseArtifact,string? MetadataConsumer=null,Guid? AuditDestination=null){
    public long? PublishedVersion{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceProjectBuild? ImportedBuild{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFile{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TableStorageId{get;init;}

    internal Definition ReadDefinition(string directory){
        if(this.SourceFile is not{} file){return this.Definition;}
        if(this.Definition.Kind!=DefinitionKind.Database||!file.EndsWith(".cs",StringComparison.Ordinal)||!DatabaseDesigner.IsManagedDatabaseName(file[..^3])||
            this.Definition.Source.Files is not [var source]||source.Path!=file||source.Text.Length!=0){throw new InvalidDataException("DBソースの保存情報が不正です。");}
        var path=System.IO.Path.Combine(directory,file);SourceWorkspace.CheckLinks(path);
        using var stream=File.OpenRead(path);
        if(stream.Length>16L<<20){throw new ManagementException("Limit","DBソースが大きすぎます。");}
        using var reader=new StreamReader(stream,new System.Text.UTF8Encoding(false,true));
        var snapshot=this.Definition.Source with{Files=[source with{Text=reader.ReadToEnd()}]};
        SourceEditor.Validate(snapshot);
        return this.Definition with{Source=snapshot};
    }
}
internal sealed class ManagedLoadContext(Assembly? database=null,CapturedSourceBuild? imported=null):AssemblyLoadContext(isCollectible:true){
    protected override Assembly? Load(AssemblyName name){
        if(database is not null&&AssemblyName.ReferenceMatchesDefinition(database.GetName(),name)){return database;}
        if(name.Name is{} simpleName&&imported?.PrivateDependency(simpleName) is{} bytes){using var stream=new MemoryStream(bytes,false);return this.LoadFromStream(stream);}
        return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly=>AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name));
    }
}
internal sealed class DatabaseInstance(Definition definition,string path,CompiledSource compiled):IDisposable{
    internal readonly ManagedLoadContext Context=new(imported:compiled.ImportedBuild);
    private bool disposed;
    internal readonly Definition Definition=definition;
    internal readonly string Path=path;
    internal readonly CompiledSource Compiled=compiled;
    internal Assembly Assembly{get;private set;}=null!;
    internal DatabaseContainer Database{get;private set;}=null!;
    internal Guid? AuditDestination{get;private set;}
    internal void Open(DatabaseContainerOptions? debugOptions=null){
        using var stream=new MemoryStream(this.Compiled.AssemblyBytes,false);
        if(this.Compiled.ImportedBuild is{} imported&&imported.Build.PdbSha256.Length!=0){using var pdb=new MemoryStream(imported.PdbBytes,false);this.Assembly=this.Context.LoadFromStream(stream,pdb);}
        else{this.Assembly=this.Context.LoadFromStream(stream);}
        var type=this.Assembly.GetType(this.Definition.EntryType,true)!;
        if(!typeof(DatabaseContainer).IsAssignableFrom(type)){throw new ManagementException("Validation","DBの入口はDatabaseContainer派生型にしてください。");}
        var options=DatabaseContainerOptions.OnStorage(this.Path) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=TableMemoryBytes,ManagedMemoryLimitBytes=MemoryLimit(type),TemporaryDirectory=System.IO.Path.Combine(this.Path,"temporary"),MaximumQueryWorkers=Math.Min(8,Environment.ProcessorCount)};
        Dictionary<string,string>? directories=null;
        if(debugOptions is null){using var manifestFile=File.OpenRead(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(this.Path)!,"manifest.json"));var manifest=JsonSerializer.Deserialize<ArtifactManifest>(manifestFile,ManagementJson.Options)??throw new InvalidDataException("DBのmanifestがありません。");if(manifest.ReadDefinition(System.IO.Path.GetDirectoryName(this.Path)!).Hash()!=this.Definition.Hash()||manifest.Sha256!=this.Compiled.Result.ArtifactHash||manifest.MetadataConsumer is not null&&(manifest.MetadataConsumer!="audit-v1"||manifest.AuditDestination is null||manifest.AuditDestination==Guid.Empty)||manifest.MetadataConsumer is null&&manifest.AuditDestination is not null){throw new InvalidDataException("DBの監査プロファイルが一致しません。");}options=options with{TransactionMetadataConsumer=manifest.MetadataConsumer};this.AuditDestination=manifest.AuditDestination;
            directories=TableStorage.OpenDirectories(System.IO.Path.GetDirectoryName(this.Path)!,manifest.TableStorageId);
            if(directories is not null){options=options with{SharedStoreOptions=new(){ComponentDirectories=directories}};}
        }
        this.Database=(DatabaseContainer)(Activator.CreateInstance(type,[debugOptions??options])??throw new ManagementException("Validation","DBコンストラクターが必要です。"));
        if(directories is not null){
            var declarations=DatabaseTableDeclaration.Read(type).ToDictionary(table=>table.Path,StringComparer.Ordinal);
            foreach(var (name,path) in directories){
                if(!declarations.TryGetValue(name,out var declaration)){throw new ManagementException("TableStorage","テーブルが見つかりません。"+name+" "+path);}
                var table=declaration.GetTable(this.Database);
                if(table.GetType().GetProperty("Name")?.GetValue(table) is not string actual||actual!=name){
                    throw new ManagementException("TableStorage","テーブル名とストレージ名が一致しません。"+name+" "+path);
                }
            }
        }
        _=this.Database.Queries;
        foreach(var property in Tables(this.Database)){_=property.GetValue(this.Database); }
    }
    // 表ごとのログのメモリ(MemorySize)。表を開くときに、これを元に索引・ページの分を DB の上限から先に取り置く(空の表でも約15MB)。
    internal const long TableMemoryBytes=1L<<24;
    // DB の上限は、表の数に合わせて広げる(表16個までは従来の 256MB)。共通ストアの上限が DB の上限にそろったので効く(2026-11-16)。
    // 使う表だけを開く形にするまでの間の対処。設計：Document/refactoring-designs/20261116-使う表だけを開く.md。
    internal static long MemoryLimit(Type database)=>Math.Max(256L<<20,checked((64L<<20)+StorageCount(database)*TableMemoryBytes));
    // 自分の保存の仕組み(Tsavorite)を開く宣言の数：取引で書く表・集計の表・取引を通さない表。
    internal static int StorageCount(Type database)=>database.GetProperties(BindingFlags.Public|BindingFlags.Instance).Count(property=>property.PropertyType.IsGenericType&&
        property.PropertyType.GetGenericTypeDefinition() is var definition&&(definition==typeof(Table<,>)||definition==typeof(Table<>)||definition==typeof(MaterializedView<,>)||definition==typeof(global::Seamless.Database.Set<,>)||definition==typeof(global::Seamless.Database.Set<>)));
    internal static PropertyInfo[] Tables(DatabaseContainer database)=>database.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.PropertyType.IsGenericType&&(property.PropertyType.GetGenericTypeDefinition()==typeof(Table<,>)||property.PropertyType.GetGenericTypeDefinition()==typeof(Table<>))).ToArray();
    public void Dispose(){if(this.disposed){return;}this.disposed=true;try{this.Database?.Dispose();}finally{try{this.Context.Unload();}finally{this.Compiled.Dispose();}}}
}

internal sealed partial class NativeRuntime:IDisposable{
    private readonly ManagementStore store;
    private readonly RuntimeCompiler compiler=new();
    private readonly ConcurrentDictionary<string,DatabaseInstance> databases=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string,SemaphoreSlim> gates=new(StringComparer.Ordinal);
    internal Action? BeforeQueryAdmission{get;set;}
    internal Action? BeforeQueryExecution{get;set;}
    internal Action? AfterQueryRow{get;set;}
    internal WeakReference? LastQueryContext{get;private set;}
    private readonly TableStorage tableStorage;
    internal NativeRuntime(ManagementStore store,bool startAuditWorker=true){this.store=store;this.tableStorage=new(store.Root);if(startAuditWorker){this.StartAuditDelivery();}}
    private SemaphoreSlim Gate(string database)=>this.gates.GetOrAdd(database,_=>new(1,1));
    internal T WithDatabaseIdentity<T>(string id,CancellationToken cancellation,Func<T> action){
        var gate=this.Gate("db:"+id);gate.Wait(cancellation);
        try{return action();}finally{gate.Release();}
    }
    // [段階7] 設定されていれば、DB 定義の下書きは SDK でビルドする(内蔵のコンパイルを使わない)。製品の入口(ManagementApplicationHost)で有効にする。
    internal DraftSdkBuilder? SdkBuilder{get;set;}
    internal CompiledSource Compile(Definition definition){
        if(definition.Kind==DefinitionKind.Database){return this.SdkBuilder?.Build(definition)??this.compiler.Compile(definition.Source);}
        var version=this.DatabaseDefinition(this.store.CurrentDatabase(definition));using var database=this.ReadArtifact(version);
        return this.CompileQuery(definition,database.AssemblyBytes);
    }
    // 登録クエリは公開済みの DB の成果物を参照してビルドする。SDK のビルドが有効なら SDK で作る(段階7(3))。
    internal CompiledSource CompileQuery(Definition query,byte[] database)=>
        this.SdkBuilder?.BuildQuery(query,database)??this.compiler.Compile(query.Source,[database],query.EntryType,query.EntryMember);
    internal AnalysisResult Analyze(Definition definition){
        var references=SourceEditor.PlatformReferences().Cast<Microsoft.CodeAnalysis.MetadataReference>().ToList();
        if(definition.Kind==DefinitionKind.Query){using var database=this.ReadArtifact(this.DatabaseDefinition(this.store.CurrentDatabase(definition)));references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromImage(database.AssemblyBytes));}
        var projection=new SourceEditor(references).Analyze(definition.Source);var build=this.Compile(definition).Result;
        return projection with{Diagnostics=build.Diagnostics,Current=build.Diagnostics.All(item=>item.Severity!="Error")};
    }
    private DefinitionVersion DatabaseDefinition(string database)=>this.store.DatabaseDefinition(database);
    internal string[] Dependencies(Definition definition){
        if(definition.Kind==DefinitionKind.Query){var database=this.DatabaseDefinition(this.store.CurrentDatabase(definition));return [database.Definition.Id+"@"+database.Version+":"+database.ArtifactHash];}
        var result=new List<string>();long cursor=0;do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Database==this.store.CurrentDatabase(definition)&&item.Definition.Kind==DefinitionKind.Query);result.AddRange(page.Items.Select(item=>item.Definition.Id+"@"+item.Version));cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);return result.ToArray();
    }
    internal DiagnosticInfo[] ValidateDependents(Definition definition,CompiledSource compiled,List<ConfirmedQueryBuild>? confirmed=null){
        if(definition.Kind!=DefinitionKind.Database||compiled.AssemblyBytes.Length==0){return [];}
        var diagnostics=new List<DiagnosticInfo>();long cursor=0;do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Database==this.store.CurrentDatabase(definition)&&item.Definition.Kind==DefinitionKind.Query);foreach(var query in page.Items){
            using var result=this.CompileQuery(query.Definition,compiled.AssemblyBytes);
            diagnostics.AddRange(result.Result.Diagnostics.Select(item=>item with{Message=query.Definition.Id+": "+item.Message}));
            if(confirmed is not null&&result.ImportedBuild is{} build){confirmed.Add(new(query.Definition.Id,query.Version,build.Build,result.AuditMetadata));}
        }cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);return diagnostics.ToArray();
    }
    internal string[] DescribeChanges(DefinitionVersion? previousVersion,Definition after,CompiledSource selected,string? placementDatabase=null){
        var before=previousVersion?.Definition;
        if(after.Kind==DefinitionKind.Database&&selected.AssemblyBytes.Length>0){var context=new ManagedLoadContext(imported:selected.ImportedBuild);try{using var stream=new MemoryStream(selected.AssemblyBytes);DatabaseContainer.ValidateRelationDeclarations(context.LoadFromStream(stream).GetType(after.EntryType,true)!);}catch(Exception error){throw new ManagementException("ForeignKeyDefinition",error.Message);}finally{context.Unload();}}
        var changes=new List<string>();if(before is null){changes.Add("新しい"+after.Kind+"定義を公開します。");}
        if(after.Kind==DefinitionKind.Database&&selected.AssemblyBytes.Length>0){
            var placements=this.PlanTableStorage(previousVersion,after,selected,placementDatabase);
            changes.AddRange(placements.Select(item=>"テーブルの保存先 "+item.Key+" → "+item.Value));
        }
        changes.Add("入口: "+after.EntryType+"."+after.EntryMember+" / 更新取引: "+after.WritesData);
        if(before is not null&&after.Kind==DefinitionKind.Database){using var old=this.ReadArtifact(previousVersion!);var next=selected;if(old.AssemblyBytes.Length>0&&next.AssemblyBytes.Length>0){var previousContext=new ManagedLoadContext(imported:old.ImportedBuild);var nextContext=new ManagedLoadContext(imported:next.ImportedBuild);try{using var previousStream=new MemoryStream(old.AssemblyBytes);using var nextStream=new MemoryStream(next.AssemblyBytes);var previousType=previousContext.LoadFromStream(previousStream).GetType(before.EntryType,true)!;var nextType=nextContext.LoadFromStream(nextStream).GetType(after.EntryType,true)!;changes.AddRange(TypedMigration.Describe(previousType,nextType,after.Migration));_ = TypedMigration.Compile(previousType,nextType,old.AssemblyBytes,next.AssemblyBytes,after.Migration,old.ImportedBuild);}finally{previousContext.Unload();nextContext.Unload();}}changes.Add("稼働版を保持したまま非公開世代へ行を逐次移行し、制約を検証してから公開します。");}
        return changes.ToArray();
    }
    private string Full(string relative){
        var root=System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(this.store.Root));
        var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));
        var normalized=System.IO.Path.GetRelativePath(root,path);
        if(System.IO.Path.IsPathRooted(normalized)||normalized==".."||normalized.StartsWith(".."+System.IO.Path.DirectorySeparatorChar,StringComparison.Ordinal)){
            throw new InvalidDataException("保存先が管理領域を外れています。");
        }
        var parts=normalized.Split(System.IO.Path.DirectorySeparatorChar);
        if(parts is ["generations",_,_,..]){
            var generation=ManagementPlacement.GenerationPath(root,System.IO.Path.Combine(parts[..3]));
            return System.IO.Path.Combine([generation,..parts[3..]]);
        }
        return path;
    }
    private Dictionary<string,string> PlanTableStorage(DefinitionVersion? old,Definition definition,CompiledSource compiled,string? placementDatabase){
        var database=placementDatabase??this.store.CurrentDatabase(definition);
        var previous=old is null?null:TableStorage.Read(this.Full(old.StoragePath));
        if(previous is null&&!this.tableStorage.HasConfiguration(database)){return new(StringComparer.Ordinal);}
        using var module=new MemoryStream(compiled.AssemblyBytes,false);
        var context=new ManagedLoadContext(imported:compiled.ImportedBuild);
        try{
            var type=context.LoadFromStream(module).GetType(definition.EntryType,true)!;
            if(old is null){return this.tableStorage.Plan(database,definition,type,null,null);}
            using var prior=this.ReadArtifact(old);
            using var priorModule=new MemoryStream(prior.AssemblyBytes,false);
            var priorContext=new ManagedLoadContext(imported:prior.ImportedBuild);
            try{return this.tableStorage.Plan(database,definition,type,priorContext.LoadFromStream(priorModule).GetType(old.Definition.EntryType,true)!,previous);}
            finally{priorContext.Unload();}
        }finally{context.Unload();}
    }
    private PreparedDefinition Save(Definition definition,CompiledSource compiled,long expectedVersion,string? databaseArtifact=null,bool auditWrites=true,IReadOnlyDictionary<string,string>? placements=null,string? publicationEntry=null){
        if(compiled.AssemblyBytes.Length==0){
            var errors=compiled.Result.Diagnostics.Where(item=>item.Severity=="Error").Select(item=>{
                var location="";
                if(item.Path is not null){location=item.Path+" ";}
                return location+item.Code+": "+item.Message;
            });
            throw new ManagementException("Validation",string.Join("\n",errors));
        }
        var relative=System.IO.Path.Combine("generations",Guid.NewGuid().ToString("N"));
        var manifestDefinition=definition;
        string? sourceName=null,moduleName=null;
        if(definition.Kind==DefinitionKind.Database&&compiled.ImportedBuild?.Build.PdbSha256==""&&definition.Source.Files is [var source]&&
            definition.EntryType.EndsWith(".Database",StringComparison.Ordinal)&&DatabaseDesigner.IsManagedDatabaseName(definition.EntryType[..^9])){
            var name=definition.EntryType[..^9];
            if(source.Path==name+".cs"){
                sourceName=source.Path;moduleName=name+".dll";
                manifestDefinition=definition with{Source=definition.Source with{Files=[source with{Text=""}]}};
            }
        }
        string? pending=null;
        if(publicationEntry is not null){
            relative=System.IO.Path.Combine("generations",publicationEntry,ManagementPlacement.VersionDirectory(checked(expectedVersion+1)));
            pending=System.IO.Path.Combine("generations",publicationEntry,".pending-"+Guid.NewGuid().ToString("N"));
        }
        string directory;
        lock(this.store.Gate){
            var final=this.Full(relative);SourceWorkspace.CheckLinks(final);
            if(Directory.Exists(final)||File.Exists(final)){throw new ManagementException("Conflict","公開先が既にあります。"+final);}
            directory=this.Full(pending??relative);SourceWorkspace.CheckLinks(directory);
            Directory.CreateDirectory(directory);
        }
        if(sourceName is not null){
            using var file=new FileStream(System.IO.Path.Combine(directory,sourceName),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            file.Write(System.Text.Encoding.UTF8.GetBytes(definition.Source.Files[0].Text));file.Flush(true);
        }
        if(compiled.ImportedBuild is{} imported){SourceBuildArtifactStore.WriteGeneration(imported,directory,moduleName);}
        else{using var file=new FileStream(System.IO.Path.Combine(directory,"module.dll"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);file.Write(compiled.AssemblyBytes);file.Flush(true);}
        string? consumer=null;Guid? destination=null;if(auditWrites&&definition.Kind==DefinitionKind.Database&&this.store.HasAudit){consumer="audit-v1";destination=this.store.InstanceId;}
        var storageId=placements is null?null:TableStorage.Allocate(directory,placements);
        using(var file=new FileStream(System.IO.Path.Combine(directory,"manifest.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){JsonSerializer.Serialize(file,new ArtifactManifest(manifestDefinition,compiled.Result.ArtifactHash,databaseArtifact,consumer,destination){ImportedBuild=compiled.ImportedBuild?.Build,SourceFile=sourceName,TableStorageId=storageId,PublishedVersion=publicationEntry is null&&placements is null?null:checked(expectedVersion+1)},ManagementJson.Options);if(file.Length>64L<<20){throw new ManagementException("Limit","ビルドのmanifestは64MiB以下にしてください。");}file.Flush(true);}
        this.store.Write(()=>{this.store.Put("build",compiled.Result.ArtifactHash,compiled.Result);if(compiled.AuditMetadata is{} audit){QueryAuditMetadataBuilder.Validate(audit,definition.Source.Hash(),compiled.Result.ArtifactHash);this.store.Put("queryAudit",compiled.Result.ArtifactHash,audit);}});
        return new(definition,expectedVersion,compiled.Result.ArtifactHash,relative){PendingPath=pending,Database=this.store.CurrentDatabase(definition)};
    }
    internal CompiledSource ReadArtifact(DefinitionVersion version){
        this.store.RequirePlacement(version);
        var path=this.Full(version.StoragePath);SourceWorkspace.CheckLinks(path);
        var manifestPath=System.IO.Path.Combine(path,"manifest.json");SourceWorkspace.CheckLinks(manifestPath);
        using var manifest=File.OpenRead(manifestPath);if(manifest.Length>64L<<20){throw new ManagementException("Limit","ビルドのmanifestは64MiB以下にしてください。");}
        var identity=JsonSerializer.Deserialize<ArtifactManifest>(manifest,ManagementJson.Options)??throw new InvalidDataException("ビルド情報がありません。");
        if(identity.ReadDefinition(path).Hash()!=version.Definition.Hash()||identity.Sha256!=version.ArtifactHash
            ||identity.PublishedVersion is{} number&&number!=version.Version){throw new InvalidDataException("版とビルドの識別情報が一致しません。");}
        if(identity.MetadataConsumer is not null&&(identity.MetadataConsumer!="audit-v1"||identity.AuditDestination!=this.store.InstanceId||identity.Definition.Kind!=DefinitionKind.Database)||identity.MetadataConsumer is null&&identity.AuditDestination is not null){throw new InvalidDataException("監査プロファイルの転記先が一致しません。");}
        var build=this.store.Read<BuildResult>("build",version.ArtifactHash);if(build is not null&&(build.SourceHash!=version.Definition.Source.Hash()||build.ArtifactHash!=version.ArtifactHash)){throw new InvalidDataException("保存された計画の識別情報が一致しません。");}
        if(identity.ImportedBuild is{} imported){
            if(imported.DefinitionHash!=version.Definition.Hash()||imported.SourceHash!=version.Definition.Source.Hash()||imported.BundleHash!=version.ArtifactHash){throw new InvalidDataException("版と取り込み済み成果物が一致しません。");}
            return FromImported(SourceBuildArtifactStore.ReadGeneration(imported,path,System.IO.Path.ChangeExtension(identity.SourceFile,".dll")),build);
        }
        var bytes=File.ReadAllBytes(System.IO.Path.Combine(path,"module.dll"));if(Convert.ToHexString(SHA256.HashData(bytes))!=version.ArtifactHash){throw new InvalidDataException("公開済みDLLのハッシュが一致しません。");}
        if(build is not null){return new(bytes,build);}
        using var pe=new MemoryStream(bytes);return new(bytes,new(version.Definition.Source.Hash(),version.ArtifactHash,[],ExecutionPlanDocument.Read(pe)));
    }
    internal string? PlacementError(DefinitionVersion version){
        try{
            this.store.RequirePlacement(version);
            var generation=this.Full(version.StoragePath);
            if(!Directory.Exists(generation)){return "公開世代がありません。"+generation;}
            var manifestPath=System.IO.Path.Combine(generation,"manifest.json");
            SourceWorkspace.CheckLinks(manifestPath);
            using var file=File.OpenRead(manifestPath);
            if(file.Length>64L<<20){throw new InvalidDataException("公開世代の記録が大きすぎます。"+manifestPath);}
            var manifest=JsonSerializer.Deserialize<ArtifactManifest>(file,ManagementJson.Options);
            if(manifest is null||manifest.Definition.Id!=version.Definition.Id||manifest.Sha256!=version.ArtifactHash
                ||manifest.PublishedVersion is{} number&&number!=version.Version){
                return "公開世代の識別情報が一致しません。"+manifestPath;
            }
            ManagementPlacement.CheckData(generation);
            return null;
        }catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException or System.Runtime.InteropServices.COMException or ManagementException){
            return error.Message;
        }
    }
    internal static CompiledSource FromImported(CapturedSourceBuild imported,BuildResult? build=null){
        try{
            using var stream=new MemoryStream(imported.AssemblyBytes,false);
            build??=new(imported.Build.SourceHash,imported.Build.BundleHash,[],ExecutionPlanDocument.Read(stream));
            return new([],build){ImportedBuild=imported};
        }catch{imported.Dispose();throw;}
    }
    private DatabaseInstance Open(DefinitionVersion version){
        this.store.RequirePlacement(version);
        ManagementPlacement.CheckData(this.Full(version.StoragePath));
        var path=this.Full(System.IO.Path.Combine(version.StoragePath,"data"));
        if(this.databases.TryGetValue(version.Definition.Id,out var cached)&&(cached.Path!=path||cached.Compiled.Result.ArtifactHash!=version.ArtifactHash)){if(this.databases.TryRemove(version.Definition.Id,out var stale)){stale.Dispose();}}
        return this.databases.GetOrAdd(version.Definition.Id,_=>{var instance=new DatabaseInstance(version.Definition,path,this.ReadArtifact(version));try{instance.Open();this.AuditOriginOpened(version,instance);return instance;}catch{instance.Dispose();throw;}});
    }
    internal void Publish(Definition definition,CancellationToken cancellationToken,Action<PreparedPublication> commit,string? backupPath=null,Guid? enableAuditGeneration=null,Action<string,long>? migrationCheckpoint=null,CompiledSource? selected=null,Action<DatabaseInstance,CancellationToken>? fill=null,Action<DatabaseInstance,CancellationToken>? verifyReopened=null,ConfirmedQueryBuild[]? confirmedQueries=null,string? placementDatabase=null){
        var databaseId=definition.Id;if(definition.Kind==DefinitionKind.Query){databaseId=this.DatabaseDefinition(this.store.CurrentDatabase(definition)).Definition.Id;}var gate=this.Gate("db:"+databaseId);gate.Wait(cancellationToken);
        try{
            if(selected is not null&&(selected.ImportedBuild is null||selected.ImportedBuild.Build.DefinitionHash!=definition.Hash()||selected.Result.SourceHash!=definition.Source.Hash())){throw new ManagementException("Conflict","適用定義と選択済み成果物が一致しません。");}
            var old=this.store.Read<DefinitionVersion>("active",definition.Id);CompiledSource compiled;
            if(enableAuditGeneration is{} auditGeneration){if(selected is not null||!this.store.HasAudit||definition.Kind!=DefinitionKind.Database||old is null||old.Definition.Hash()!=definition.Hash()){throw new ManagementException("Conflict","監査導入は現在のDB定義をそのまま使用してください。");}compiled=this.compiler.CompileForGeneration(definition.Source,auditGeneration);}else{compiled=selected?.Retain()??this.Compile(definition);}
            using var owned=compiled;cancellationToken.ThrowIfCancellationRequested();
            if(backupPath is not null&&old is not null){this.BackupGeneration(old,backupPath,cancellationToken);}
            if(definition.Kind==DefinitionKind.Query){var db=this.DatabaseDefinition(this.store.CurrentDatabase(definition));var prepared=this.Save(definition,compiled,old?.Version??0,db.ArtifactHash);commit(new(prepared,[]));return;}
            var auditWrites=true;if(old is not null){using var previousManifest=File.OpenRead(this.Full(System.IO.Path.Combine(old.StoragePath,"manifest.json")));auditWrites=JsonSerializer.Deserialize<ArtifactManifest>(previousManifest,ManagementJson.Options)?.MetadataConsumer=="audit-v1";}
            if(enableAuditGeneration is not null){auditWrites=true;}
            var placements=compiled.AssemblyBytes.Length==0?null:this.PlanTableStorage(old,definition,compiled,placementDatabase);
            var entry=placementDatabase??this.store.CurrentDatabase(definition);
            var named=ManagementPlacement.IsEntryName(entry)&&(compiled.ImportedBuild is not null||old is not null&&ManagementStore.TryNamedGeneration(old.StoragePath,out _,out _));
            var candidate=this.Save(definition,compiled,old?.Version??0,auditWrites:auditWrites,placements:placements,publicationEntry:named?entry:null);
            var workingPath=candidate.PendingPath??candidate.StoragePath;
            if(old is not null){ManagementPlacement.InheritData(this.Full(old.StoragePath),this.Full(workingPath));}
            var instance=new DatabaseInstance(definition,this.Full(System.IO.Path.Combine(workingPath,"data")),compiled.Retain());
            try{
                // 外部DBの取り込み(fill)は、古い世代から写さずに、取り込み元の行で新しい世代を作る(置き換えるときの古い世代はバックアップに残す)。
                instance.Open();if(fill is not null){fill(instance,cancellationToken);}else if(old is not null){if(enableAuditGeneration is not null){TypedMigration.Copy(this.Open(old),instance,cancellationToken,migration:null,checkpoint:migrationCheckpoint);}else{this.Copy(this.Open(old),instance,cancellationToken);}CopyDirectSets(this.Open(old).Database,instance.Database,cancellationToken);}
                var rebuilt=new List<PreparedDefinition>();long cursor=0;
                do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Database==this.store.CurrentDatabase(definition)&&item.Definition.Kind==DefinitionKind.Query);
                    foreach(var query in page.Items){
                        cancellationToken.ThrowIfCancellationRequested();
                        using var result=confirmedQueries is null
                            ?this.CompileQuery(query.Definition,compiled.AssemblyBytes)
                            :this.ReadConfirmedQuery(query,confirmedQueries,cancellationToken);
                        rebuilt.Add(this.Save(query.Definition,result,query.Version,candidate.ArtifactHash));
                    }
                    cursor=page.NextCursor;if(!page.HasMore){break;}
                }while(true);
                instance.Dispose();instance=new(definition,this.Full(System.IO.Path.Combine(workingPath,"data")),compiled.Retain());instance.Open();cancellationToken.ThrowIfCancellationRequested();
                verifyReopened?.Invoke(instance,cancellationToken);
                ManagementPlacement.SealData(this.Full(workingPath));
                candidate=candidate with{AuditOrigin=this.CreateAuditOrigin(candidate,instance)};
                if(candidate.PendingPath is not null){instance.Dispose();instance=null!;}
                commit(new(candidate,rebuilt.ToArray()));
                if(candidate.PendingPath is not null){
                    if(this.databases.TryRemove(definition.Id,out var obsolete)){obsolete.Dispose();}
                    return;
                }
                if(this.databases.TryRemove(definition.Id,out var previous)){previous.Dispose();}this.databases[definition.Id]=instance;instance=null!;
            }finally{instance?.Dispose();}
        }finally{gate.Release();}
    }
    private CompiledSource ReadConfirmedQuery(DefinitionVersion query,ConfirmedQueryBuild[] confirmed,CancellationToken cancellationToken){
        var selected=confirmed.SingleOrDefault(item=>item.Id==query.Definition.Id&&item.Version==query.Version);
        if(selected is null||selected.Build.DefinitionHash!=query.Definition.Hash()||selected.Build.SourceHash!=query.Definition.Source.Hash()){
            throw new ManagementException("Conflict","登録クエリの変更影響を再確認してください: "+query.Definition.Id);
        }
        return FromImported(this.store.SourceBuilds.Read(selected.Build,cancellationToken)) with{AuditMetadata=selected.Audit};
    }
    private void Copy(DatabaseInstance source,DatabaseInstance target,CancellationToken cancellationToken)=>TypedMigration.Copy(source,target,cancellationToken,target.Definition.Migration);
    // 取引を通さない表(Set<T>)を写す。新旧の両方に同じ経路の宣言がある表だけを、行を JSON にして新しい行の型へ読み直して足す。
    // 新しい定義から消えた表は写さない。行の形が合わなければ公開を止める(黙って欠けさせない)。
    private static void CopyDirectSets(DatabaseContainer before,DatabaseContainer after,CancellationToken cancellation){
        var targets=DatabaseTableDeclaration.ReadDirectSets(after.GetType()).ToDictionary(item=>item.Path,StringComparer.Ordinal);
        foreach(var source in DatabaseTableDeclaration.ReadDirectSets(before.GetType())){
            if(!targets.TryGetValue(source.Path,out var target)){continue;}
            var type=target.Property.PropertyType;
            var method=type.GetGenericTypeDefinition()==typeof(Set<,>)?nameof(CopyIntoKeyedSet):nameof(CopyIntoKeylessSet);
            var copy=typeof(NativeRuntime).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.MakeGenericMethod(type.GenericTypeArguments);
            try{copy.Invoke(null,[source.Path,(System.Collections.IEnumerable)source.GetTable(before),target.GetTable(after),cancellation]);}
            catch(TargetInvocationException error) when(error.InnerException is not null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();}
        }
    }
    private static void CopyIntoKeyedSet<TKey,TRecord>(string path,System.Collections.IEnumerable rows,object target,CancellationToken cancellation) where TRecord:global::Seamless.Collections.IRecord<TKey>{
        var set=(Set<TKey,TRecord>)target;
        foreach(var row in rows){cancellation.ThrowIfCancellationRequested();if(!set.Add((TRecord)ReadDirectSetRow(path,row,typeof(TRecord)))){throw new ManagementException("Validation","取引を通さない表を写すときに主キーが重複しました: "+path);}}
    }
    private static void CopyIntoKeylessSet<TRecord>(string path,System.Collections.IEnumerable rows,object target,CancellationToken cancellation) where TRecord:notnull{
        var set=(Set<TRecord>)target;
        foreach(var row in rows){cancellation.ThrowIfCancellationRequested();set.Add((TRecord)ReadDirectSetRow(path,row,typeof(TRecord)));}
    }
    private static object ReadDirectSetRow(string path,object? row,Type type){
        try{return RuntimeValueJson.Deserialize(RuntimeValueJson.Serialize(row),type)??throw new InvalidDataException("null");}
        catch(Exception error) when(error is not OperationCanceledException and not ManagementException){throw new ManagementException("Validation","取引を通さない表の行の形が新しい定義と合わないため、写せません: "+path+"（"+error.Message+"）");}
    }
    internal void BackupGeneration(DefinitionVersion version,string destination,CancellationToken cancellation){
        var generation=this.Full(version.StoragePath);
        if(version.Definition.Kind==DefinitionKind.Database){ManagementPlacement.CheckData(generation);}
        if(this.databases.TryRemove(version.Definition.Id,out var active)){active.Dispose();}
        var backups=System.IO.Path.Combine(this.store.Root,"backups");
        var relative=System.IO.Path.GetRelativePath(backups,destination);
        if(!System.IO.Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+System.IO.Path.DirectorySeparatorChar,StringComparison.Ordinal)){
            destination=System.IO.Path.Combine(ManagementPlacement.DirectoryPath(backups,allowShortcut:true),relative);
        }
        CopyGenerationDirectory(generation,destination,cancellation);
        File.WriteAllText(System.IO.Path.Combine(destination,"definition-version.json"),JsonSerializer.Serialize(version,ManagementJson.Options));
    }
    internal static void CopyGenerationDirectory(string source,string destination,CancellationToken cancellation,bool bundleTables=true){
        var relative=System.IO.Path.GetRelativePath(source,destination);
        if(!System.IO.Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+System.IO.Path.DirectorySeparatorChar,StringComparison.Ordinal)){
            throw new IOException("バックアップ先はコピー元から独立した場所を指定してください。");
        }
        if(Directory.Exists(destination)||File.Exists(destination)){throw new IOException("コピー先は未使用の場所を指定してください。");}
        SourceWorkspace.CheckLinks(destination);
        Directory.CreateDirectory(destination);
        void CopyDirectory(string from,string to,string area){
            foreach(var entry in Directory.EnumerateFileSystemEntries(from)){
                cancellation.ThrowIfCancellationRequested();
                var name=System.IO.Path.GetFileName(entry);
                var allowed=bundleTables&&(area==""&&name=="data"||area=="data"&&name is "store-v2" or "wal-v2");
                var actual=allowed?ManagementPlacement.DirectoryPath(entry):entry;
                if(allowed&&ManagementPlacement.Overlaps(actual,destination)){
                    throw new IOException("バックアップ先はリンク先から独立した場所を指定してください。"+actual);
                }
                var attributes=File.GetAttributes(actual);
                if((attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("バックアップ対象に未対応のリンクがあります。"+entry);}
                var target=System.IO.Path.Combine(to,name);
                if((attributes&FileAttributes.Directory)!=0){Directory.CreateDirectory(target);CopyDirectory(actual,target,area.Length==0?name:area+"/"+name);}
                else{File.Copy(actual,target,false);}
            }
        }
        SourceWorkspace.CheckLinks(source);
        CopyDirectory(source,destination,"");
        if(bundleTables){TableStorage.Bundle(source,destination,cancellation);}
    }
    internal ValidationDatabase CreateValidation(DefinitionVersion version,string owner){
        if(version.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("Validation","DB定義を選択してください。");}
        using var compiled=this.ReadArtifact(version);
        return this.CreateValidation(version.Definition,new(Guid.NewGuid().ToString("N"),version.Database,version.Definition.Id,version.Version,owner){BundleHash=compiled.ImportedBuild?.Build.BundleHash},compiled);
    }
    internal ValidationDatabase CreateValidation(Draft draft,string owner){
        using var compiled=this.Compile(draft.Definition);return this.CreateValidation(draft,owner,compiled);
    }
    internal ValidationDatabase CreateValidation(Draft draft,string owner,CompiledSource compiled){
        if(draft.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("Validation","DB定義の下書きを選択してください。");}
        return this.CreateValidation(draft.Definition,new(Guid.NewGuid().ToString("N"),this.store.CurrentDatabase(draft.Definition),draft.Definition.Id,draft.Revision,owner,draft.Definition.Source.Hash(),true){BundleHash=compiled.ImportedBuild?.Build.BundleHash},compiled);
    }
    private ValidationDatabase CreateValidation(Definition definition,ValidationDatabase validation,CompiledSource compiled){
        var prepared=this.Save(definition,compiled,validation.SourceVersion,auditWrites:false);
        using(var instance=new DatabaseInstance(definition,this.Full(System.IO.Path.Combine(prepared.StoragePath,"data")),compiled)){instance.Open();}
        this.store.Write(()=>{
            this.store.Put("validation",validation.Id,validation);
            this.store.Put("validationVersion",validation.Id,new DefinitionVersion(definition,validation.SourceVersion,validation.Owner,DateTimeOffset.UtcNow,prepared.ArtifactHash,prepared.StoragePath){Database=validation.Database});
        });return validation;
    }
    internal BuildResult PublishedPlans(DefinitionVersion version){using var compiled=this.ReadArtifact(version);return compiled.Result;}
    private (ValidationDatabase Validation,DefinitionVersion Version) Validation(string id,UserAccount user){var validation=this.store.Read<ValidationDatabase>("validation",id)??throw new ManagementException("NotFound","検証DBがありません。");if(validation.Deleted){throw new ManagementException("NotFound","検証DBは破棄済みです。");}if(ManagementStore.Normalize(validation.Owner)!=ManagementStore.Normalize(user.Name)||!ManagementService.Can(user,validation.Database,ManagementPermission.Validate)){throw new ManagementException("Permission","検証DBの所有者または権限が一致しません。");}return(validation,this.store.Read<DefinitionVersion>("validationVersion",id)!);}
    internal void Seed(SeedRequest request,UserAccount user)=>this.SeedBatch(new(request.ValidationDatabaseId,[new(request.Table,request.Rows)]),user);
    internal void SeedBatch(SeedBatchRequest request,UserAccount user){
        var (_,version)=this.Validation(request.ValidationDatabaseId,user);if(request.Tables.Sum(table=>(long)table.Rows.Length)>1000){throw new ManagementException("InvalidRequest","投入は合計1000行以下に分割してください。");}
        var gate=this.Gate("validation:"+request.ValidationDatabaseId);gate.Wait();try{
            _=this.Validation(request.ValidationDatabaseId,user);using var instance=new DatabaseInstance(version.Definition,this.Full(System.IO.Path.Combine(version.StoragePath,"data")),this.ReadArtifact(version));instance.Open();
            var declarations=DatabaseTableDeclaration.Read(instance.Database.GetType());using var tx=instance.Database.BeginTransaction();
            foreach(var input in request.Tables){var declaration=declarations.SingleOrDefault(table=>table.Path==input.Table)??throw new ManagementException("NotFound","テーブルがありません: "+input.Table);var table=declaration.GetTable(instance.Database);var type=declaration.Property.PropertyType.GetGenericArguments()[^1];var add=table.GetType().GetMethod("Add",[type])!;
                foreach(var row in input.Rows){if(add.Invoke(table,[Materialize(row,type)]) is false){throw new ManagementException("Constraint","キーが重複しています。");}}
            }tx.Commit();
        }finally{gate.Release();}
    }
    internal void DropValidation(string id,UserAccount user){
        var gate=this.Gate("validation:"+id);gate.Wait();try{
            var validation=this.store.Read<ValidationDatabase>("validation",id)??throw new ManagementException("NotFound","検証DBがありません。");if(ManagementStore.Normalize(validation.Owner)!=ManagementStore.Normalize(user.Name)||!ManagementService.Can(user,validation.Database,ManagementPermission.Validate)){throw new ManagementException("Permission","検証DBの所有者または権限が一致しません。");}
            var version=this.store.Read<DefinitionVersion>("validationVersion",id)??throw new ManagementException("RecoveryRequired","検証DBの版情報がありません。");var path=this.Full(version.StoragePath);
            if(this.store.List<DefinitionVersion>("active",predicate:item=>item.StoragePath==version.StoragePath).Items.Length>0){throw new ManagementException("Conflict","稼働版と共有する保存先は破棄できません。");}
            // 再起動後も破棄済みを再オープンしないよう、所有する検証領域だけを先に無効化する。
            this.store.Write(()=>{var latest=this.store.Read<ValidationDatabase>("validation",id)??throw new ManagementException("NotFound","検証DBがありません。");this.store.Put("validation",id,latest with{Deleted=true});this.store.AddHistory(user.Name,latest.Database,"ValidationDiscarded",id,latest.SourceVersion);});
            if(Directory.Exists(path)){var pending=new Queue<string>();pending.Enqueue(path);while(pending.TryDequeue(out var current)){if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む検証領域はCLIで確認してください。");}foreach(var entry in Directory.EnumerateFileSystemEntries(current)){var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む検証領域はCLIで確認してください。");}if((attributes&FileAttributes.Directory)!=0){pending.Enqueue(entry);}}}Directory.Delete(path,true);}
        }finally{gate.Release();}
    }
    internal IEnumerable<ExecutionRow> Execute(Definition definition,ExecuteRequest request,UserAccount user,CancellationToken cancellationToken,Action? beforeCompletion=null)=>this.ExecuteCore(definition,request,user,cancellationToken,beforeCompletion,null);
    private IEnumerable<ExecutionRow> ExecuteCore(Definition definition,ExecuteRequest request,UserAccount user,CancellationToken cancellationToken,Action? beforeCompletion,FunctionExecutionCapture? capture){
        var started=DateTimeOffset.UtcNow;
        if(definition.Kind!=DefinitionKind.Query||request.MaximumRows is <1 or >10000){throw new ManagementException("Validation","問い合わせと1〜10000行の上限を指定してください。");}
        DatabaseInstance? temporary=null;DefinitionVersion version;var gateKey="";var databaseName=this.store.CurrentDatabase(definition);
        if(request.ValidationDatabaseId is not null){var validation=this.Validation(request.ValidationDatabaseId,user);if(validation.Validation.Database!=databaseName){throw new ManagementException("Permission","検証DBの接続先が異なります。");}version=validation.Version;gateKey="validation:"+request.ValidationDatabaseId;}
        else{version=this.DatabaseDefinition(databaseName);gateKey="db:"+version.Definition.Id;}
        if(request.ExpectedDatabaseDefinitionId is{} expectedDatabase&&version.Definition.Id!=expectedDatabase){throw new ManagementException("CatalogChanged","関数の所属DBが変わりました。");}
        var gate=this.Gate(gateKey);capture?.Begin();this.BeforeQueryAdmission?.Invoke();capture?.WaitingForGate();gate.Wait(cancellationToken);ManagedLoadContext? context=null;CompiledSource? loaded=null;ReadAuditObservation? completedRead=null;
        ManagementExecutionContext? execution=null;
        try{
            capture?.EnteredGate();
            if(request.ValidationDatabaseId is null){version=this.store.Read<DefinitionVersion>("active",version.Definition.Id)??throw new ManagementException("NotFound","DBがありません。");}
            else{version=this.Validation(request.ValidationDatabaseId,user).Version;}
            DatabaseInstance database;if(request.ValidationDatabaseId is null){database=this.Open(version);}else{temporary=new(version.Definition,this.Full(System.IO.Path.Combine(version.StoragePath,"data")),this.ReadArtifact(version));temporary.Open();database=temporary;}
            CompiledSource query;if(request.DraftRevision.HasValue){query=this.CompileQuery(definition,database.Compiled.AssemblyBytes);loaded=query;}else{var published=this.store.Read<DefinitionVersion>("active",definition.Id)??throw new ManagementException("NotFound","問い合わせがありません。");if(request.ExpectedVersion is{} expectedVersion&&published.Version!=expectedVersion||request.ExpectedArtifactHash is{} expectedArtifact&&published.ArtifactHash!=expectedArtifact){throw new ManagementException("CatalogChanged","関数の公開版または成果物が変わりました。");}definition=published.Definition;query=this.ReadArtifact(published);loaded=query;capture?.Bound(published);var manifest=JsonSerializer.Deserialize<ArtifactManifest>(File.ReadAllText(this.Full(System.IO.Path.Combine(published.StoragePath,"manifest.json"))),ManagementJson.Options)!;if(manifest.DatabaseArtifact!=version.ArtifactHash){throw new ManagementException("SchemaMismatch","問い合わせの参照世代が異なります。");}}
            if(query.AssemblyBytes.Length==0){throw new ManagementException("Validation",string.Join("\n",query.Result.Diagnostics.Select(item=>item.Message)));}
            context=new(database.Assembly);this.LastQueryContext=new(context);using var pe=new MemoryStream(query.AssemblyBytes);var assembly=context.LoadFromStream(pe);var entry=assembly.GetType("Seamless.Management.Generated.QueryEntry",false);var type=entry??assembly.GetType(definition.EntryType,true)!;var member=definition.EntryMember;if(entry is not null){member="Invoke";}var method=type.GetMethod(member,BindingFlags.Public|BindingFlags.Static)??throw new ManagementException("Validation","公開されたstatic入口がありません。");
            execution=new ManagementExecutionContext(database.Database,cancellationToken,static()=>new ManagementException("SchemaMismatch","DB型が実行環境と一致しません。"));var arguments=method.GetParameters().Select(parameter=>{if(parameter.ParameterType==typeof(ManagementExecutionContext)){return execution;}return RuntimeValueJson.Deserialize(request.Arguments.GetProperty(parameter.Name!),parameter.ParameterType);}).ToArray();
            // 更新する公開定義だけが取引を開始できる。結果上限・取消・列挙例外では確定しない。
            DatabaseReadSession? readScope=null;DatabaseTransaction? writeScope=null;if(definition.WritesData){writeScope=database.Database.BeginTransaction(cancellationToken);}else{readScope=database.Database.BeginReadSession(cancellationToken);}
            using var read=readScope;using var write=writeScope;
            cancellationToken.ThrowIfCancellationRequested();this.BeforeQueryExecution?.Invoke();capture?.Invoking();var result=method.Invoke(null,arguments);var count=0;
            if(result is IEnumerable rows&&result is not string){
                if(capture is not null){capture.IsSequence=true;}
                var iterator=rows.GetEnumerator();try{
                    while(count<request.MaximumRows&&iterator.MoveNext()){cancellationToken.ThrowIfCancellationRequested();var row=iterator.Current;count++;capture?.Output();var encoded=RuntimeValueJson.Serialize(row);this.AfterQueryRow?.Invoke();yield return new(encoded);}
                    if(count==request.MaximumRows&&(definition.WritesData||capture is not null)&&iterator.MoveNext()){capture?.Output();if(capture is not null){capture.HasMore=true;}if(definition.WritesData){throw new ManagementException("ResultLimit","更新結果が上限を超えたため、取引を取り消しました。");}}
                }finally{(iterator as IDisposable)?.Dispose();}
            }else{capture?.Output();yield return new(RuntimeValueJson.Serialize(result));}
            cancellationToken.ThrowIfCancellationRequested();capture?.BeforeCompletion();beforeCompletion?.Invoke();
            if(write is not null){
                var journal=database.Database.MetadataJournal;
                if(request.ValidationDatabaseId is null&&journal is not null){var item=this.CreateUpdateAudit(definition,query,user,write.MetadataTransactionId,started);write.CommitWithMetadata(AuditWalPayload.Encode(this.store.InstanceId,journal.Device.WalId,item));write.Dispose();this.NotifyAudit(journal.Device.WalId);}
                else{write.Commit();}
            }
            else if(this.ReadCompleted is not null&&request.ValidationDatabaseId is null&&!request.DraftRevision.HasValue){completedRead=new(DateTimeOffset.UtcNow,ManagementStore.Normalize(user.Name),this.store.CurrentDatabase(definition),definition.Id,query.Result.ArtifactHash);}
        }finally{execution?.End();context?.Unload();loaded?.Dispose();temporary?.Dispose();gate.Release();}
        if(completedRead is{} sample){this.ReadCompleted?.Invoke(sample);}
    }
    public void Dispose(){this.StopAuditDelivery();foreach(var item in this.databases.Values){item.Dispose();}this.databases.Clear();}
    private static object Materialize(JsonElement value,Type type)=>RuntimeValueJson.Deserialize(value,type)??throw new ManagementException("Validation","行の値を構築できません。");
}
