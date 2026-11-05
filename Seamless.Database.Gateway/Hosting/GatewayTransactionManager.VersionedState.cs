using System.Text.Json;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
using ExecutionStore=Seamless.Execution.Store;
namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    private VersionedState? versioned;
    internal bool IsVersioned=>this.versioned is not null;
    internal GatewayVersionedBinding CreateVersionedBinding(GatewayDatabaseProfile profile)=>this.versioned!.CreateBinding(profile);
    private readonly record struct CatalogueRow(string Key,string Value);
    private sealed class VersionedState:IDisposable{
        private readonly GatewayTransactionManager owner;
        private readonly ExecutionScope resources;
        private readonly ExecutionStore store;
        private readonly TransactionCoordinatorOptions options;
        private readonly Dictionary<int,VersionedTransactionParticipantGroup> groups=[];
        internal readonly Dictionary<string,GenerationHandle> Generations=new(StringComparer.Ordinal);
        internal readonly Dictionary<string,GatewayNativeQueryVersion> NativeVersions=new(StringComparer.Ordinal);
        internal readonly TransactionCoordinator Coordinator;
        internal readonly VersionedTransactionalTable<string,CatalogueRow> Catalogue;
        internal readonly CatalogueCheckpoint Saved;
        internal bool Started;
        internal VersionedState(GatewayTransactionManager owner,DatabaseContainerOptions databaseOptions){
            this.owner=owner;this.resources=new(new(){ParentBudget=owner.budget,MemoryLimitBytes=databaseOptions.ManagedMemoryLimitBytes,TemporaryStorageLimitBytes=databaseOptions.TemporaryStorageLimitBytes,TemporaryDirectory=Path.Combine(owner.directory,"catalogue-work"),MaximumQueryWorkers=databaseOptions.MaximumQueryWorkers});
            try{
                this.options=new(){Concurrency=databaseOptions.TransactionConcurrency,History=databaseOptions.TransactionHistory!,ParentBudget=owner.budget};this.Coordinator=new(null,null,owner.journal,this.options);
                this.store=ExecutionStore.Open(Path.Combine(owner.directory,"catalogue-mvcc-v1"),parentBudget:this.resources.Budget);this.Saved=new(owner.WalId);this.store.Register(this.Saved);
                if(this.store.CurrentCheckpoint is not null&&!this.Saved.Loaded){throw new InvalidDataException("The Gateway catalogue checkpoint is missing.");}
                const string identity="gateway-catalogue-v1|canonical";var data=this.store.CreateTsavoriteHistory("catalogue",new(){IndexSize=64,PageSize=4096,SegmentSize=65536,LogMemorySize=65536},VersionedTransactionalTable<string,CatalogueRow>.StorageIdentity(identity,VersionedRecordEncoding.Canonical));this.Catalogue=new(0,1,identity,data,this.resources,Math.Min(databaseOptions.TransactionMemoryLimit,1<<20),static row=>row.Key);this.Coordinator.Register(this.Catalogue);
                if(this.store.CurrentCheckpoint is null){if(owner.journal.DurableLsn!=0){throw new InvalidDataException("A committed Gateway WAL requires its original catalogue snapshot.");}using(var initial=this.Catalogue.BeginUnpublishedSnapshot()){initial.Complete();}this.Catalogue.CloseForMaintenance();this.store.Save();this.Catalogue.ReopenAfterMaintenance();}
                this.Coordinator.ConfigureMaintenance(owner.SaveVersionedCheckpoint);
            }catch{this.Catalogue?.Dispose();this.store?.Dispose();this.Coordinator?.DisposeAsync().AsTask().GetAwaiter().GetResult();this.resources.Dispose();throw;}
        }
        internal GatewayVersionedBinding CreateBinding(GatewayDatabaseProfile profile){
            lock(this.owner.gate){
                ObjectDisposedException.ThrowIf(this.owner.disposed,this.owner);
                if(this.owner.recovered){var scope=DatabaseRegistry.CurrentSqlSession?.SnapshotTransaction?.Scope??throw new InvalidOperationException("A live generation must be opened by its owning Gateway schema transaction.");scope.ValidateOwner(this.Coordinator);if(scope.IsReadOnly){throw new InvalidOperationException("A read-only snapshot cannot attach a new generation.");}}
                if(profile.WalId!=this.owner.WalId){throw new InvalidDataException("The generation belongs to another common WAL.");}
                if(this.Generations.ContainsKey(profile.GenerationId.ToString("N"))){throw new InvalidOperationException("The physical generation is already open.");}
                if(!this.groups.TryGetValue(profile.DatabaseId,out var group)){if(this.Started){throw new InvalidOperationException("Register every logical database before starting its common domain.");}group=new(profile.DatabaseId,0,"gateway-database-"+profile.DatabaseId,this.resources.Budget);this.Coordinator.Register(group);this.groups.Add(profile.DatabaseId,group);}
                var memory=this.resources.Budget.ReserveMemory(512);GatewayVersionedBinding? binding=null;
                try{binding=new(this.Coordinator,group,profile.WalId,profile.GenerationId,this.options,this.resources.Budget,()=>this.owner.recovered&&!this.owner.disposed);this.Generations.Add(profile.GenerationId.ToString("N"),new(binding,memory));return binding;}catch{binding?.Dispose();memory.Dispose();throw;}
            }
        }
        internal GatewayPreparedGeneration Track(GatewayPreparedGeneration prepared){lock(this.owner.gate){var entry=this.Generations[prepared.Reference.GenerationId];if(entry.Prepared is not null&&!ReferenceEquals(entry.Prepared,prepared)){throw new InvalidOperationException("A generation has two physical owners.");}entry.Prepared=prepared;return prepared;}}
        internal GatewayPreparedGeneration Open(GatewayGenerationReference reference,long publicationLsn=0){lock(this.owner.gate){if(this.Generations.TryGetValue(reference.GenerationId,out var existing)&&existing.Prepared is{} prepared){if(prepared.Reference!=reference){throw new InvalidDataException("The generation descriptor changed.");}return prepared;}}return this.Track(this.owner.generationStore!.Open(reference,this.owner.WalId,publicationLsn));}
        internal static string DatabaseKey(int id)=>"D/"+id.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        internal static string NativeKey(string id)=>"N/"+id;
        internal static string NativeDependenciesKey(int id)=>"ND/"+id.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        internal void ObserveNativeDependencies(TransactionScope scope,int id)=>this.Catalogue.For(scope).TryGetValue(NativeDependenciesKey(id),out _);
        internal void TouchNativeDependencies(TransactionScope scope,int id)=>this.Catalogue.For(scope)[NativeDependenciesKey(id)]=new(NativeDependenciesKey(id),scope.Id.ToString("N"));
        internal GenerationChange ReadDatabase(TransactionScope scope,int id){var rows=this.Catalogue.For(scope);if(!rows.TryGetValue(DatabaseKey(id),out var row)){throw new InvalidDataException("The logical database has no visible generation.");}return JsonSerializer.Deserialize<GenerationChange>(row.Value)??throw new InvalidDataException("The catalogue generation is missing.");}
        internal GatewayNativeQueryReference? ReadNative(TransactionScope scope,string id){if(!this.Catalogue.For(scope).TryGetValue(NativeKey(id),out var row)){return null;}return JsonSerializer.Deserialize<GatewayNativeQueryReference>(row.Value)??throw new InvalidDataException("The catalogue native reference is missing.");}
        internal void WriteDatabase(TransactionScope scope,GenerationChange change)=>this.Catalogue.For(scope)[DatabaseKey(change.Reference.DatabaseId)]=new(DatabaseKey(change.Reference.DatabaseId),JsonSerializer.Serialize(change));
        internal void WriteNative(TransactionScope scope,string id,GatewayNativeQueryReference reference)=>this.Catalogue.For(scope)[NativeKey(id)]=new(NativeKey(id),JsonSerializer.Serialize(reference));
        internal void Start(long lsn,long version){this.Coordinator.RecoverVersioned(lsn,Math.Max(1,version));this.Started=true;}
        internal void Save(long lsn,long version){this.Saved.Lsn=lsn;this.Saved.CommitVersion=version;this.store.Save();}
        public void Dispose(){
            this.Coordinator.StopAccepting();using(this.Coordinator.EnterReadForShutdown()){}List<Exception>? errors=null;
            foreach(var entry in this.Generations.Values){try{entry.Binding.AllowDisposeForShutdown();entry.Prepared?.Dispose();entry.Binding.Dispose();entry.Memory.Dispose();}catch(Exception error){(errors??=[]).Add(error);}}
            this.Generations.Clear();foreach(var native in this.NativeVersions.Values){try{native.Dispose();}catch(Exception error){(errors??=[]).Add(error);}}this.NativeVersions.Clear();
            foreach(var group in this.groups.Values){try{group.Dispose();}catch(Exception error){(errors??=[]).Add(error);}}this.groups.Clear();this.Catalogue.Dispose();this.store.Dispose();this.resources.Dispose();if(errors is not null){throw new AggregateException("Gateway versioned storage cleanup failed.",errors);}
        }
    }
    private DatabaseRegistration VersionedRegistration(GenerationHandle handle,string name){lock(this.gate){return handle.Registration??=this.GenerationRegistration(name,handle.Prepared!.Loaded.Adapter,handle.Prepared.Artifact,handle.Prepared.Schema);}}
    private sealed class GenerationHandle(GatewayVersionedBinding binding,ResourceReservation memory){internal GatewayVersionedBinding Binding{get;}=binding;internal ResourceReservation Memory{get;}=memory;internal GatewayPreparedGeneration? Prepared;internal DatabaseRegistration? Registration;internal bool Published,Retired,Abandoned;}
    private sealed class CatalogueCheckpoint(Guid walId):IStoreComponent{
        internal long Lsn,CommitVersion=1;internal bool Loaded;
        public string Name=>"gateway-catalogue-checkpoint-v1";
        public string Save()=>JsonSerializer.Serialize(new CatalogueSave(1,walId,this.Lsn,this.CommitVersion));
        public void Load(string state){var value=JsonSerializer.Deserialize<CatalogueSave>(state)??throw new InvalidDataException("Missing catalogue checkpoint.");if(value.Version!=1||value.WalId!=walId||value.Lsn<0||value.CommitVersion<1){throw new InvalidDataException("Invalid catalogue checkpoint identity.");}this.Lsn=value.Lsn;this.CommitVersion=value.CommitVersion;this.Loaded=true;}
        public void Flush(){}
        public void Dispose(){}
    }
    private sealed record CatalogueSave(int Version,Guid WalId,long Lsn,long CommitVersion);
    private ValueTask SaveVersionedCheckpoint()=>this.journal.CheckpointAsync((walId,lsn)=>{
        var state=this.versioned!;var version=this.commitCoordinator.GetDomainDiagnostics().PublishedVersion;
        foreach(var handle in state.Generations.Values){if(handle.Binding.NeedsCheckpoint){handle.Prepared!.Loaded.Database.SaveGatewayVersionedCheckpoint(lsn,version);this.failureInjection?.Invoke(GatewayCommitPhase.DatabaseCheckpointed,handle.Prepared.Reference.DatabaseId);}}
        state.Save(lsn,version);ValueReservation[] values;lock(this.valueGate){values=this.values.Values.ToArray();}
        var checkpoint=new CheckpointData(2,walId,lsn,this.databases.Values.Select(static registration=>new DatabaseIdentity(registration.Id,registration.Name)).OrderBy(static item=>item.Id).ToArray(),this.published.Values.Select(static item=>item.State).ToArray(),values,this.nativePublished.Values.Select(static item=>item.State).ToArray(),version,state.Generations.Where(pair=>pair.Value.Prepared is not null&&!this.retiringGenerations.Contains(pair.Key)).Select(static pair=>pair.Value.Prepared!.Reference).ToArray());
        var payload=JsonSerializer.SerializeToUtf8Bytes(checkpoint);var envelope=new CheckpointEnvelope("seamless-gateway-checkpoint-v2",Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(payload)),payload);var path=Path.Combine(this.directory,"common-checkpoint.json");using(var output=new FileStream(path+".new",FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,envelope);output.Flush(true);}File.Move(path+".new",path,true);this.checkpoint=checkpoint;this.CaptureVersionedCollectionPlans();return ValueTask.CompletedTask;
    });
}
