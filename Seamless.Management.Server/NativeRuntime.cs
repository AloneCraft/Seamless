using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Database;

namespace Seamless.Management.Server;

internal sealed record PreparedDefinition(Definition Definition,long ExpectedVersion,string ArtifactHash,string StoragePath){public AuditOrigin? AuditOrigin{get;init;}public string Database{get;init;}=Definition.Database;}
internal sealed record PreparedPublication(PreparedDefinition Primary,PreparedDefinition[] Dependents);
internal readonly record struct ReadAuditObservation(DateTimeOffset At,string SubjectId,string Database,string QueryId,string CodeVersion);
internal sealed record ArtifactManifest(Definition Definition,string Sha256,string? DatabaseArtifact,string? MetadataConsumer=null,Guid? AuditDestination=null){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceProjectBuild? ImportedBuild{get;init;}
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
        if(this.Compiled.ImportedBuild is{} imported){using var pdb=new MemoryStream(imported.PdbBytes,false);this.Assembly=this.Context.LoadFromStream(stream,pdb);}
        else{this.Assembly=this.Context.LoadFromStream(stream);}
        var type=this.Assembly.GetType(this.Definition.EntryType,true)!;
        if(!typeof(DatabaseContainer).IsAssignableFrom(type)){throw new ManagementException("Validation","DBの入口はDatabaseContainer派生型にしてください。");}
        var options=DatabaseContainerOptions.OnStorage(this.Path) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=1<<24,TemporaryDirectory=System.IO.Path.Combine(this.Path,"temporary"),MaximumQueryWorkers=Math.Min(8,Environment.ProcessorCount)};
        if(debugOptions is null){using var manifestFile=File.OpenRead(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(this.Path)!,"manifest.json"));var manifest=JsonSerializer.Deserialize<ArtifactManifest>(manifestFile,ManagementJson.Options)??throw new InvalidDataException("DBのmanifestがありません。");if(manifest.Definition.Hash()!=this.Definition.Hash()||manifest.Sha256!=this.Compiled.Result.ArtifactHash||manifest.MetadataConsumer is not null&&(manifest.MetadataConsumer!="audit-v1"||manifest.AuditDestination is null||manifest.AuditDestination==Guid.Empty)||manifest.MetadataConsumer is null&&manifest.AuditDestination is not null){throw new InvalidDataException("DBの監査プロファイルが一致しません。");}options=options with{TransactionMetadataConsumer=manifest.MetadataConsumer};this.AuditDestination=manifest.AuditDestination;}
        this.Database=(DatabaseContainer)(Activator.CreateInstance(type,[debugOptions??options])??throw new ManagementException("Validation","DBコンストラクターが必要です。"));
        foreach(var property in Tables(this.Database)){_=property.GetValue(this.Database);}
    }
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
    internal NativeRuntime(ManagementStore store,bool startAuditWorker=true){this.store=store;if(startAuditWorker){this.StartAuditDelivery();}}
    private SemaphoreSlim Gate(string database)=>this.gates.GetOrAdd(database,_=>new(1,1));
    internal T WithDatabaseIdentity<T>(string id,CancellationToken cancellation,Func<T> action){
        var gate=this.Gate("db:"+id);gate.Wait(cancellation);
        try{return action();}finally{gate.Release();}
    }
    internal CompiledSource Compile(Definition definition){
        if(definition.Kind==DefinitionKind.Database){return this.compiler.Compile(definition.Source);}
        var version=this.DatabaseDefinition(this.store.CurrentDatabase(definition));using var database=this.ReadArtifact(version);
        return this.compiler.Compile(definition.Source,[database.AssemblyBytes],definition.EntryType,definition.EntryMember);
    }
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
    internal DiagnosticInfo[] ValidateDependents(Definition definition,CompiledSource compiled){
        if(definition.Kind!=DefinitionKind.Database||compiled.AssemblyBytes.Length==0){return [];}
        var diagnostics=new List<DiagnosticInfo>();long cursor=0;do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Database==this.store.CurrentDatabase(definition)&&item.Definition.Kind==DefinitionKind.Query);foreach(var query in page.Items){var result=this.compiler.Compile(query.Definition.Source,[compiled.AssemblyBytes],query.Definition.EntryType,query.Definition.EntryMember);diagnostics.AddRange(result.Result.Diagnostics.Select(item=>item with{Message=query.Definition.Id+": "+item.Message}));}cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);return diagnostics.ToArray();
    }
    internal string[] DescribeChanges(DefinitionVersion? previousVersion,Definition after,CompiledSource selected){
        var before=previousVersion?.Definition;
        if(after.Kind==DefinitionKind.Database&&selected.AssemblyBytes.Length>0){var context=new ManagedLoadContext(imported:selected.ImportedBuild);try{using var stream=new MemoryStream(selected.AssemblyBytes);DatabaseContainer.ValidateRelationDeclarations(context.LoadFromStream(stream).GetType(after.EntryType,true)!);}catch(Exception error){throw new ManagementException("ForeignKeyDefinition",error.Message);}finally{context.Unload();}}
        var changes=new List<string>();if(before is null){changes.Add("新しい"+after.Kind+"定義を公開します。");}
        else{foreach(var file in before.Source.Files){var next=after.Source.Files.SingleOrDefault(item=>item.Path==file.Path);if(next is null){changes.Add("ファイルを削除: "+file.Path);}else if(next.Text!=file.Text){changes.Add("ファイルを変更: "+file.Path+"\n--- 変更前\n"+file.Text+"\n+++ 変更後\n"+next.Text);}}foreach(var file in after.Source.Files.Where(item=>before.Source.Files.All(previous=>previous.Path!=item.Path))){changes.Add("ファイルを追加: "+file.Path+"\n"+file.Text);}}
        changes.Add("入口: "+after.EntryType+"."+after.EntryMember+" / 更新取引: "+after.WritesData);
        if(before is not null&&after.Kind==DefinitionKind.Database){using var old=this.ReadArtifact(previousVersion!);var next=selected;if(old.AssemblyBytes.Length>0&&next.AssemblyBytes.Length>0){var previousContext=new ManagedLoadContext(imported:old.ImportedBuild);var nextContext=new ManagedLoadContext(imported:next.ImportedBuild);try{using var previousStream=new MemoryStream(old.AssemblyBytes);using var nextStream=new MemoryStream(next.AssemblyBytes);var previousType=previousContext.LoadFromStream(previousStream).GetType(before.EntryType,true)!;var nextType=nextContext.LoadFromStream(nextStream).GetType(after.EntryType,true)!;changes.AddRange(TypedMigration.Describe(previousType,nextType,after.Migration));_ = TypedMigration.Compile(previousType,nextType,old.AssemblyBytes,next.AssemblyBytes,after.Migration,old.ImportedBuild);}finally{previousContext.Unload();nextContext.Unload();}}changes.Add("稼働版を保持したまま非公開世代へ行を逐次移行し、制約を検証してから公開します。");}
        return changes.ToArray();
    }
    private string Full(string relative){var root=System.IO.Path.GetFullPath(this.store.Root)+System.IO.Path.DirectorySeparatorChar;var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));if(!path.StartsWith(root,StringComparison.Ordinal)){throw new InvalidDataException("保存先が管理領域を外れています。");}return path;}
    private PreparedDefinition Save(Definition definition,CompiledSource compiled,long expectedVersion,string? databaseArtifact=null,bool auditWrites=true){
        if(compiled.AssemblyBytes.Length==0){throw new ManagementException("Validation",string.Join("\n",compiled.Result.Diagnostics.Select(item=>item.Message)));}
        var relative=System.IO.Path.Combine("generations",Guid.NewGuid().ToString("N"));var directory=this.Full(relative);Directory.CreateDirectory(directory);
        if(compiled.ImportedBuild is{} imported){SourceBuildArtifactStore.WriteGeneration(imported,directory);}
        else{using var file=new FileStream(System.IO.Path.Combine(directory,"module.dll"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);file.Write(compiled.AssemblyBytes);file.Flush(true);}
        string? consumer=null;Guid? destination=null;if(auditWrites&&definition.Kind==DefinitionKind.Database&&this.store.HasAudit){consumer="audit-v1";destination=this.store.InstanceId;}
        using(var file=new FileStream(System.IO.Path.Combine(directory,"manifest.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){JsonSerializer.Serialize(file,new ArtifactManifest(definition,compiled.Result.ArtifactHash,databaseArtifact,consumer,destination){ImportedBuild=compiled.ImportedBuild?.Build},ManagementJson.Options);if(file.Length>64L<<20){throw new ManagementException("Limit","ビルドのmanifestは64MiB以下にしてください。");}file.Flush(true);}
        this.store.Write(()=>{this.store.Put("build",compiled.Result.ArtifactHash,compiled.Result);if(compiled.AuditMetadata is{} audit){QueryAuditMetadataBuilder.Validate(audit,definition.Source.Hash(),compiled.Result.ArtifactHash);this.store.Put("queryAudit",compiled.Result.ArtifactHash,audit);}});
        return new(definition,expectedVersion,compiled.Result.ArtifactHash,relative){Database=this.store.CurrentDatabase(definition)};
    }
    internal CompiledSource ReadArtifact(DefinitionVersion version){
        var path=this.Full(version.StoragePath);SourceWorkspace.CheckLinks(path);
        var manifestPath=System.IO.Path.Combine(path,"manifest.json");SourceWorkspace.CheckLinks(manifestPath);
        using var manifest=File.OpenRead(manifestPath);if(manifest.Length>64L<<20){throw new ManagementException("Limit","ビルドのmanifestは64MiB以下にしてください。");}
        var identity=JsonSerializer.Deserialize<ArtifactManifest>(manifest,ManagementJson.Options)??throw new InvalidDataException("ビルド情報がありません。");if(identity.Definition.Hash()!=version.Definition.Hash()||identity.Sha256!=version.ArtifactHash){throw new InvalidDataException("版とビルドの識別情報が一致しません。");}
        if(identity.MetadataConsumer is not null&&(identity.MetadataConsumer!="audit-v1"||identity.AuditDestination!=this.store.InstanceId||identity.Definition.Kind!=DefinitionKind.Database)||identity.MetadataConsumer is null&&identity.AuditDestination is not null){throw new InvalidDataException("監査プロファイルの転記先が一致しません。");}
        var build=this.store.Read<BuildResult>("build",version.ArtifactHash);if(build is not null&&(build.SourceHash!=version.Definition.Source.Hash()||build.ArtifactHash!=version.ArtifactHash)){throw new InvalidDataException("保存された計画の識別情報が一致しません。");}
        if(identity.ImportedBuild is{} imported){
            if(imported.DefinitionHash!=version.Definition.Hash()||imported.SourceHash!=version.Definition.Source.Hash()||imported.BundleHash!=version.ArtifactHash){throw new InvalidDataException("版と取り込み済み成果物が一致しません。");}
            return FromImported(SourceBuildArtifactStore.ReadGeneration(imported,path),build);
        }
        var bytes=File.ReadAllBytes(System.IO.Path.Combine(path,"module.dll"));if(Convert.ToHexString(SHA256.HashData(bytes))!=version.ArtifactHash){throw new InvalidDataException("公開済みDLLのハッシュが一致しません。");}
        if(build is not null){return new(bytes,build);}
        using var pe=new MemoryStream(bytes);return new(bytes,new(version.Definition.Source.Hash(),version.ArtifactHash,[],ExecutionPlanDocument.Read(pe)));
    }
    internal static CompiledSource FromImported(CapturedSourceBuild imported,BuildResult? build=null){
        try{
            using var stream=new MemoryStream(imported.AssemblyBytes,false);
            build??=new(imported.Build.SourceHash,imported.Build.BundleHash,[],ExecutionPlanDocument.Read(stream));
            return new([],build){ImportedBuild=imported};
        }catch{imported.Dispose();throw;}
    }
    private DatabaseInstance Open(DefinitionVersion version){
        var path=this.Full(System.IO.Path.Combine(version.StoragePath,"data"));
        if(this.databases.TryGetValue(version.Definition.Id,out var cached)&&(cached.Path!=path||cached.Compiled.Result.ArtifactHash!=version.ArtifactHash)){if(this.databases.TryRemove(version.Definition.Id,out var stale)){stale.Dispose();}}
        return this.databases.GetOrAdd(version.Definition.Id,_=>{var instance=new DatabaseInstance(version.Definition,path,this.ReadArtifact(version));try{instance.Open();this.AuditOriginOpened(version,instance);return instance;}catch{instance.Dispose();throw;}});
    }
    internal void Publish(Definition definition,CancellationToken cancellationToken,Action<PreparedPublication> commit,string? backupPath=null,Guid? enableAuditGeneration=null,Action<string,long>? migrationCheckpoint=null,CompiledSource? selected=null){
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
            var candidate=this.Save(definition,compiled,old?.Version??0,auditWrites:auditWrites);var instance=new DatabaseInstance(definition,this.Full(System.IO.Path.Combine(candidate.StoragePath,"data")),compiled.Retain());
            try{
                instance.Open();if(old is not null){if(enableAuditGeneration is not null){TypedMigration.Copy(this.Open(old),instance,cancellationToken,migration:null,checkpoint:migrationCheckpoint);}else{this.Copy(this.Open(old),instance,cancellationToken);}}
                var rebuilt=new List<PreparedDefinition>();long cursor=0;
                do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Database==this.store.CurrentDatabase(definition)&&item.Definition.Kind==DefinitionKind.Query);
                    foreach(var query in page.Items){cancellationToken.ThrowIfCancellationRequested();var result=this.compiler.Compile(query.Definition.Source,[compiled.AssemblyBytes],query.Definition.EntryType,query.Definition.EntryMember);rebuilt.Add(this.Save(query.Definition,result,query.Version,candidate.ArtifactHash));}
                    cursor=page.NextCursor;if(!page.HasMore){break;}
                }while(true);
                instance.Dispose();instance=new(definition,this.Full(System.IO.Path.Combine(candidate.StoragePath,"data")),compiled.Retain());instance.Open();cancellationToken.ThrowIfCancellationRequested();
                candidate=candidate with{AuditOrigin=this.CreateAuditOrigin(candidate,instance)};commit(new(candidate,rebuilt.ToArray()));
                if(this.databases.TryRemove(definition.Id,out var previous)){previous.Dispose();}this.databases[definition.Id]=instance;instance=null!;
            }finally{instance?.Dispose();}
        }finally{gate.Release();}
    }
    private void Copy(DatabaseInstance source,DatabaseInstance target,CancellationToken cancellationToken)=>TypedMigration.Copy(source,target,cancellationToken,target.Definition.Migration);
    private void BackupGeneration(DefinitionVersion version,string destination,CancellationToken cancellation){
        if(Directory.Exists(destination)||File.Exists(destination)){throw new IOException("バックアップ先は未使用の場所を指定してください。");}
        if(this.databases.TryRemove(version.Definition.Id,out var active)){active.Dispose();}
        var source=this.Full(version.StoragePath);Directory.CreateDirectory(destination);
        void CopyDirectory(string from,string to){
            if((File.GetAttributes(from)&FileAttributes.ReparsePoint)!=0){throw new IOException("バックアップ対象にリンクがあります。");}
            foreach(var entry in Directory.EnumerateFileSystemEntries(from)){
                cancellation.ThrowIfCancellationRequested();var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("バックアップ対象にリンクがあります。");}
                var target=System.IO.Path.Combine(to,System.IO.Path.GetFileName(entry));if((attributes&FileAttributes.Directory)!=0){Directory.CreateDirectory(target);CopyDirectory(entry,target);}else{File.Copy(entry,target,false);}
            }
        }
        CopyDirectory(source,destination);File.WriteAllText(System.IO.Path.Combine(destination,"definition-version.json"),JsonSerializer.Serialize(version,ManagementJson.Options));
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
        var gate=this.Gate(gateKey);capture?.Begin();this.BeforeQueryAdmission?.Invoke();capture?.WaitingForGate();gate.Wait(cancellationToken);ManagedLoadContext? context=null;ReadAuditObservation? completedRead=null;
        try{
            capture?.EnteredGate();
            if(request.ValidationDatabaseId is null){version=this.store.Read<DefinitionVersion>("active",version.Definition.Id)??throw new ManagementException("NotFound","DBがありません。");}
            else{version=this.Validation(request.ValidationDatabaseId,user).Version;}
            DatabaseInstance database;if(request.ValidationDatabaseId is null){database=this.Open(version);}else{temporary=new(version.Definition,this.Full(System.IO.Path.Combine(version.StoragePath,"data")),this.ReadArtifact(version));temporary.Open();database=temporary;}
            CompiledSource query;if(request.DraftRevision.HasValue){query=this.compiler.Compile(definition.Source,[database.Compiled.AssemblyBytes],definition.EntryType,definition.EntryMember);}else{var published=this.store.Read<DefinitionVersion>("active",definition.Id)??throw new ManagementException("NotFound","問い合わせがありません。");if(request.ExpectedVersion is{} expectedVersion&&published.Version!=expectedVersion||request.ExpectedArtifactHash is{} expectedArtifact&&published.ArtifactHash!=expectedArtifact){throw new ManagementException("CatalogChanged","関数の公開版または成果物が変わりました。");}definition=published.Definition;query=this.ReadArtifact(published);capture?.Bound(published);var manifest=JsonSerializer.Deserialize<ArtifactManifest>(File.ReadAllText(this.Full(System.IO.Path.Combine(published.StoragePath,"manifest.json"))),ManagementJson.Options)!;if(manifest.DatabaseArtifact!=version.ArtifactHash){throw new ManagementException("SchemaMismatch","問い合わせの参照世代が異なります。");}}
            if(query.AssemblyBytes.Length==0){throw new ManagementException("Validation",string.Join("\n",query.Result.Diagnostics.Select(item=>item.Message)));}
            context=new(database.Assembly);this.LastQueryContext=new(context);using var pe=new MemoryStream(query.AssemblyBytes);var assembly=context.LoadFromStream(pe);var entry=assembly.GetType("Seamless.Management.Generated.QueryEntry",false);var type=entry??assembly.GetType(definition.EntryType,true)!;var member=definition.EntryMember;if(entry is not null){member="Invoke";}var method=type.GetMethod(member,BindingFlags.Public|BindingFlags.Static)??throw new ManagementException("Validation","公開されたstatic入口がありません。");
            var execution=new ManagementExecutionContext(database.Database,cancellationToken);var arguments=method.GetParameters().Select(parameter=>{if(parameter.ParameterType==typeof(ManagementExecutionContext)){return execution;}return RuntimeValueJson.Deserialize(request.Arguments.GetProperty(parameter.Name!),parameter.ParameterType);}).ToArray();
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
        }finally{context?.Unload();temporary?.Dispose();gate.Release();}
        if(completedRead is{} sample){this.ReadCompleted?.Invoke(sample);}
    }
    public void Dispose(){this.StopAuditDelivery();foreach(var item in this.databases.Values){item.Dispose();}this.databases.Clear();}
    private static object Materialize(JsonElement value,Type type)=>RuntimeValueJson.Deserialize(value,type)??throw new ManagementException("Validation","行の値を構築できません。");
}
