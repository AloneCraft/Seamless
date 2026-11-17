using System.Text.Json;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Transactions;
namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    private async Task BootstrapVersioned(IReadOnlyList<DatabaseCatalog> definitions,CancellationToken token,Func<DatabaseCatalog,GeneratedDatabase,IDatabaseImportSource>? initialDataSource){
        if(!this.NeedsBootstrap||definitions.Count is<1 or>1024||definitions.Select(static item=>item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=definitions.Count){throw new InvalidOperationException("A fresh Gateway requires unique initial database definitions.");}
        this.journal.Replay(static(id,lsn,metadata,entries)=>{foreach(var entry in entries){}});var owner=new GatewayTransactionOwner(Guid.NewGuid(),0);var changes=new List<GenerationChange>();
        for(var index=0;index<definitions.Count;index++){
            using var candidate=await this.generationStore!.BuildAsync(GatewaySchemaCatalog.Create(definitions[index]),this.Profile(index+1),owner,token).ConfigureAwait(false);var source=initialDataSource?.Invoke(definitions[index],candidate.Artifact.Generated);using var lifetime=source as IDisposable;
            if(source is not null){TypedGenerationMigrator.Import(candidate.Loaded,source,token);candidate.Schema=candidate.Loaded.Database.ReadGatewayVersionedSeed(owner,()=>candidate.Loaded.Adapter.CaptureImportedIdentityValues(candidate.Schema));}else{using var initial=candidate.Loaded.Database.BeginUnpublishedSnapshot();initial.Complete();}
            var prepared=this.versioned!.Track(candidate.SealVersioned(token));changes.Add(new(definitions[index].Name,1,prepared.Reference));
        }
        this.versioned!.Start(0,1);await using var scope=await this.commitCoordinator.BeginOwnedAsync(owner.TransactionId,false,token).ConfigureAwait(false);foreach(var change in changes){this.versioned.WriteDatabase(scope,change);}this.failureInjection?.Invoke(GatewayCommitPhase.BeforeWal,0);
        await scope.CommitOwnedAsync(JsonSerializer.SerializeToUtf8Bytes(new GenerationCommit("seamless-gateway-generations-v2",changes.ToArray(),Retained:changes.Select(static change=>change.Reference).ToArray())),_=>this.failureInjection?.Invoke(GatewayCommitPhase.WalDurable,0),token).ConfigureAwait(false);
    }
    private void RecoverVersionedGateway(){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(this.recovered){return;}var state=this.versioned!;var published=(this.checkpoint?.Generations??[]).ToDictionary(static item=>item.Current.Reference.DatabaseId);var natives=(this.checkpoint?.NativeQueries??[]).ToDictionary(static item=>item.ModuleId,StringComparer.Ordinal);var references=(this.checkpoint?.RetainedGenerations??[]).ToDictionary(static item=>item.GenerationId,StringComparer.Ordinal);this.ApplyValues(this.checkpoint?.Values??[]);
            foreach(var item in published.Values){references[item.Current.Reference.GenerationId]=item.Current.Reference;}
            // checkpointに含まれた世代だけが、そのLSN以上のsnapshotを必要とする。
            // 後続WALで追加された世代は、封印済みseedと後続の共通WALから復旧する。
            var checkpointGenerations=references.Keys.ToHashSet(StringComparer.Ordinal);
            this.journal.Replay((id,lsn,metadata,entries)=>{
                if(!metadata.IsEmpty&&!this.RestoreValueCommit(metadata)){
                    var commit=JsonSerializer.Deserialize<GenerationCommit>(metadata.Span)??throw new InvalidDataException("Missing versioned generation metadata.");if(commit.Format!="seamless-gateway-generations-v2"||commit.Changes.Length>1024||(commit.NativeChanges?.Length??0)>1024||(commit.Retained?.Length??0)>4096){throw new InvalidDataException("Invalid versioned generation metadata.");}
                    foreach(var reference in commit.Retained??[]){if(references.TryGetValue(reference.GenerationId,out var previous)&&previous!=reference){throw new InvalidDataException("A generation descriptor changed in the common WAL.");}references[reference.GenerationId]=reference;}
                    foreach(var change in commit.Changes){published.TryGetValue(change.Reference.DatabaseId,out var previous);if(change.Epoch<=0||previous is not null&&(previous.Current.Name!=change.Name||previous.Current.Epoch>=change.Epoch)){throw new InvalidDataException("The generation epoch regressed.");}references[change.Reference.GenerationId]=change.Reference;published[change.Reference.DatabaseId]=new(new(change.Name,change.Epoch,lsn,change.Reference),previous?.Current);}
                    foreach(var native in commit.NativeChanges??[]){natives.TryGetValue(native.ModuleId,out var previous);natives[native.ModuleId]=new(native.ModuleId,native.Reference,previous?.Current);}
                }
                foreach(var entry in entries){}
            },this.checkpoint?.Lsn??0);
            foreach(var reference in references.Values){state.Open(reference);}
            foreach(var item in published.Values.OrderBy(static item=>item.Current.Reference.DatabaseId)){
                var current=item.Current;var prepared=state.Generations[current.Reference.GenerationId].Prepared!;state.Generations[current.Reference.GenerationId].Published=true;this.nextGenerationEpoch=Math.Max(this.nextGenerationEpoch,current.Epoch);this.published.Add(current.Reference.DatabaseId,new(item,prepared));var registration=this.VersionedRegistration(state.Generations[current.Reference.GenerationId],current.Name);this.generationRegistry!.Register(registration.Name,registration.Adapter,registration.IsReadOnly,registration.SchemaFingerprint,registration.Views,registration.Routines,registration.DefaultCollation);
            }
            foreach(var handle in state.Generations.Values){handle.Retired=!handle.Published;}
            var floor=this.checkpoint?.Lsn??0;if(state.Saved.Lsn<floor||state.Generations.Any(entry=>checkpointGenerations.Contains(entry.Key)&&entry.Value.Prepared!.Loaded.Database.GatewayVersionedCheckpoint.Lsn<floor)){throw new InvalidDataException("A common checkpoint is missing a participating store snapshot.");}if(state.Saved.Lsn>this.journal.DurableLsn){throw new InvalidDataException("The catalogue checkpoint is ahead of the common WAL.");}
            state.Start(floor,this.checkpoint?.CommitVersion??1);var verification=this.commitCoordinator.BeginOwnedAsync(Guid.NewGuid(),true).AsTask().GetAwaiter().GetResult();try{foreach(var item in published.Values){var actual=state.ReadDatabase(verification,item.Current.Reference.DatabaseId);if(actual.Reference!=item.Current.Reference||actual.Epoch!=item.Current.Epoch||actual.Name!=item.Current.Name){throw new InvalidDataException("The versioned catalogue and publication metadata disagree.");}}verification.CommitAsync().AsTask().GetAwaiter().GetResult();}finally{verification.DisposeAsync().AsTask().GetAwaiter().GetResult();}this.RestoreNativePublications(natives.Values);foreach(var item in this.nativePublished.Values){state.NativeVersions[item.Version.Reference.Id]=item.Version;}this.recovered=true;
        }
    }
    private void StageVersionedSchema(SessionContext session,string database,GatewaySchemaCatalog schema,MigrationContext context){
        schema.Validate();var transaction=this.Own(session);var registration=this.generationRegistry!.Resolve(database);var adapter=(InProcessDatabaseAdapter)registration.Adapter;var id=adapter.Database.Options.GatewayProfile!.DatabaseId;if(schema.Definition.Name!=registration.Name){throw new InvalidDataException("DDL cannot change the logical database identity.");}
        this.versioned!.ObserveNativeDependencies(transaction.Scope!,id);this.AcquireNativeSchemaDependencies(session,database);this.EnsureVersionedWrite(session,adapter);var source=transaction.Participants[id];source.SchemaLease??=this.Acquire(session,transaction.Id,id,DatabaseLockMode.Schema);GatewayPreparedGeneration? prepared=null;
        try{
            this.failureInjection?.Invoke(GatewayCommitPhase.BeforeCandidateBuild,id);using var candidate=this.generationStore!.BuildAsync(schema,this.Profile(id),new(transaction.Id,session.SessionId),session.CancellationToken).GetAwaiter().GetResult();candidate.Loaded.Adapter.ConfigureComputed(schema.Definition,this.generationRegistry);var (plans,created)=MigrationPlans(source.Prepared!.Schema,source.Prepared.Loaded.Generated,schema,candidate.Artifact.Generated);var rowIds=plans.SelectMany(plan=>plan.Plan.TargetColumns.Where(column=>column.Id==long.MaxValue-plan.Plan.TableId&&!plan.Plan.SourceColumns.Any(old=>old.Id==column.Id))).ToDictionary(static column=>column.Id,static _=>0L);var effective=new MigrationContext(column=>{if(rowIds.TryGetValue(column,out var last)){return rowIds[column]=checked(last+1);}return context.EvaluateDefault(column);},session.CancellationToken);
            this.failureInjection?.Invoke(GatewayCommitPhase.CandidateBuilt,id);TypedGenerationMigrator.Prepare(source.Prepared.Loaded,candidate.Loaded,plans,effective,created);this.failureInjection?.Invoke(GatewayCommitPhase.CandidateCopied,id);prepared=this.versioned!.Track(candidate.SealVersioned(session.CancellationToken));prepared.Loaded.Adapter.GatewayTransactions=this;
            var next=new GatewayParticipant(prepared.Loaded.Adapter,EmptyLease.Instance){Prepared=prepared,KeepPrepared=true,GenerationChanged=true,Written=true,Version=checked(++transaction.NextVersion),Epoch=Interlocked.Increment(ref this.nextGenerationEpoch)};next.Registration=this.VersionedRegistration(this.versioned.Generations[prepared.Reference.GenerationId],registration.Name);source.VersionedContext?.Dispose();source.VersionedContext=null;next.VersionedContext=next.Adapter.Database.EnterGatewayVersionedExecution(new(transaction.Id,session.SessionId),transaction.Scope!);transaction.RetiredParticipants.Add(source);transaction.Participants[id]=next;this.Resize(transaction);session.RefreshTransactionContext();this.failureInjection?.Invoke(GatewayCommitPhase.GenerationSealed,id);this.ValidateConstraints(session,database);this.RebuildNativeForSchema(session,database);
        }catch{if(prepared is not null&&!transaction.Participants.Values.Concat(transaction.RetiredParticipants).Any(participant=>ReferenceEquals(participant.Prepared,prepared))){lock(this.gate){this.versioned!.Generations[prepared.Reference.GenerationId].Abandoned=true;}}throw;}
    }
}
