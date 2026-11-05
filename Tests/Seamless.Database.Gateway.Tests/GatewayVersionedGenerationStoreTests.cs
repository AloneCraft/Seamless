using System.Text.Json;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Rdbms;
using Seamless.Execution;
using Seamless.Transactions;
namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayVersionedGenerationStoreTests{
    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedSeedAndLiveBindingRecoverThroughTheCommonWal(bool checkpoint){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var path=Path.Combine(data,"versioned-generation-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);GatewayGenerationReference reference;Guid committed;
        await using(var fixture=new Fixture(path,compiler)){
            var schema=GatewaySchemaCatalog.Create(new("typed",[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"N",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("pk",[new("Id",false)]))])],"Japanese_CI_AS"));
            var owner=new GatewayTransactionOwner(Guid.NewGuid(),1);using var candidate=await fixture.Store.BuildAsync(schema,new(fixture.WalId,1),owner);
            Assert.Equal(256,TypedGenerationMigrator.Import(candidate.Loaded,new SeedSource()));
            var prepared=fixture.Track(candidate.SealVersioned());reference=prepared.Reference;Assert.Equal(TransactionOutcomeKind.Unknown,(await fixture.Owner.GetOutcomeAsync(owner.TransactionId)).Kind);fixture.Start();
            await using(var tx=await fixture.Owner.BeginAsync()){committed=tx.Id;using var execution=prepared.Loaded.Database.EnterGatewayVersionedExecution(new(tx.Id,1),tx);using var session=new SessionContext(1,"typed");Assert.Equal(1,prepared.Loaded.Adapter.ExecuteInsert("dbo.Items",new Dictionary<string,object?>{{"Id",257},{"N",2570}},session));await tx.CommitAsync();}
            if(checkpoint){await fixture.Owner.CheckpointVersionedAsync();}
            await using(var tx=await fixture.Owner.BeginAsync()){using var execution=prepared.Loaded.Database.EnterGatewayVersionedExecution(new(tx.Id,1),tx);using var session=new SessionContext(1,"typed");prepared.Loaded.Adapter.ExecuteInsert("dbo.Items",new Dictionary<string,object?>{{"Id",258},{"N",2580}},session);await tx.CommitAsync();}
        }
        await using(var fixture=new Fixture(path,compiler)){
            var prepared=fixture.Track(fixture.Store.Open(reference,fixture.WalId,0));fixture.Start();await using var read=await fixture.Owner.BeginReadOnlyAsync();using var execution=prepared.Loaded.Database.EnterGatewayVersionedExecution(new(read.Id,2),read);using var session=new SessionContext(2,"typed");var rows=prepared.Loaded.Adapter.ExecuteSelect("dbo.Items",null,null,session).Rows.ToArray();Assert.Equal(258,rows.Length);Assert.Contains(rows,static row=>Equals(row[0],256)&&Equals(row[1],2560));Assert.Contains(rows,static row=>Equals(row[0],258)&&Equals(row[1],2580));Assert.Equal(TransactionOutcomeKind.Committed,(await fixture.Owner.GetOutcomeAsync(committed)).Kind);await read.CommitAsync();
        }
    }
    private sealed class SeedSource:IDatabaseImportSource{
        public IEnumerable<TRow> ReadTable<TRow>(global::Seamless.Database.System.Keys.DatabaseObject key){var constructor=typeof(TRow).GetConstructors().Single(static item=>item.GetParameters().Length==2);for(var id=1;id<=256;id++){yield return(TRow)constructor.Invoke([id,id*10]);}}
    }
    private sealed class Fixture:IAsyncDisposable{
        private readonly ExecutionScope resources;
        private readonly PreparedTransactionJournal journal;
        private readonly VersionedTransactionParticipantGroup group;
        private readonly List<GatewayVersionedBinding> bindings=[];
        private readonly List<GatewayPreparedGeneration> generations=[];
        private readonly string checkpointPath;
        private bool ready;
        internal readonly TransactionCoordinator Owner;
        internal readonly GatewayGenerationStore Store;
        internal Guid WalId=>this.journal.WalId;
        internal Fixture(string path,GeneratedAssemblyBuilder compiler){
            this.checkpointPath=Path.Combine(path,"common-state.json");this.resources=new(new(){MemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=64<<20,TemporaryDirectory=Path.Combine(path,"work")});this.journal=new(Path.Combine(path,"wal"),new(){MaxFrameBytes=4096,QueueBytes=16384,SegmentBytes=65536,CatalogIdentity="versioned-generation-tests"},this.resources.Budget);
            var history=new TransactionHistoryOptions{MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=4<<20,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)};var options=new TransactionCoordinatorOptions{Concurrency=TransactionConcurrencyMode.Optimistic,History=history,ParentBudget=this.resources.Budget};this.Owner=new(null,null,this.journal,options);this.group=new(1,0,"typed",this.resources.Budget);this.Owner.Register(this.group);
            this.Store=new(Path.Combine(path,"generations"),compiler,new(){TransactionConcurrency=options.Concurrency,TransactionHistory=history,ParentBudget=this.resources.Budget,IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,ManagedMemoryLimitBytes=8<<20,TemporaryStorageLimitBytes=16<<20,TransactionMemoryLimit=1},this.resources.Budget,profile=>{var binding=new GatewayVersionedBinding(this.Owner,this.group,this.WalId,profile.GenerationId,options,this.resources.Budget,()=>this.ready);this.bindings.Add(binding);return binding;});
            this.Owner.ConfigureMaintenance(()=>this.journal.CheckpointAsync((walId,lsn)=>{var version=this.Owner.GetDomainDiagnostics().PublishedVersion;foreach(var generation in this.generations){generation.Loaded.Database.SaveGatewayVersionedCheckpoint(lsn,version);}using(var file=new FileStream(this.checkpointPath+".new",FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,new Checkpoint(walId,lsn,version));file.Flush(true);}File.Move(this.checkpointPath+".new",this.checkpointPath,true);return ValueTask.CompletedTask;}));
        }
        internal GatewayPreparedGeneration Track(GatewayPreparedGeneration generation){this.generations.Add(generation);return generation;}
        internal void Start(){var checkpoint=new Checkpoint(this.WalId,0,0);if(File.Exists(this.checkpointPath)){checkpoint=JsonSerializer.Deserialize<Checkpoint>(File.ReadAllBytes(this.checkpointPath))!;}if(checkpoint.WalId!=this.WalId||this.generations.Any(generation=>generation.Loaded.Database.GatewayVersionedCheckpoint.Lsn<checkpoint.Lsn)){throw new InvalidDataException("Incomplete common checkpoint.");}this.Owner.RecoverVersioned(checkpoint.Lsn,checkpoint.Version);this.ready=true;}
        public async ValueTask DisposeAsync(){this.Owner.StopAccepting();foreach(var binding in this.bindings){binding.AllowDisposeForShutdown();}foreach(var generation in this.generations){generation.Dispose();}foreach(var binding in this.bindings){binding.Dispose();}await this.Owner.DisposeAsync();this.group.Dispose();await this.journal.DisposeAsync();this.resources.Dispose();}
        private sealed record Checkpoint(Guid WalId,long Lsn,long Version);
    }
}
