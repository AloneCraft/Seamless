using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Database;

namespace Seamless.Management.Server;

internal sealed record PreparedDefinition(Definition Definition,long ExpectedVersion,string ArtifactHash,string StoragePath);
internal sealed record PreparedPublication(PreparedDefinition Primary,PreparedDefinition[] Dependents);
internal sealed record ArtifactManifest(Definition Definition,string Sha256,string? DatabaseArtifact);
internal sealed class ManagedLoadContext(Assembly? database=null):AssemblyLoadContext(isCollectible:true){
    protected override Assembly? Load(AssemblyName name){if(database is not null&&AssemblyName.ReferenceMatchesDefinition(database.GetName(),name)){return database;}return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly=>AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name));}
}
internal sealed class DatabaseInstance(Definition definition,string path,CompiledSource compiled):IDisposable{
    internal readonly ManagedLoadContext Context=new();
    internal readonly Definition Definition=definition;
    internal readonly string Path=path;
    internal readonly CompiledSource Compiled=compiled;
    internal Assembly Assembly{get;private set;}=null!;
    internal DatabaseContainer Database{get;private set;}=null!;
    internal void Open(){
        using var stream=new MemoryStream(this.Compiled.AssemblyBytes);this.Assembly=this.Context.LoadFromStream(stream);
        var type=this.Assembly.GetType(this.Definition.EntryType,true)!;
        if(!typeof(DatabaseContainer).IsAssignableFrom(type)){throw new ManagementException("Validation","DBの入口はDatabaseContainer派生型にしてください。");}
        var options=DatabaseContainerOptions.OnStorage(this.Path) with{Transactions=DatabaseTransactionMode.SharedV2,RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=1<<24,TemporaryDirectory=System.IO.Path.Combine(this.Path,"temporary"),MaximumQueryWorkers=Math.Min(8,Environment.ProcessorCount)};
        this.Database=(DatabaseContainer)(Activator.CreateInstance(type,[options])??throw new ManagementException("Validation","DBコンストラクターが必要です。"));
        foreach(var property in Tables(this.Database)){_=property.GetValue(this.Database);}
    }
    internal static PropertyInfo[] Tables(DatabaseContainer database)=>database.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.PropertyType.IsGenericType&&(property.PropertyType.GetGenericTypeDefinition()==typeof(Table<,>)||property.PropertyType.GetGenericTypeDefinition()==typeof(Table<>))).ToArray();
    public void Dispose(){this.Database?.Dispose();this.Context.Unload();}
}

internal sealed class NativeRuntime:IDisposable{
    private readonly ManagementStore store;
    private readonly RuntimeCompiler compiler=new();
    private readonly ConcurrentDictionary<string,DatabaseInstance> databases=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string,SemaphoreSlim> gates=new(StringComparer.Ordinal);
    internal Action? BeforeQueryAdmission{get;set;}
    internal WeakReference? LastQueryContext{get;private set;}
    internal NativeRuntime(ManagementStore store){this.store=store;}
    private SemaphoreSlim Gate(string database)=>this.gates.GetOrAdd(database,_=>new(1,1));
    internal CompiledSource Compile(Definition definition){
        if(definition.Kind==DefinitionKind.Database){return this.compiler.Compile(definition.Source);}
        var version=this.DatabaseDefinition(definition.Database);var bytes=this.ReadArtifact(version).AssemblyBytes;
        return this.compiler.Compile(definition.Source,[bytes],definition.EntryType,definition.EntryMember);
    }
    internal AnalysisResult Analyze(Definition definition){
        var references=SourceEditor.PlatformReferences().Cast<Microsoft.CodeAnalysis.MetadataReference>().ToList();
        if(definition.Kind==DefinitionKind.Query){references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromImage(this.ReadArtifact(this.DatabaseDefinition(definition.Database)).AssemblyBytes));}
        var projection=new SourceEditor(references).Analyze(definition.Source);var build=this.Compile(definition).Result;
        return projection with{Diagnostics=build.Diagnostics,Current=build.Diagnostics.All(item=>item.Severity!="Error")};
    }
    private DefinitionVersion DatabaseDefinition(string database){
        long cursor=0;do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:version=>version.Definition.Kind==DefinitionKind.Database&&version.Definition.Database==database);if(page.Items.Length>0){return page.Items[0];}cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);
        throw new ManagementException("NotFound","先にDB定義を適用してください: "+database);
    }
    internal string[] Dependencies(Definition definition){
        if(definition.Kind==DefinitionKind.Query){var database=this.DatabaseDefinition(definition.Database);return [database.Definition.Id+"@"+database.Version+":"+database.ArtifactHash];}
        var result=new List<string>();long cursor=0;do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Definition.Database==definition.Database&&item.Definition.Kind==DefinitionKind.Query);result.AddRange(page.Items.Select(item=>item.Definition.Id+"@"+item.Version));cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);return result.ToArray();
    }
    internal DiagnosticInfo[] ValidateDependents(Definition definition,CompiledSource compiled){
        if(definition.Kind!=DefinitionKind.Database||compiled.AssemblyBytes.Length==0){return [];}
        var diagnostics=new List<DiagnosticInfo>();long cursor=0;do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Definition.Database==definition.Database&&item.Definition.Kind==DefinitionKind.Query);foreach(var query in page.Items){var result=this.compiler.Compile(query.Definition.Source,[compiled.AssemblyBytes],query.Definition.EntryType,query.Definition.EntryMember);diagnostics.AddRange(result.Result.Diagnostics.Select(item=>item with{Message=query.Definition.Id+": "+item.Message}));}cursor=page.NextCursor;if(!page.HasMore){break;}}while(true);return diagnostics.ToArray();
    }
    internal string[] DescribeChanges(Definition? before,Definition after){
        var changes=new List<string>();if(before is null){changes.Add("新しい"+after.Kind+"定義を公開します。");}
        else{foreach(var file in before.Source.Files){var next=after.Source.Files.SingleOrDefault(item=>item.Path==file.Path);if(next is null){changes.Add("ファイルを削除: "+file.Path);}else if(next.Text!=file.Text){changes.Add("ファイルを変更: "+file.Path+"\n--- 変更前\n"+file.Text+"\n+++ 変更後\n"+next.Text);}}foreach(var file in after.Source.Files.Where(item=>before.Source.Files.All(previous=>previous.Path!=item.Path))){changes.Add("ファイルを追加: "+file.Path+"\n"+file.Text);}}
        changes.Add("入口: "+after.EntryType+"."+after.EntryMember+" / 更新取引: "+after.WritesData);
        if(before is not null&&after.Kind==DefinitionKind.Database){var old=this.Compile(before);var next=this.Compile(after);if(old.AssemblyBytes.Length>0&&next.AssemblyBytes.Length>0){var previousContext=new ManagedLoadContext();var nextContext=new ManagedLoadContext();try{using var previousStream=new MemoryStream(old.AssemblyBytes);using var nextStream=new MemoryStream(next.AssemblyBytes);var previousType=previousContext.LoadFromStream(previousStream).GetType(before.EntryType,true)!;var nextType=nextContext.LoadFromStream(nextStream).GetType(after.EntryType,true)!;changes.AddRange(TypedMigration.Describe(previousType,nextType));}finally{previousContext.Unload();nextContext.Unload();}}changes.Add("稼働版を保持したまま非公開世代へ行を逐次移行し、制約を検証してから公開します。");}
        return changes.ToArray();
    }
    private string Full(string relative){var root=System.IO.Path.GetFullPath(this.store.Root)+System.IO.Path.DirectorySeparatorChar;var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));if(!path.StartsWith(root,StringComparison.Ordinal)){throw new InvalidDataException("保存先が管理領域を外れています。");}return path;}
    private PreparedDefinition Save(Definition definition,CompiledSource compiled,long expectedVersion,string? databaseArtifact=null){
        if(compiled.AssemblyBytes.Length==0){throw new ManagementException("Validation",string.Join("\n",compiled.Result.Diagnostics.Select(item=>item.Message)));}
        var relative=System.IO.Path.Combine("generations",Guid.NewGuid().ToString("N"));var directory=this.Full(relative);Directory.CreateDirectory(directory);
        using(var file=new FileStream(System.IO.Path.Combine(directory,"module.dll"),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){file.Write(compiled.AssemblyBytes);file.Flush(true);}
        using(var file=new FileStream(System.IO.Path.Combine(directory,"manifest.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){JsonSerializer.Serialize(file,new ArtifactManifest(definition,compiled.Result.ArtifactHash,databaseArtifact),ManagementJson.Options);file.Flush(true);}
        this.store.Write(()=>this.store.Put("build",compiled.Result.ArtifactHash,compiled.Result));
        return new(definition,expectedVersion,compiled.Result.ArtifactHash,relative);
    }
    internal CompiledSource ReadArtifact(DefinitionVersion version){
        var path=this.Full(version.StoragePath);var bytes=File.ReadAllBytes(System.IO.Path.Combine(path,"module.dll"));if(Convert.ToHexString(SHA256.HashData(bytes))!=version.ArtifactHash){throw new InvalidDataException("公開済みDLLのハッシュが一致しません。");}
        using var manifest=File.OpenRead(System.IO.Path.Combine(path,"manifest.json"));var identity=JsonSerializer.Deserialize<ArtifactManifest>(manifest,ManagementJson.Options)??throw new InvalidDataException("ビルド情報がありません。");if(identity.Definition.Hash()!=version.Definition.Hash()||identity.Sha256!=version.ArtifactHash){throw new InvalidDataException("版とビルドの識別情報が一致しません。");}
        var build=this.store.Read<BuildResult>("build",version.ArtifactHash);if(build is not null){if(build.SourceHash!=version.Definition.Source.Hash()||build.ArtifactHash!=version.ArtifactHash){throw new InvalidDataException("保存された計画の識別情報が一致しません。");}return new(bytes,build);}
        using var pe=new MemoryStream(bytes);return new(bytes,new(version.Definition.Source.Hash(),version.ArtifactHash,[],ExecutionPlanDocument.Read(pe)));
    }
    private DatabaseInstance Open(DefinitionVersion version){
        return this.databases.GetOrAdd(version.Definition.Database,_=>{var instance=new DatabaseInstance(version.Definition,this.Full(System.IO.Path.Combine(version.StoragePath,"data")),this.ReadArtifact(version));try{instance.Open();return instance;}catch{instance.Dispose();throw;}});
    }
    internal void Publish(Definition definition,CancellationToken cancellationToken,Action<PreparedPublication> commit){
        var gate=this.Gate(definition.Database);gate.Wait(cancellationToken);
        try{
            var old=this.store.Read<DefinitionVersion>("active",definition.Id);var compiled=this.Compile(definition);cancellationToken.ThrowIfCancellationRequested();
            if(definition.Kind==DefinitionKind.Query){var db=this.DatabaseDefinition(definition.Database);var prepared=this.Save(definition,compiled,old?.Version??0,db.ArtifactHash);commit(new(prepared,[]));return;}
            var candidate=this.Save(definition,compiled,old?.Version??0);var instance=new DatabaseInstance(definition,this.Full(System.IO.Path.Combine(candidate.StoragePath,"data")),compiled);
            try{
                instance.Open();if(old is not null){this.Copy(this.Open(old),instance,cancellationToken);}
                var rebuilt=new List<PreparedDefinition>();long cursor=0;
                do{var page=this.store.List<DefinitionVersion>("active",cursor,predicate:item=>item.Definition.Database==definition.Database&&item.Definition.Kind==DefinitionKind.Query);
                    foreach(var query in page.Items){cancellationToken.ThrowIfCancellationRequested();var result=this.compiler.Compile(query.Definition.Source,[compiled.AssemblyBytes],query.Definition.EntryType,query.Definition.EntryMember);rebuilt.Add(this.Save(query.Definition,result,query.Version,candidate.ArtifactHash));}
                    cursor=page.NextCursor;if(!page.HasMore){break;}
                }while(true);
                instance.Dispose();instance=new(definition,this.Full(System.IO.Path.Combine(candidate.StoragePath,"data")),compiled);instance.Open();cancellationToken.ThrowIfCancellationRequested();
                commit(new(candidate,rebuilt.ToArray()));
                if(this.databases.TryRemove(definition.Database,out var previous)){previous.Dispose();}this.databases[definition.Database]=instance;instance=null!;
            }finally{instance?.Dispose();}
        }finally{gate.Release();}
    }
    private void Copy(DatabaseInstance source,DatabaseInstance target,CancellationToken cancellationToken)=>TypedMigration.Copy(source,target,cancellationToken);
    internal ValidationDatabase CreateValidation(DefinitionVersion version,string owner){
        if(version.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("Validation","DB定義を選択してください。");}
        var id=Guid.NewGuid().ToString("N");var validation=new ValidationDatabase(id,version.Definition.Database,version.Definition.Id,version.Version,owner);
        var definition=version.Definition;var compiled=this.ReadArtifact(version);var relative=System.IO.Path.Combine("validation",id);var prepared=this.Save(definition,compiled,version.Version);var instance=new DatabaseInstance(definition,this.Full(System.IO.Path.Combine(prepared.StoragePath,"data")),compiled);
        try{instance.Open();}finally{instance.Dispose();}
        this.store.Write(()=>{this.store.Put("validation",id,validation);this.store.Put("validationVersion",id,new DefinitionVersion(definition,version.Version,owner,DateTimeOffset.UtcNow,prepared.ArtifactHash,prepared.StoragePath));});return validation;
    }
    internal ValidationDatabase CreateValidation(Draft draft,string owner){
        if(draft.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("Validation","DB定義の下書きを選択してください。");}
        var compiled=this.Compile(draft.Definition);var prepared=this.Save(draft.Definition,compiled,draft.BaseVersion);
        var result=this.CreateValidation(new DefinitionVersion(draft.Definition,draft.Revision,owner,DateTimeOffset.UtcNow,prepared.ArtifactHash,prepared.StoragePath),owner) with{SourceHash=draft.Definition.Source.Hash(),FromDraft=true};
        this.store.Write(()=>this.store.Put("validation",result.Id,result));return result;
    }
    internal BuildResult PublishedPlans(DefinitionVersion version)=>this.ReadArtifact(version).Result;
    private (ValidationDatabase Validation,DefinitionVersion Version) Validation(string id,UserAccount user){var validation=this.store.Read<ValidationDatabase>("validation",id)??throw new ManagementException("NotFound","検証DBがありません。");if(validation.Deleted){throw new ManagementException("NotFound","検証DBは破棄済みです。");}if(ManagementStore.Normalize(validation.Owner)!=ManagementStore.Normalize(user.Name)||!ManagementService.Can(user,validation.Database,ManagementPermission.Validate)){throw new ManagementException("Permission","検証DBの所有者または権限が一致しません。");}return(validation,this.store.Read<DefinitionVersion>("validationVersion",id)!);}
    internal void Seed(SeedRequest request,UserAccount user){
        var (_,version)=this.Validation(request.ValidationDatabaseId,user);if(request.Rows.Length>1000){throw new ManagementException("InvalidRequest","投入は1000行以下に分割してください。");}
        var gate=this.Gate("validation:"+request.ValidationDatabaseId);gate.Wait();try{_=this.Validation(request.ValidationDatabaseId,user);using var instance=new DatabaseInstance(version.Definition,this.Full(System.IO.Path.Combine(version.StoragePath,"data")),this.ReadArtifact(version));instance.Open();var property=DatabaseInstance.Tables(instance.Database).SingleOrDefault(property=>property.Name==request.Table)??throw new ManagementException("NotFound","テーブルがありません。");var table=property.GetValue(instance.Database)!;var type=property.PropertyType.GetGenericArguments()[^1];var add=table.GetType().GetMethod("Add",[type])!;using var tx=instance.Database.BeginTransaction();foreach(var row in request.Rows){if(add.Invoke(table,[Materialize(row,type)]) is false){throw new ManagementException("Constraint","キーが重複しています。");}}tx.Commit();}finally{gate.Release();}
    }
    internal void DropValidation(string id,UserAccount user){
        var gate=this.Gate("validation:"+id);gate.Wait();try{
            var validation=this.store.Read<ValidationDatabase>("validation",id)??throw new ManagementException("NotFound","検証DBがありません。");if(ManagementStore.Normalize(validation.Owner)!=ManagementStore.Normalize(user.Name)||!ManagementService.Can(user,validation.Database,ManagementPermission.Validate)){throw new ManagementException("Permission","検証DBの所有者または権限が一致しません。");}
            var version=this.store.Read<DefinitionVersion>("validationVersion",id)??throw new ManagementException("RecoveryRequired","検証DBの版情報がありません。");var path=this.Full(version.StoragePath);
            if(this.store.List<DefinitionVersion>("active",predicate:item=>item.StoragePath==version.StoragePath).Items.Length>0){throw new ManagementException("Conflict","稼働版と共有する保存先は破棄できません。");}
            // 再起動後も破棄済みを再オープンしないよう、所有する検証領域だけを先に無効化する。
            this.store.Write(()=>{this.store.Put("validation",id,validation with{Deleted=true});this.store.AddHistory(user.Name,validation.Database,"ValidationDiscarded",id,validation.SourceVersion);});
            if(Directory.Exists(path)){var pending=new Queue<string>();pending.Enqueue(path);while(pending.TryDequeue(out var current)){if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む検証領域はCLIで確認してください。");}foreach(var entry in Directory.EnumerateFileSystemEntries(current)){var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む検証領域はCLIで確認してください。");}if((attributes&FileAttributes.Directory)!=0){pending.Enqueue(entry);}}}Directory.Delete(path,true);}
        }finally{gate.Release();}
    }
    internal IEnumerable<ExecutionRow> Execute(Definition definition,ExecuteRequest request,UserAccount user,CancellationToken cancellationToken){
        if(definition.Kind!=DefinitionKind.Query||request.MaximumRows is <1 or >10000){throw new ManagementException("Validation","問い合わせと1〜10000行の上限を指定してください。");}
        DatabaseInstance? temporary=null;DefinitionVersion version;var gateKey=definition.Database;
        if(request.ValidationDatabaseId is not null){var validation=this.Validation(request.ValidationDatabaseId,user);if(validation.Validation.Database!=definition.Database){throw new ManagementException("Permission","検証DBの接続先が異なります。");}version=validation.Version;gateKey="validation:"+request.ValidationDatabaseId;}
        else{version=this.DatabaseDefinition(definition.Database);}
        var gate=this.Gate(gateKey);this.BeforeQueryAdmission?.Invoke();gate.Wait(cancellationToken);ManagedLoadContext? context=null;
        try{
            if(request.ValidationDatabaseId is null){version=this.DatabaseDefinition(definition.Database);}
            else{version=this.Validation(request.ValidationDatabaseId,user).Version;}
            DatabaseInstance database;if(request.ValidationDatabaseId is null){database=this.Open(version);}else{temporary=new(version.Definition,this.Full(System.IO.Path.Combine(version.StoragePath,"data")),this.ReadArtifact(version));temporary.Open();database=temporary;}
            CompiledSource query;if(request.DraftRevision.HasValue){query=this.compiler.Compile(definition.Source,[database.Compiled.AssemblyBytes],definition.EntryType,definition.EntryMember);}else{var published=this.store.Read<DefinitionVersion>("active",definition.Id)??throw new ManagementException("NotFound","問い合わせがありません。");definition=published.Definition;query=this.ReadArtifact(published);var manifest=JsonSerializer.Deserialize<ArtifactManifest>(File.ReadAllText(this.Full(System.IO.Path.Combine(published.StoragePath,"manifest.json"))),ManagementJson.Options)!;if(manifest.DatabaseArtifact!=version.ArtifactHash){throw new ManagementException("SchemaMismatch","問い合わせの参照世代が異なります。");}}
            if(query.AssemblyBytes.Length==0){throw new ManagementException("Validation",string.Join("\n",query.Result.Diagnostics.Select(item=>item.Message)));}
            context=new(database.Assembly);this.LastQueryContext=new(context);using var pe=new MemoryStream(query.AssemblyBytes);var assembly=context.LoadFromStream(pe);var entry=assembly.GetType("Seamless.Management.Generated.QueryEntry",false);var type=entry??assembly.GetType(definition.EntryType,true)!;var member=definition.EntryMember;if(entry is not null){member="Invoke";}var method=type.GetMethod(member,BindingFlags.Public|BindingFlags.Static)??throw new ManagementException("Validation","公開されたstatic入口がありません。");
            var execution=new ManagementExecutionContext(database.Database,cancellationToken);var arguments=method.GetParameters().Select(parameter=>{if(parameter.ParameterType==typeof(ManagementExecutionContext)){return execution;}return RuntimeValueJson.Deserialize(request.Arguments.GetProperty(parameter.Name!),parameter.ParameterType);}).ToArray();
            // 更新する公開定義だけが取引を開始できる。結果上限・取消・列挙例外では確定しない。
            DatabaseReadSession? readScope=null;DatabaseTransaction? writeScope=null;if(definition.WritesData){writeScope=database.Database.BeginTransaction(cancellationToken);}else{readScope=database.Database.BeginReadSession(cancellationToken);}
            using var read=readScope;using var write=writeScope;
            cancellationToken.ThrowIfCancellationRequested();var result=method.Invoke(null,arguments);var count=0;
            if(result is IEnumerable rows&&result is not string){
                var iterator=rows.GetEnumerator();try{
                    while(count<request.MaximumRows&&iterator.MoveNext()){cancellationToken.ThrowIfCancellationRequested();var row=iterator.Current;count++;yield return new(RuntimeValueJson.Serialize(row));}
                    if(definition.WritesData&&count==request.MaximumRows&&iterator.MoveNext()){throw new ManagementException("ResultLimit","更新結果が上限を超えたため、取引を取り消しました。");}
                }finally{(iterator as IDisposable)?.Dispose();}
            }else{yield return new(RuntimeValueJson.Serialize(result));}
            cancellationToken.ThrowIfCancellationRequested();write?.Commit();
        }finally{context?.Unload();temporary?.Dispose();gate.Release();}
    }
    public void Dispose(){foreach(var item in this.databases.Values){item.Dispose();}this.databases.Clear();}
    private static object Materialize(JsonElement value,Type type)=>RuntimeValueJson.Deserialize(value,type)??throw new ManagementException("Validation","行の値を構築できません。");
}
