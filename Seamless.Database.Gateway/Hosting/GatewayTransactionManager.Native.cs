using System.Reflection;
using Seamless.Database.Client;
using Seamless.Transactions;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Hosting;

public sealed record GatewayNativeQueryStatus(string ModuleId,NativeModuleManifest Manifest,string? Diagnostic);
public sealed record GatewayNativePublicationSummary(string Database,string ModuleId,string QueryId,Guid GenerationId,bool IsReadOnly);
public sealed record GatewayNativePublicationPage(Guid Version,IReadOnlyList<GatewayNativePublicationSummary> Items,bool HasMore);

public sealed partial class GatewayTransactionManager{
    private GatewayNativeQueryStore? nativeStore;
    private readonly Dictionary<string,NativePublished> nativePublished=new(StringComparer.Ordinal);
    private Guid nativeCatalogVersion=Guid.NewGuid();
    private long nextNativeSessionId;
    internal void ConfigureNativeQueries(GatewayNativeQueryStore store)=>this.nativeStore=store;
    public IReadOnlyList<GatewayNativeQueryStatus> NativeQueries{get{lock(this.gate){this.EnsureReady();return this.nativePublished.Select(static pair=>new GatewayNativeQueryStatus(pair.Key,pair.Value.Version.Manifest,pair.Value.Version.Diagnostic)).ToArray();}}}
    /// <summary>許可対象DBだけに依存する現在の公開クエリを、名前順に上限付きで取得する。</summary>
    public GatewayNativePublicationPage ListNativePublications(IReadOnlySet<string> databases,Guid version,string? afterModule,string? afterQuery,int pageSize,CancellationToken cancellationToken=default){
        if(pageSize is <1 or >100){throw new ArgumentOutOfRangeException(nameof(pageSize));}
        lock(this.gate){
            this.EnsureReady();
            cancellationToken.ThrowIfCancellationRequested();
            var current=this.nativeCatalogVersion;
            if(version!=Guid.Empty&&version!=current||databases.Count==0){return new(current,[],false);}
            var items=new SortedSet<GatewayNativePublicationSummary>(Comparer<GatewayNativePublicationSummary>.Create(static (left,right)=>{
                var order=string.CompareOrdinal(left.ModuleId,right.ModuleId);
                if(order!=0){return order;}
                return string.CompareOrdinal(left.QueryId,right.QueryId);
            }));
            foreach(var (module,published) in this.nativePublished){
                cancellationToken.ThrowIfCancellationRequested();
                var order=string.CompareOrdinal(module,afterModule);
                if(order<0||string.IsNullOrWhiteSpace(module)||module.Length>1024||module.StartsWith('$')||published.Version.Artifact is not{} artifact||
                    artifact.Dependencies.Count>1024||!artifact.Dependencies.All(dependency=>databases.Contains(dependency.Database))){continue;}
                var generation=Guid.ParseExact(published.Version.Reference.Id,"N");
                foreach(var query in published.Version.Manifest.Queries){
                    cancellationToken.ThrowIfCancellationRequested();
                    if(string.IsNullOrWhiteSpace(query.Id)||query.Id.Length>1024||query.Database.Length>1024||
                        order==0&&string.CompareOrdinal(query.Id,afterQuery)<=0||!databases.Contains(query.Database)){continue;}
                    if(items.Count>pageSize&&items.Max is{} last){
                        var compared=string.CompareOrdinal(module,last.ModuleId);
                        if(compared>0||compared==0&&string.CompareOrdinal(query.Id,last.QueryId)>=0){continue;}
                        items.Remove(last);
                    }
                    items.Add(new(query.Database,module,query.Id,generation,query.IsReadOnly));
                }
            }
            return new(current,items.Take(pageSize).ToArray(),items.Count>pageSize);
        }
    }
    public Task InstallNativeQueryAsync(NativeQuerySourceProject source,CancellationToken cancellationToken=default)=>
        this.InstallNativeVersionAsync(source.ModuleId,source.Dependencies,session=>this.PrepareNativeVersionAsync(session,source,false),cancellationToken);
    /// <summary>共通ビルド済みのNative成果物を、依存DBと同じ取引で公開する。</summary>
    public Task InstallPrebuiltNativeQueryAsync(NativeQueryBuildArtifact artifact,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(artifact);
        if(artifact.Manifest is null||artifact.Manifest.Queries is null||artifact.Dependencies is null||artifact.Manifest.Queries.Count is <1 or >1024||artifact.Dependencies.Count is <1 or >1024){throw new InvalidDataException("Invalid prebuilt native manifest or dependency count.");}
        artifact=artifact with{Manifest=artifact.Manifest with{Queries=Array.AsReadOnly(artifact.Manifest.Queries.ToArray())},Dependencies=Array.AsReadOnly(artifact.Dependencies.ToArray())};
        var project=artifact;
        return this.InstallNativeVersionAsync(project.Manifest.ModuleId,project.Dependencies.Select(static item=>item.Database).ToArray(),session=>{
            var bindings=this.NativeBindings(session,project.Dependencies.Select(static item=>item.Database).ToArray());
            var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
            var prepared=this.nativeStore!.PreparePrebuilt(project,bindings.Inputs,version,bindings.Assemblies,session.CancellationToken);
            this.StageNativeVersion(session,project.Manifest.ModuleId,prepared,bindings.Assemblies);return Task.CompletedTask;
        },cancellationToken);
    }
    private async Task InstallNativeVersionAsync(string moduleId,IReadOnlyList<string> names,Func<SessionContext,Task> prepare,CancellationToken cancellationToken){
        this.EnsureReady();cancellationToken.ThrowIfCancellationRequested();if(this.nativeStore is null){throw new InvalidOperationException("Native registration requires generation storage.");}
        var database=names.FirstOrDefault()??throw new ArgumentException("A native project needs database dependencies.",nameof(names));
        using var session=new SessionContext(Interlocked.Decrement(ref this.nextNativeSessionId),database){CancellationToken=cancellationToken};this.Begin(session);
        try{
            using var binding=DatabaseRegistry.EnterSqlSession(session);
            var dependencies=names.AsEnumerable();lock(this.gate){if(this.nativePublished.TryGetValue(moduleId,out var previous)){dependencies=dependencies.Concat(previous.Version.Source.Dependencies).ToArray();}}
            this.AcquireNativeDependencies(session,dependencies);await prepare(session).ConfigureAwait(false);this.Commit(session);
        }catch{if(session.IsInTransaction){this.Rollback(session);}throw;}
    }
    private void AcquireNativeDependencies(SessionContext session,IEnumerable<string> names){
        using var binding=DatabaseRegistry.EnterSqlSession(session);var transaction=this.Own(session);
        var registrations=names.Distinct(StringComparer.OrdinalIgnoreCase).Select(name=>this.generationRegistry!.Resolve(name)).OrderBy(static registration=>((InProcessDatabaseAdapter)registration.Adapter).Database.Options.GatewayProfile!.DatabaseId).ToArray();
        foreach(var registration in registrations){var adapter=(InProcessDatabaseAdapter)registration.Adapter;var participant=this.Enlist(session,adapter);participant.SchemaLease??=this.Acquire(session,transaction.Id,adapter.Database.Options.GatewayProfile!.DatabaseId,DatabaseLockMode.Schema);}
    }
    private NativeQuerySourceProject[] AffectedNativeProjects(SessionContext session,string database){
        if(this.versioned is not null){string[] names;lock(this.gate){names=this.nativePublished.Keys.Concat(this.Own(session).NativeChanges.Keys).Distinct(StringComparer.Ordinal).ToArray();}return names.Select(id=>this.ResolveNativeVersion(session,id)?.Source).OfType<NativeQuerySourceProject>().Where(source=>source.Dependencies.Contains(database,StringComparer.OrdinalIgnoreCase)).ToArray();}
        lock(this.gate){var projects=this.nativePublished.ToDictionary(static pair=>pair.Key,static pair=>pair.Value.Version.Source,StringComparer.Ordinal);foreach(var pair in this.Own(session).NativeChanges){projects[pair.Key]=pair.Value.Source;}return projects.Values.Where(source=>source.Dependencies.Contains(database,StringComparer.OrdinalIgnoreCase)).ToArray();}
    }
    private void AcquireNativeSchemaDependencies(SessionContext session,string database){
        if(this.nativeStore is null){return;}this.AcquireNativeDependencies(session,this.AffectedNativeProjects(session,database).SelectMany(static source=>source.Dependencies));
    }
    private void RebuildNativeForSchema(SessionContext session,string database){
        if(this.nativeStore is null){return;}foreach(var project in this.AffectedNativeProjects(session,database)){this.PrepareNativeVersionAsync(session,project,true).GetAwaiter().GetResult();}
    }
    private (NativeDatabaseAssembly[] Inputs,IReadOnlyDictionary<string,Assembly> Assemblies) NativeBindings(SessionContext session,IReadOnlyList<string> dependencies){
        using var binding=DatabaseRegistry.EnterSqlSession(session);var inputs=new List<NativeDatabaseAssembly>();var assemblies=new Dictionary<string,Assembly>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in dependencies){var registration=this.generationRegistry!.Resolve(name);var assembly=((InProcessDatabaseAdapter)registration.Adapter).Database.GetType().Assembly;inputs.Add(new(name,registration.SchemaFingerprint!,assembly.Location));assemblies.Add(name,assembly);}return(inputs.ToArray(),assemblies);
    }
    private async Task PrepareNativeVersionAsync(SessionContext session,NativeQuerySourceProject project,bool disable){
        var bindings=this.NativeBindings(session,project.Dependencies);var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        GatewayNativeQueryVersion prepared;
        if(disable&&this.ResolveNativeVersion(session,project.ModuleId)?.IsPrebuilt==true){prepared=this.nativeStore!.DisablePrebuilt(project,bindings.Inputs,version,session.CancellationToken);}
        else{prepared=await this.nativeStore!.PrepareAsync(project,bindings.Inputs,version,disable,session.CancellationToken,bindings.Assemblies).ConfigureAwait(false);}
        this.StageNativeVersion(session,project.ModuleId,prepared,bindings.Assemblies);
    }
    private void StageNativeVersion(SessionContext session,string moduleId,GatewayNativeQueryVersion prepared,IReadOnlyDictionary<string,Assembly> assemblies){
        var transaction=this.Own(session);
        try{if(this.versioned is not null){prepared.Account(this.budget);}prepared.Bind(assemblies);transaction.NativeVersions.Add(prepared);transaction.NativeChanges[moduleId]=prepared;this.Resize(transaction);}catch{if(!transaction.NativeVersions.Contains(prepared)){prepared.Dispose();}throw;}
    }
    private void RebindNativeVersions(SessionContext session){foreach(var version in this.Own(session).NativeChanges.Values){version.Bind(this.NativeBindings(session,version.Source.Dependencies).Assemblies);}}
    private void RestoreNativeSavepoint(LogicalTransaction transaction,GatewaySavepoint point){transaction.NativeChanges.Clear();foreach(var pair in point.NativeVersions){transaction.NativeChanges.Add(pair.Key,pair.Value);}this.PruneNativeVersions(transaction);}
    private void PruneNativeVersions(LogicalTransaction transaction){
        var retained=transaction.NativeChanges.Values.Concat(transaction.Savepoints.SelectMany(static point=>point.NativeVersions.Values)).ToHashSet();
        foreach(var version in transaction.NativeVersions.Where(version=>!retained.Contains(version)).ToArray()){version.Dispose();transaction.NativeVersions.Remove(version);}
    }
    private void PublishNativeVersions(LogicalTransaction transaction){
        if(transaction.NativeChanges.Count==0){return;}
        if(this.versioned is not null){lock(this.gate){foreach(var pair in transaction.NativeChanges){this.nativePublished.TryGetValue(pair.Key,out var previous);this.versioned.NativeVersions[pair.Value.Reference.Id]=pair.Value;this.nativePublished[pair.Key]=new(new(pair.Key,pair.Value.Reference,previous?.State.Current),pair.Value);}this.nativeCatalogVersion=Guid.NewGuid();}return;}
        var retired=new List<GatewayNativeQueryVersion>();lock(this.gate){foreach(var pair in transaction.NativeChanges){this.nativePublished.TryGetValue(pair.Key,out var previous);if(previous is not null){retired.Add(previous.Version);}this.nativePublished[pair.Key]=new(new(pair.Key,pair.Value.Reference,previous?.State.Current),pair.Value);}this.nativeCatalogVersion=Guid.NewGuid();}
        foreach(var version in retired){version.Dispose();}
    }
    private void CloseNativeVersions(LogicalTransaction transaction,List<Exception> errors){
        HashSet<GatewayNativeQueryVersion> published;lock(this.gate){published=this.nativePublished.Values.Select(static current=>current.Version).ToHashSet();if(this.versioned is not null){published.UnionWith(this.versioned.NativeVersions.Values);}}
        foreach(var version in transaction.NativeVersions){try{if(!published.Contains(version)){version.Dispose();}}catch(Exception error){errors.Add(error);}}
        transaction.NativeVersions.Clear();transaction.NativeChanges.Clear();
    }
    internal bool TryResolveNative(SessionContext session,string id,out NativeModuleManifest manifest,out INativeQueryModule module){
        if(!this.TryResolveNativeVersion(session,id,out var version)){manifest=null!;module=null!;return false;}manifest=version.Manifest;module=version.Module;return true;
    }
    internal bool TryResolveNativeVersion(SessionContext session,string id,out GatewayNativeQueryVersion version){
        version=this.ResolveNativeVersion(session,id)!;if(version is null){return false;}if(!version.IsEnabled){throw new NativeQueryException("The native query module is disabled: "+version.Diagnostic,true);}return true;
    }
    internal NativeQueryExecution DescribeNativeExecution(SessionContext session,GatewayNativeQueryVersion version,NativeQueryDefinition definition){
        using var binding=DatabaseRegistry.EnterSqlSession(session);
        return version.Describe(this.WalId,definition,version.Artifact!.Dependencies.Select(dependency=>{
            var registration=this.generationRegistry!.Resolve(dependency.Database);
            return(dependency.Database,registration.Adapter as InProcessDatabaseAdapter??throw new NativeQueryException("A native dependency requires an in-process database.",true));
        }),this.budget);
    }
    /// <summary>管理ホストが現在公開されている成果物と依存DBの識別情報を取得する。</summary>
    public NativeQueryExecution InspectNativePublication(string database,string moduleId,string queryId,CancellationToken cancellationToken=default){
        this.EnsureReady();
        var registry=this.generationRegistry??throw new NotSupportedException("Native publication inspection requires generation storage.");
        using var session=new SessionContext(Interlocked.Decrement(ref this.nextNativeSessionId),database){CancellationToken=cancellationToken};
        return session.Run(()=>{
            using var binding=DatabaseRegistry.EnterSqlSession(session);
            var registration=registry.Resolve(database);
            var adapter=(InProcessDatabaseAdapter)registration.Adapter;
            using var read=adapter.AcquireReadSession(session);
            if(!this.TryResolveNativeVersion(session,moduleId,out var version)){throw new NativeQueryException("The native publication is unavailable.",true);}
            var definition=version.Manifest.Queries.SingleOrDefault(query=>query.Id==queryId&&string.Equals(query.Database,registration.Name,StringComparison.OrdinalIgnoreCase))
                ??throw new NativeQueryException("The native publication does not contain the requested query.",true);
            return this.DescribeNativeExecution(session,version,definition);
        });
    }
    private GatewayNativeQueryVersion? ResolveNativeVersion(SessionContext session,string id){var transaction=session.SnapshotTransaction;if(transaction?.NativeChanges.GetValueOrDefault(id) is{} own){return own;}if(this.versioned is not null&&transaction?.Scope is{} scope){var reference=this.versioned.ReadNative(scope,id);if(reference is null){return null;}lock(this.gate){return this.versioned.NativeVersions.GetValueOrDefault(reference.Id)??throw new InvalidDataException("The visible native query generation is not retained.");}}lock(this.gate){return this.nativePublished.GetValueOrDefault(id)?.Version;}}
    internal T ExecuteNativeOperation<T>(SessionContext session,Func<T> action){if(this.versioned is not null&&session.SnapshotTransaction?.Scope is{} scope){return scope.ExecuteOwnedOperation(action);}return action();}
    internal IDisposable AcquireNativeQueryReads(SessionContext session,string id,bool readOnly){
        var dependencies=this.ResolveNativeVersion(session,id)?.Source.Dependencies.ToArray()??[];
        var leases=new List<IDisposable>();
        try{foreach(var registration in dependencies.Select(name=>this.generationRegistry!.Resolve(name)).OrderBy(static registration=>((InProcessDatabaseAdapter)registration.Adapter).Database.Options.GatewayProfile!.DatabaseId)){
            var adapter=(InProcessDatabaseAdapter)registration.Adapter;if(!readOnly&&!registration.IsReadOnly){adapter.PrepareGatewayWrite(session);}leases.Add(this.AcquireRead(session,adapter));if(readOnly||registration.IsReadOnly){leases.Add(adapter.Database.BeginGatewayReadOnlyExecution());}
        }return new NativeReadLeases(leases);}catch{for(var index=leases.Count-1;index>=0;index--){leases[index].Dispose();}throw;}
    }
    internal object NativeDatabase(SessionContext session,string moduleId,string name,bool readOnly){
        var version=this.ResolveNativeVersion(session,moduleId);if(version is null||!version.Source.Dependencies.Contains(name,StringComparer.OrdinalIgnoreCase)){throw new NativeQueryException("The native query has no declared access to database '"+name+"'.",true);}
        var registration=this.generationRegistry!.Resolve(name);var adapter=(InProcessDatabaseAdapter)registration.Adapter;if(!readOnly&&!registration.IsReadOnly){adapter.PrepareGatewayWrite(session);}return adapter.Database;
    }
    internal void ValidateNativeStatement(SessionContext session){
        var transaction=this.Own(session);foreach(var participant in transaction.Participants.Values.Where(static participant=>participant.Transaction is not null||participant.Written).ToArray()){this.ValidateConstraints(session,this.databases[participant.Adapter.Database.Options.GatewayProfile!.DatabaseId].Name);}
    }
    private void RestoreNativePublications(IEnumerable<NativePublishedState> states){
        foreach(var state in states){if(this.nativeStore is null){throw new InvalidDataException("The common WAL requires its native query store.");}var version=this.nativeStore.Read(state.Current);try{if(this.versioned is not null){version.Account(this.budget);}if(version.Manifest.ModuleId!=state.ModuleId){throw new InvalidDataException("Native query publication identity mismatch.");}var assemblies=version.Source.Dependencies.ToDictionary(name=>name,name=>((InProcessDatabaseAdapter)this.generationRegistry!.Resolve(name).Adapter).Database.GetType().Assembly,StringComparer.OrdinalIgnoreCase);version.Bind(assemblies);this.nativePublished.Add(state.ModuleId,new(state,version));}catch{version.Dispose();throw;}}
    }
    private sealed record NativeChange(string ModuleId,GatewayNativeQueryReference Reference);
    private sealed record NativePublishedState(string ModuleId,GatewayNativeQueryReference Current,GatewayNativeQueryReference? Previous);
    private sealed record NativePublished(NativePublishedState State,GatewayNativeQueryVersion Version);
    private sealed class NativeReadLeases(List<IDisposable> leases):IDisposable{public void Dispose(){for(var index=leases.Count-1;index>=0;index--){leases[index].Dispose();}}}
}
