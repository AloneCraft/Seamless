using System.Text.Json;
using Seamless.Database.Rdbms;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    private GatewayGenerationStore? generationStore;
    private DatabaseRegistry? generationRegistry;
    private readonly Dictionary<int,PublishedGeneration> published=[];
    private long nextGenerationEpoch;
    internal bool TypedDdlEnabled=>this.generationStore is not null;
    internal IReadOnlyDictionary<string,object?> CompleteInsert(SessionContext session,InProcessDatabaseAdapter adapter,TableMetadata table,IReadOnlyDictionary<string,object?> supplied){
        if(this.generationStore is null){return supplied;}var id=adapter.Database.Options.GatewayProfile!.DatabaseId;var database=this.databases[id].Name;var schema=this.TransactionSchema(session,database);var definition=schema.Definition.Schemas.Single(owner=>owner.Name==table.SchemaName).Tables.Single(item=>item.Name==table.TableName);var values=new Dictionary<string,object?>(supplied,StringComparer.OrdinalIgnoreCase);
        using var binding=DatabaseRegistry.EnterSqlSession(session);var queries=new SqlQueryEngine(this.generationRegistry!);
        if(definition.Columns.Any(column=>column.ComputedDefinition is not null&&values.ContainsKey(column.Name))){throw new SqlQueryException("A computed column cannot be inserted explicitly.",271);}
        foreach(var column in definition.Columns.Where(static column=>column.Type.SqlTypeName is "rowversion" or "timestamp")){if(values.ContainsKey(column.Name)){throw new SqlQueryException("An explicit timestamp value cannot be inserted.",273);}values[column.Name]=new byte[8];}
        var identity=schema.Tables.Single(item=>item.Id==table.StableObjectId);foreach(var column in definition.Columns.Where(static column=>column.IsIdentity)){var enabled=session.IdentityInsertTarget==(id,identity.Id);if(values.TryGetValue(column.Name,out var suppliedIdentity)){if(!enabled){throw new SqlQueryException("An explicit identity value requires IDENTITY_INSERT ON.",544);}values[column.Name]=this.NextIdentity(session,database,identity,identity.Columns.Single(item=>item.Name==column.Name),column,suppliedIdentity,true);}else{if(enabled){throw new SqlQueryException("An explicit identity value is required while IDENTITY_INSERT is ON.",545);}values[column.Name]=this.NextIdentity(session,database,identity,identity.Columns.Single(item=>item.Name==column.Name),column);}}
        foreach(var column in definition.Columns){if(!values.ContainsKey(column.Name)&&column.DefaultDefinition is{} expression){values[column.Name]=queries.Prepare("SELECT "+expression,database).Execute(options:new(){ParentBudget=this.budget,CancellationToken=session.CancellationToken,StatementTimestamp=session.StatementTimestamp}).Rows.Single()[0];}}
        return values;
    }
    internal void ValidateConstraints(SessionContext session,string database){if(this.generationStore is null){return;}using var binding=DatabaseRegistry.EnterSqlSession(session);var registration=this.generationRegistry!.Resolve(database);var adapter=(InProcessDatabaseAdapter)registration.Adapter;var id=adapter.Database.Options.GatewayProfile!.DatabaseId;var participant=session.SnapshotTransaction?.Participants.GetValueOrDefault(id);if(participant?.Transaction is not null||participant?.Written==true){adapter.NormalizePersistedComputed(session);}Func<string,string,bool>? native=null;if(this.versioned is not null){native=(owner,table)=>adapter.HasNativeIntegerPrimaryKey(owner+"."+table);}Sql.GatewayConstraintValidator.Validate(this.TransactionSchema(session,database),database,this.generationRegistry!,new(){ParentBudget=this.budget,CancellationToken=session.CancellationToken},native);}
    internal IReadOnlyDictionary<string,object?> CompleteUpdate(SessionContext session,InProcessDatabaseAdapter adapter,TableMetadata table,IReadOnlyDictionary<string,object?> supplied){
        if(!supplied.Values.Any(static value=>value is Sql.SqlDefaultValue)){return supplied;}if(this.generationStore is null){throw new SqlQueryException("UPDATE DEFAULT requires a persisted default definition.",40515);}var id=adapter.Database.Options.GatewayProfile!.DatabaseId;var database=this.databases[id].Name;var definition=this.TransactionSchema(session,database).Definition.Schemas.Single(owner=>owner.Name==table.SchemaName).Tables.Single(item=>item.Name==table.TableName);var values=new Dictionary<string,object?>(supplied,StringComparer.OrdinalIgnoreCase);using var binding=DatabaseRegistry.EnterSqlSession(session);var queries=new SqlQueryEngine(this.generationRegistry!);
        foreach(var pair in supplied){if(pair.Value is not Sql.SqlDefaultValue){continue;}var column=definition.Columns.Single(column=>column.Name.Equals(pair.Key,StringComparison.OrdinalIgnoreCase));values[pair.Key]=column.DefaultDefinition is{} expression?queries.Prepare("SELECT "+expression,database).Execute(options:new(){ParentBudget=this.budget,CancellationToken=session.CancellationToken,StatementTimestamp=session.StatementTimestamp}).Rows.Single()[0]:null;}return values;
    }
    internal void AcquireSchema(SessionContext session,IReadOnlyList<DatabaseRegistration> targets){
        foreach(var registration in targets.OrderBy(static item=>((InProcessDatabaseAdapter)item.Adapter).Database.Options.GatewayProfile!.DatabaseId)){var adapter=(InProcessDatabaseAdapter)registration.Adapter;this.EnsureWrite(session,adapter);var id=adapter.Database.Options.GatewayProfile!.DatabaseId;var participant=this.Own(session).Participants[id];participant.SchemaLease??=this.Acquire(session,this.Own(session).Id,id,global::Seamless.Transactions.DatabaseLockMode.Schema);}
    }
    private long PublishedEpoch(int databaseId){lock(this.gate){return this.published.TryGetValue(databaseId,out var generation)?generation.State.Current.Epoch:0;}}
    private void ValidatePublishedAdapter(InProcessDatabaseAdapter adapter){lock(this.gate){var id=adapter.Database.Options.GatewayProfile!.DatabaseId;if(!this.databases.TryGetValue(id,out var registration)||!ReferenceEquals(registration.Adapter,adapter)){throw new SqlQueryException("The SQL binding belongs to an obsolete published generation.",3961);}}}
    private void DiscardVersions(LogicalTransaction transaction,int databaseId,long after){
        var discarded=transaction.RetiredParticipants.Where(participant=>participant.Adapter.Database.Options.GatewayProfile!.DatabaseId==databaseId&&participant.Version>after).ToList();
        if(transaction.Participants.TryGetValue(databaseId,out var current)&&current.Version>after){discarded.Add(current);}
        foreach(var participant in discarded.OrderByDescending(static item=>item.Version)){CloseParticipant(participant,false);transaction.RetiredParticipants.Remove(participant);}
    }
    internal DatabaseRegistration ResolveGeneration(DatabaseRegistration registration){
        var transaction=DatabaseRegistry.CurrentSqlSession?.SnapshotTransaction;if(transaction?.Manager!=this||registration.Adapter is not InProcessDatabaseAdapter adapter){return registration;}
        if(this.versioned is not null){var databaseId=adapter.Database.Options.GatewayProfile!.DatabaseId;if(transaction.Participants.TryGetValue(databaseId,out var selectedParticipant)&&selectedParticipant.Registration is{} own){return own;}var visible=this.versioned.ReadDatabase(transaction.Scope!,databaseId);lock(this.gate){return this.VersionedRegistration(this.versioned.Generations[visible.Reference.GenerationId],visible.Name);}}
        var id=adapter.Database.Options.GatewayProfile!.DatabaseId;return transaction.Participants.TryGetValue(id,out var participant)&&participant.Registration is{} selected?selected:registration;
    }
    internal long? GenerationEpoch(InProcessDatabaseAdapter adapter){
        var id=adapter.Database.Options.GatewayProfile!.DatabaseId;var transaction=DatabaseRegistry.CurrentSqlSession?.SnapshotTransaction;if(transaction?.Manager==this){if(transaction.Participants.TryGetValue(id,out var participant)&&participant.Epoch>0){return participant.Epoch;}if(this.versioned is not null){return this.versioned.ReadDatabase(transaction.Scope!,id).Epoch;}}var published=this.PublishedEpoch(id);return published>0?published:null;
    }
    internal GatewaySchemaCatalog TransactionSchema(SessionContext session,string database){
        if(this.versioned is not null){var selectedRegistration=this.generationRegistry!.Resolve(database);var databaseId=((InProcessDatabaseAdapter)selectedRegistration.Adapter).Database.Options.GatewayProfile!.DatabaseId;var ownedTransaction=this.Own(session);if(ownedTransaction.Participants.TryGetValue(databaseId,out var own)){return own.Prepared!.Schema;}var selected=this.versioned.ReadDatabase(ownedTransaction.Scope!,databaseId);lock(this.gate){return this.versioned.Generations[selected.Reference.GenerationId].Prepared!.Schema;}}
        var registration=this.generationRegistry?.Resolve(database)??throw new SqlQueryException("Typed DDL requires generation storage.",40515);var id=((InProcessDatabaseAdapter)registration.Adapter).Database.Options.GatewayProfile!.DatabaseId;var transaction=this.Own(session);
        if(transaction.Participants.TryGetValue(id,out var participant)&&participant.Candidate is{} candidate){return candidate.Schema;}lock(this.gate){return this.published[id].Prepared.Schema;}
    }
    internal void StageSchema(SessionContext session,string database,GatewaySchemaCatalog schema,MigrationContext context){
        if(this.versioned is not null){this.StageVersionedSchema(session,database,schema,context);return;}
        if(this.generationStore is null||this.generationRegistry is null){throw new SqlQueryException("Typed DDL requires generation storage.",40515);}schema.Validate();var transaction=this.Own(session);var registration=this.generationRegistry.Resolve(database);var adapter=(InProcessDatabaseAdapter)registration.Adapter;var id=adapter.Database.Options.GatewayProfile!.DatabaseId;
        if(!schema.Definition.Name.Equals(registration.Name,StringComparison.Ordinal)){throw new InvalidDataException("DDL cannot change the persistent database identity.");}
        session.CancellationToken.ThrowIfCancellationRequested();this.AcquireNativeSchemaDependencies(session,database);this.EnsureWrite(session,adapter);var source=transaction.Participants[id];source.SchemaLease??=this.Acquire(session,transaction.Id,id,global::Seamless.Transactions.DatabaseLockMode.Schema);
        var sourceLoaded=source.Candidate?.Loaded??this.published[id].Prepared.Loaded;var oldSchema=source.Candidate?.Schema??this.published[id].Prepared.Schema;GatewayGenerationCandidate? candidate=null;
        if(source.Candidate is null){source.Adapter.Database.PreserveGatewayCommittedGeneration();}
        try{
            this.failureInjection?.Invoke(GatewayCommitPhase.BeforeCandidateBuild,id);
            candidate=this.generationStore.BuildAsync(schema,this.Profile(id),new(transaction.Id,session.SessionId),session.CancellationToken).GetAwaiter().GetResult();candidate.Loaded.Adapter.ConfigureComputed(schema.Definition,this.generationRegistry);var (plans,created)=MigrationPlans(oldSchema,sourceLoaded.Generated,schema,candidate.Artifact.Generated);
            var rowIds=plans.SelectMany(plan=>plan.Plan.TargetColumns.Where(column=>column.Id==long.MaxValue-plan.Plan.TableId&&!plan.Plan.SourceColumns.Any(old=>old.Id==column.Id))).ToDictionary(static column=>column.Id,static _=>0L);
            var effective=new MigrationContext(column=>rowIds.TryGetValue(column,out var last)?rowIds[column]=checked(last+1):context.EvaluateDefault(column),session.CancellationToken);
            this.failureInjection?.Invoke(GatewayCommitPhase.CandidateBuilt,id);TypedGenerationMigrator.Prepare(sourceLoaded,candidate.Loaded,plans,effective,created);this.failureInjection?.Invoke(GatewayCommitPhase.CandidateCopied,id);candidate.Loaded.Adapter.GatewayTransactions=this;
            var next=new GatewayParticipant(candidate.Loaded.Adapter,EmptyLease.Instance){Version=checked(++transaction.NextVersion),Epoch=Interlocked.Increment(ref this.nextGenerationEpoch),Candidate=candidate};
            next.Registration=GenerationRegistration(registration.Name,next.Adapter,candidate.Artifact,candidate.Schema);transaction.Memory!.Resize(checked(transaction.Memory.Bytes+512));transaction.RetiredParticipants.EnsureCapacity(transaction.RetiredParticipants.Count+1);next.Transaction=next.Adapter.BeginGatewayDatabaseTransaction(new(transaction.Id,session.SessionId),session.CancellationToken);transaction.RetiredParticipants.Add(source);transaction.Participants[id]=next;candidate=null;session.RefreshTransactionContext();this.ValidateConstraints(session,database);this.RebuildNativeForSchema(session,database);
        }finally{candidate?.Dispose();}
    }
    private static (IReadOnlyList<GeneratedTableMigration> Plans,IReadOnlySet<string> Created)MigrationPlans(GatewaySchemaCatalog oldSchema,GeneratedDatabase oldGenerated,GatewaySchemaCatalog schema,GeneratedDatabase generated){
        var plans=new List<GeneratedTableMigration>();var created=new HashSet<string>(StringComparer.Ordinal);
        foreach(var table in schema.Tables){
            var target=generated.Tables.Single(binding=>binding.SourceSchema==table.Schema&&binding.SourceTable==table.Name);var previous=oldSchema.Tables.SingleOrDefault(item=>item.Id==table.Id);if(previous is null){created.Add(target.ObjectName);continue;}
            var source=oldGenerated.Tables.Single(binding=>binding.SourceSchema==previous.Schema&&binding.SourceTable==previous.Name);
            MigrationColumn[] Columns(GatewaySchemaCatalog catalog,GatewayTableIdentity identity,GeneratedTableBinding binding)=>binding.Columns.Select(column=>new MigrationColumn(column.IsSynthetic?long.MaxValue-identity.Id:identity.Columns.Single(item=>item.Name==column.SourceName).Id,column.PropertyName,ComparisonHost.ToSqlType(column.Type),column.Type.IsNullable){IsComputed=!column.IsSynthetic&&catalog.Definition.Schemas.Single(schema=>schema.Name==identity.Schema).Tables.Single(table=>table.Name==identity.Name).Columns.Single(field=>field.Name==column.SourceName).ComputedDefinition is not null}).ToArray();
            plans.Add(new(source.ObjectName,target.ObjectName,new(table.Id,Columns(oldSchema,previous,source),Columns(schema,table,target))));
        }
        return(plans,created);
    }
    private DatabaseRegistration GenerationRegistration(string name,InProcessDatabaseAdapter adapter,GeneratedAssemblyArtifact artifact,GatewaySchemaCatalog schema){
        var definition=schema.Definition;var views=definition.Schemas.SelectMany(item=>item.Views.Select(view=>new DatabaseViewDefinition(item.Name,view.Name,view.Definition??throw new InvalidDataException("A persisted view definition is missing."),view.Columns.Select(column=>new SqlColumn(column.Name,ComparisonHost.ToSqlType(column.Type),column.Type.IsNullable,new(name,item.Name,view.Name),column.Ordinal)).ToArray(),view.Columns){OwnerName=view.OwnerName})).ToArray();
        var routines=definition.Schemas.SelectMany(item=>item.Routines.Select(routine=>new DatabaseRoutineDefinition(item.Name,routine.Name,routine.Definition,routine,routine.Definition is null?artifact.Generated.Routines.SingleOrDefault(binding=>binding.SourceSchemaName==item.Name&&binding.SourceName==routine.Name):null))).ToArray();adapter.SetViews(views);adapter.SetRoutines(routines);adapter.ConfigureComputed(definition,this.generationRegistry!);adapter.ConfigureGatewayChanges(schema);return new(name,adapter,false,artifact.Generated.SchemaFingerprint,views,routines,definition.DefaultCollation);
    }
    private sealed class EmptyLease:IDisposable{internal static EmptyLease Instance{get;}=new();public void Dispose(){}}
    private byte[] SealGenerations(SessionContext session,LogicalTransaction transaction,GatewayTransactionOwner owner){
        var changes=new List<GenerationChange>();foreach(var participant in transaction.Participants.Values.Where(static item=>item.Candidate is not null).OrderBy(static item=>item.Adapter.Database.Options.GatewayProfile!.DatabaseId)){
            var prepared=participant.Candidate!.Seal(participant.Transaction!,session.CancellationToken);participant.Transaction=null;participant.Prepared=prepared;participant.Adapter=prepared.Loaded.Adapter;participant.Adapter.GatewayTransactions=this;participant.Epoch=Interlocked.Increment(ref this.nextGenerationEpoch);
            participant.Registration=GenerationRegistration(participant.Registration!.Name,participant.Adapter,prepared.Artifact,prepared.Schema);changes.Add(new(participant.Registration.Name,participant.Epoch,prepared.Reference));
            this.failureInjection?.Invoke(GatewayCommitPhase.GenerationSealed,prepared.Reference.DatabaseId);
        }
        this.RebindNativeVersions(session);var native=transaction.NativeChanges.Select(static pair=>new NativeChange(pair.Key,pair.Value.Reference)).ToArray();
        return changes.Count==0&&native.Length==0?[]:JsonSerializer.SerializeToUtf8Bytes(new GenerationCommit("seamless-gateway-generations-v1",changes.ToArray(),native));
    }
    private void PublishGenerations(LogicalTransaction transaction,long lsn,List<GatewayPreparedGeneration> retired){
        var replacements=transaction.Participants.Values.Where(static item=>item.Prepared is not null).ToArray();if(replacements.Length==0){return;}
        foreach(var participant in replacements){participant.Adapter.Database.CompleteGatewayRecovery(lsn);this.failureInjection?.Invoke(GatewayCommitPhase.DatabaseApplied,participant.Adapter.Database.Options.GatewayProfile!.DatabaseId);}
        this.failureInjection?.Invoke(GatewayCommitPhase.BeforePublication,0);
        lock(this.gate){
            foreach(var participant in replacements){var id=participant.Adapter.Database.Options.GatewayProfile!.DatabaseId;var old=this.published[id];retired.Add(old.Prepared);var state=new PublishedGenerationState(new(participant.Registration!.Name,participant.Epoch,lsn,participant.Prepared!.Reference),old.State.Current);this.published[id]=new(state,participant.Prepared);this.databases[id]=new(id,participant.Registration.Name,participant.Adapter);participant.KeepPrepared=true;}
            this.generationRegistry!.Publish(replacements.Select(static item=>item.Registration!).ToArray());
        }
        this.failureInjection?.Invoke(GatewayCommitPhase.AfterPublication,0);
    }
    internal void ConfigureGenerations(GatewayGenerationStore store,DatabaseRegistry registry){
        lock(this.gate){if(this.recovered||this.databases.Count!=0||!ReferenceEquals(registry.GatewayTransactions,this)){throw new InvalidOperationException("Configure generation storage before registering or recovering databases.");}this.generationStore=store;this.generationRegistry=registry;}
    }
    internal bool NeedsBootstrap=>this.journal.DurableLsn==0&&this.checkpoint is null;
    internal async Task BootstrapAsync(IReadOnlyList<DatabaseCatalog> definitions,CancellationToken token,Func<DatabaseCatalog,GeneratedDatabase,IDatabaseImportSource>? initialDataSource=null){
        if(this.versioned is not null){await this.BootstrapVersioned(definitions,token,initialDataSource).ConfigureAwait(false);return;}
        if(!this.NeedsBootstrap||this.recovered||this.databases.Count!=0||this.generationStore is null){throw new InvalidOperationException("Only a new Gateway can bootstrap its initial databases.");}
        if(definitions.Count==0||definitions.Count>1024||definitions.Select(static item=>item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=definitions.Count){throw new ArgumentException("Initial database names must be unique and bounded.",nameof(definitions));}
        this.journal.Replay((id,lsn,metadata,entries)=>{foreach(var entry in entries){}});var owner=new GatewayTransactionOwner(Guid.NewGuid(),0);var changes=new List<GenerationChange>();var ready=new List<GatewayPreparedGeneration>();
        try{
            for(var index=0;index<definitions.Count;index++){
                using var candidate=await this.generationStore.BuildAsync(GatewaySchemaCatalog.Create(definitions[index]),this.Profile(index+1),owner,token).ConfigureAwait(false);
                var source=initialDataSource?.Invoke(definitions[index],candidate.Artifact.Generated);using var sourceLifetime=source as IDisposable;if(source is not null){TypedGenerationMigrator.Import(candidate.Loaded,source,token);}else{using var snapshot=candidate.Loaded.Database.BeginUnpublishedSnapshot();snapshot.Complete();}
                using var transaction=candidate.Loaded.Database.BeginGatewayTransaction(owner);if(source is not null){candidate.Schema=candidate.Loaded.Adapter.CaptureImportedIdentityValues(candidate.Schema);}var prepared=candidate.Seal(transaction,token);ready.Add(prepared);changes.Add(new(definitions[index].Name,1,prepared.Reference));
            }
            this.failureInjection?.Invoke(GatewayCommitPhase.BeforeWal,0);
            // 初期世代はまだ公開されていない。全世代の参照を共通確定へ渡し、復旧後にまとめて公開する。
            using var commit=new CommonCommitChanges(static()=>[],static _=>{});
            await this.commitCoordinator.CommitExternalAsync(owner.TransactionId,commit,JsonSerializer.SerializeToUtf8Bytes(new GenerationCommit("seamless-gateway-generations-v1",changes.ToArray())),token).ConfigureAwait(false);this.failureInjection?.Invoke(GatewayCommitPhase.WalDurable,0);
        }finally{foreach(var item in ready){item.Dispose();}}
    }
    private void RecoverGenerationRegistrations(){
        var states=(this.checkpoint?.Generations??[]).ToDictionary(static item=>item.Current.Reference.DatabaseId);var nativeStates=(this.checkpoint?.NativeQueries??[]).ToDictionary(static item=>item.ModuleId,StringComparer.Ordinal);
        this.ApplyValues(this.checkpoint?.Values??[]);
        this.journal.Replay((id,lsn,metadata,entries)=>{
            if(!metadata.IsEmpty){
                if(this.RestoreValueCommit(metadata)){foreach(var entry in entries){throw new InvalidDataException("A value reservation cannot contain data changes.");}return;}
                var commit=JsonSerializer.Deserialize<GenerationCommit>(metadata.Span)??throw new InvalidDataException("Missing generation commit metadata.");
                if(commit.Format!="seamless-gateway-generations-v1"||commit.Changes.Length==0&&(commit.NativeChanges?.Length??0)==0||commit.Changes.Length>1024||commit.Changes.Select(static item=>item.Reference.DatabaseId).Distinct().Count()!=commit.Changes.Length){throw new InvalidDataException("Invalid generation commit metadata.");}
                if(commit.NativeChanges is{} native){if(native.Length>1024||native.Select(static change=>change.ModuleId).Distinct(StringComparer.Ordinal).Count()!=native.Length){throw new InvalidDataException("Invalid native publication metadata.");}foreach(var change in native){nativeStates.TryGetValue(change.ModuleId,out var previous);nativeStates[change.ModuleId]=new(change.ModuleId,change.Reference,previous?.Current);}}
                foreach(var change in commit.Changes){
                    states.TryGetValue(change.Reference.DatabaseId,out var previous);if(change.Epoch<=0||previous is not null&&(previous.Current.Name!=change.Name||previous.Current.Epoch>=change.Epoch)){throw new InvalidDataException("Generation identity or epoch regressed.");}
                    states[change.Reference.DatabaseId]=new(new(change.Name,change.Epoch,lsn,change.Reference),previous?.Current);
                }
            }
            foreach(var entry in entries){}
        },this.checkpoint?.Lsn??0);
        if(states.Count==0){return;}if(this.generationStore is null||this.generationRegistry is null){throw new InvalidDataException("The common WAL requires its generation store.");}
        if(states.Values.Select(static state=>state.Current.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=states.Count){throw new InvalidDataException("Duplicate published database names.");}
        foreach(var state in states.Values.OrderBy(static item=>item.Current.Reference.DatabaseId)){
            var descriptor=state.Current;var id=descriptor.Reference.DatabaseId;if(this.databases.ContainsKey(id)){throw new InvalidDataException("A generated database cannot replace an independently opened backing.");}
            this.nextGenerationEpoch=Math.Max(this.nextGenerationEpoch,descriptor.Epoch);
            var prepared=this.generationStore.Open(descriptor.Reference,this.WalId,descriptor.PublicationLsn);var published=new PublishedGeneration(state,prepared);this.published.Add(id,published);
            try{
                var registration=GenerationRegistration(descriptor.Name,prepared.Loaded.Adapter,prepared.Artifact,prepared.Schema);this.generationRegistry.Register(registration.Name,registration.Adapter,registration.IsReadOnly,registration.SchemaFingerprint,registration.Views,registration.Routines,registration.DefaultCollation);
            }catch{this.published.Remove(id);prepared.Dispose();throw;}
        }
        this.RestoreNativePublications(nativeStates.Values);
    }
    private sealed record GenerationChange(string Name,long Epoch,GatewayGenerationReference Reference);
    private sealed record GenerationCommit(string Format,GenerationChange[] Changes,NativeChange[]? NativeChanges=null,GatewayGenerationReference[]? Retained=null);
    private sealed record PublishedGenerationDescriptor(string Name,long Epoch,long PublicationLsn,GatewayGenerationReference Reference);
    private sealed record PublishedGenerationState(PublishedGenerationDescriptor Current,PublishedGenerationDescriptor? Previous);
    private sealed record PublishedGeneration(PublishedGenerationState State,GatewayPreparedGeneration Prepared);
}
