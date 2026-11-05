using System.Text.Json;
using Seamless.Database;
using Seamless.Execution;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

public sealed class GatewayVersionedBindingTests{
    [Fact]
    public async Task CompletedExecutionCanBeReleasedFromAnotherCapturedContextWithoutLeavingItsOwnerUnusable(){
        await using var fixture=new Fixture(PathForTest());await using(var read=await fixture.Owner.BeginReadOnlyAsync()){
            var execution=fixture.A.Database.EnterGatewayVersionedExecution(new(read.Id,1),read);await read.CommitAsync();await OutsideCurrent(()=>{execution.Dispose();return Task.CompletedTask;});Assert.Throws<ObjectDisposedException>(()=>fixture.A.Database.Accounts.Count);execution.Dispose();
        }
        await using var next=await fixture.Owner.BeginReadOnlyAsync();using var active=fixture.A.Database.EnterGatewayVersionedExecution(new(next.Id,1),next);Assert.Empty(fixture.A.Database.Accounts);await next.CommitAsync();
    }
    private static string PathForTest()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"binding-"+Guid.NewGuid().ToString("N"));
    private static Task OutsideCurrent(Func<Task> action){using(ExecutionContext.SuppressFlow()){return Task.Run(action);}}
    [Fact]
    public async Task AutomaticallyAbortedReadReleasesExecutionMemoryBeforeTheCallerDisposesItsContext(){
        await using var fixture=new Fixture(PathForTest());await fixture.Owner.CheckpointVersionedAsync();var before=fixture.A.Database.ResourceUsage.MemoryBytes;
        await using var read=await fixture.Owner.BeginReadOnlyAsync();var execution=fixture.A.Database.EnterGatewayVersionedExecution(new(read.Id,1),read);Assert.Equal(before+512,fixture.A.Database.ResourceUsage.MemoryBytes);
        try{
            await OutsideCurrent(async()=>await fixture.Owner.ReclaimHistoryAsync());Assert.Equal(before,fixture.A.Database.ResourceUsage.MemoryBytes);Assert.Equal(TransactionAbortReason.HistoryPressure,Assert.Throws<TransactionAbortedException>(()=>fixture.A.Database.Accounts.Count).Reason);
        }finally{execution.Dispose();}
        Assert.Equal(before,fixture.A.Database.ResourceUsage.MemoryBytes);
    }
    [Fact]
    public async Task PrivateSeedSealsWithoutBusinessWalAndOpensAsTheLiveCommonParticipant(){
        var path=PathForTest();await using var fixture=new Fixture(path);var generationId=Guid.Parse("00000000-0000-0000-0000-000000000003");var profile=new GatewayDatabaseProfile(fixture.OwnerWalId,fixture.One.Partition){GenerationId=generationId};var owner=new GatewayTransactionOwner(Guid.NewGuid(),1);var capability=new GatewayVersionedSeed(profile,owner);GatewayPreparedSnapshot prepared;
        var options=fixture.DatabaseOptions(profile,"seed") with{GatewayVersionedSeed=capability,TransactionMemoryLimit=1};
        using(var candidate=new BankDatabase(options)){
            Assert.Throws<InvalidOperationException>(()=>candidate.SealGatewayVersionedSeed(owner));
            using(var snapshot=candidate.BeginUnpublishedSnapshot()){for(var id=1;id<=256;id++){snapshot.Insert(candidate.Accounts,new(id,id*10));}snapshot.Complete();}
            Assert.Throws<InvalidOperationException>(()=>candidate.SealGatewayVersionedSeed(new(Guid.NewGuid(),1)));Assert.Throws<InvalidOperationException>(()=>candidate.InitializeGatewayStorage());
            prepared=candidate.SealGatewayVersionedSeed(owner);Assert.True(capability.Sealed);Assert.Equal((0L,1L),candidate.GatewayVersionedCheckpoint);Assert.Throws<InvalidOperationException>(()=>candidate.SealGatewayVersionedSeed(owner));
            Assert.Equal(TransactionOutcomeKind.Unknown,(await fixture.Owner.GetOutcomeAsync(owner.TransactionId)).Kind);
        }
        Assert.Equal(64,prepared.DataSha256.Length);Assert.False(Directory.Exists(Path.Combine(path,"seed","wal-mvcc-v1")));
        // 本番カタログも基準版1から開始する。開始済みdomainへ加えるseedの版は所有取引の読取版を越えない。
        await using(var initial=await fixture.Owner.BeginAsync()){fixture.A.Database.GetTransactionCollection(initial,fixture.A.Database.Accounts).Add(new(1,1));await initial.CommitAsync();}
        foreach(var source in Directory.EnumerateFiles(Path.Combine(path,"seed"),"*",SearchOption.AllDirectories)){if(Path.GetFileName(source)=="store.lock"){continue;}var target=Path.Combine(path,"live",Path.GetRelativePath(Path.Combine(path,"seed"),source));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);}
        var live=fixture.Open(fixture.One,generationId,"live");await using(var read=await fixture.Owner.BeginReadOnlyAsync()){var rows=live.Database.GetTransactionCollection(read,live.Database.Accounts);Assert.Equal(256,rows.Count());Assert.Equal(2560,rows[256].Balance);await read.CommitAsync();}
        await using(var update=await fixture.Owner.BeginAsync()){live.Database.GetTransactionCollection(update,live.Database.Accounts)[256]=new(256,99);await update.CommitAsync();}await fixture.Owner.CheckpointVersionedAsync();
    }
    [Fact]
    public async Task OneScopeReadsTwoDatabasesAtOneVersionAndRecoversTheCommonWal(){
        var path=PathForTest();Guid id;
        await using(var fixture=new Fixture(path)){
            await using(var seed=await fixture.Owner.BeginAsync()){id=seed.Id;fixture.A.Database.GetTransactionCollection(seed,fixture.A.Database.Accounts).Add(new(1,100));fixture.B.Database.GetTransactionCollection(seed,fixture.B.Database.Accounts).Add(new(1,100));await seed.CommitAsync();}
            await using(var read=await fixture.Owner.BeginReadOnlyAsync()){
                Assert.Equal(100,fixture.A.Database.GetTransactionCollection(read,fixture.A.Database.Accounts)[1].Balance);
                await OutsideCurrent(async()=>{await using var tx=await fixture.Owner.BeginAsync();fixture.A.Database.GetTransactionCollection(tx,fixture.A.Database.Accounts)[1]=new(1,200);fixture.B.Database.GetTransactionCollection(tx,fixture.B.Database.Accounts)[1]=new(1,300);await tx.CommitAsync();});
                Assert.Equal(100,fixture.B.Database.GetTransactionCollection(read,fixture.B.Database.Accounts)[1].Balance);await read.CommitAsync();
            }
            await fixture.Owner.CheckpointVersionedAsync();
            await using(var update=await fixture.Owner.BeginAsync()){fixture.B.Database.GetTransactionCollection(update,fixture.B.Database.Accounts)[1]=new(1,350);await update.CommitAsync();}
            Assert.False(Directory.Exists(Path.Combine(path,"a","wal-mvcc-v1")));Assert.False(Directory.Exists(Path.Combine(path,"b","wal-mvcc-v1")));
        }
        await using(var fixture=new Fixture(path)){await using var read=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(200,fixture.A.Database.GetTransactionCollection(read,fixture.A.Database.Accounts)[1].Balance);Assert.Equal(350,fixture.B.Database.GetTransactionCollection(read,fixture.B.Database.Accounts)[1].Balance);Assert.Equal(TransactionOutcomeKind.Committed,(await fixture.Owner.GetOutcomeAsync(id)).Kind);await read.CommitAsync();}
    }
    [Fact]
    public async Task SeparateGenerationsHaveIndependentDependenciesAndRetirementDoesNotStopOtherDatabases(){
        await using var fixture=new Fixture(PathForTest());var next=fixture.Open(fixture.One,Guid.Parse("00000000-0000-0000-0000-000000000003"),"a-next");
        using var ready=new CountdownEvent(2);using var go=new ManualResetEventSlim();
        var writers=new[]{fixture.A,next}.Select(generation=>Task.Run(async()=>{await using var tx=await fixture.Owner.BeginAsync();generation.Database.GetTransactionCollection(tx,generation.Database.Accounts).Add(new(1,100));ready.Signal();if(!go.Wait(TimeSpan.FromSeconds(5))){throw new TimeoutException();}await tx.CommitAsync();})).ToArray();
        try{Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));}finally{go.Set();}await Task.WhenAll(writers);
        Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.Dispose());
        await fixture.A.Binding.RetireAsync();fixture.A.Database.Dispose();
        await using var other=await fixture.Owner.BeginAsync();fixture.B.Database.GetTransactionCollection(other,fixture.B.Database.Accounts).Add(new(1,500));Assert.Equal(100,next.Database.GetTransactionCollection(other,next.Database.Accounts)[1].Balance);await other.CommitAsync();
    }
    [Fact]
    public async Task BindingEnforcesCommonAdmissionAndMaintenanceOwnership(){
        await using var fixture=new Fixture(PathForTest());fixture.Ready=false;
        await using(var tx=await fixture.Owner.BeginReadOnlyAsync()){Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.GetTransactionCollection(tx,fixture.A.Database.Accounts));await tx.CommitAsync();}fixture.Ready=true;
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.A.Database.BeginTransactionAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.A.Database.CheckpointTransactionsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.A.Database.ReclaimTransactionHistoryAsync());
        Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.SaveGatewayVersionedCheckpoint(0,0));
        Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.InitializeGatewayStorage());
        Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.BeginUnpublishedSnapshot());
        await using var good=await fixture.Owner.BeginAsync();Assert.True(fixture.A.Database.GetTransactionCollection(good,fixture.A.Database.Accounts).Add(new(1,100)));await good.CommitAsync();
    }
    [Fact]
    public async Task OrdinaryTableAccessUsesTheOwnersScopeAndCommonSavepoints(){
        await using var fixture=new Fixture(PathForTest());
        await using(var tx=await fixture.Owner.BeginAsync()){
            var owner=new GatewayTransactionOwner(tx.Id,1);using var a=fixture.A.Database.EnterGatewayVersionedExecution(owner,tx);using var b=fixture.B.Database.EnterGatewayVersionedExecution(owner,tx);
            Assert.True(fixture.A.Database.Accounts.Add(new(1,100)));Assert.True(fixture.B.Database.Accounts.Add(new(1,200)));fixture.A.Database.LoadSummary(new(1,300));
            tx.CreateSavepoint("before");fixture.A.Database.Accounts[1]=new(1,90);Assert.True(fixture.B.Database.Accounts.Replace(1,new(2,180)));Assert.Equal(new[]{2},fixture.B.Database.Accounts.Keys);tx.RollbackToSavepoint("before");
            Assert.Equal(100,fixture.A.Database.Accounts[1].Balance);Assert.Equal(200,fixture.B.Database.Accounts[1].Balance);Assert.Equal(300,fixture.A.Database.Summaries[1].Balance);Assert.Equal(1,fixture.A.Database.Summaries.LongCount);Assert.Equal(new[]{1},fixture.A.Database.Summaries.Keys);Assert.Equal(400,new NativeReport().Total(fixture.A.Database));await tx.CommitAsync();
        }
        Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.Accounts.Count);
        await using(var read=await fixture.Owner.BeginReadOnlyAsync()){
            var owner=new GatewayTransactionOwner(read.Id,1);using var a=fixture.A.Database.EnterGatewayVersionedExecution(owner,read);using var b=fixture.B.Database.EnterGatewayVersionedExecution(owner,read);
            Assert.Equal(100,fixture.A.Database.Accounts[1].Balance);Assert.Equal(400,new NativeReport().Total(fixture.A.Database));
            await OutsideCurrent(async()=>{await using var update=await fixture.Owner.BeginAsync();using var execution=fixture.B.Database.EnterGatewayVersionedExecution(new(update.Id,2),update);fixture.B.Database.Accounts[1]=new(1,250);await update.CommitAsync();});
            Assert.Equal(200,Assert.Single(fixture.B.Database.Accounts).Balance);Assert.Equal(1,fixture.B.Database.Accounts.LongCount);await read.CommitAsync();
        }
    }
    [Fact]
    public async Task ReadOnlyExecutionsRejectWritesBeforeNormalizationOrNotifications(){
        await using var fixture=new Fixture(PathForTest());var normalized=0;var changed=0;
        fixture.A.Database.ConfigureGatewayTableChanges(fixture.A.Database.Accounts,row=>{normalized++;return row;},(_,_)=>changed++);
        await using(var tx=await fixture.Owner.BeginAsync()){
            using var execution=fixture.A.Database.EnterGatewayVersionedExecution(new(tx.Id,1),tx);
            using(fixture.A.Database.BeginGatewayReadOnlyExecution()){Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.Accounts.Add(new(1,100)));Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.Accounts[1]=new(1,100));Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.Accounts.Replace(1,new(2,200)));}
            Assert.Equal(0,normalized);Assert.Equal(0,changed);Assert.True(fixture.A.Database.Accounts.Add(new(1,100)));Assert.True(fixture.A.Database.Accounts.Remove(1));Assert.Equal(1,normalized);Assert.Equal(2,changed);await tx.CommitAsync();Assert.Throws<ObjectDisposedException>(()=>fixture.A.Database.Accounts.Add(new(2,200)));Assert.Equal(1,normalized);Assert.Equal(2,changed);
        }
        await using(var read=await fixture.Owner.BeginReadOnlyAsync()){using var execution=fixture.A.Database.EnterGatewayVersionedExecution(new(read.Id,1),read);Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.Accounts.Add(new(2,200)));Assert.Equal(1,normalized);await read.CommitAsync();}
    }
    [Fact]
    public async Task ExecutionRejectsForeignScopesAndStaleCapturedContexts(){
        await using var fixture=new Fixture(PathForTest());await using var foreignOwner=new TransactionCoordinator();await using(var foreign=await foreignOwner.BeginReadOnlyAsync()){Assert.Throws<ArgumentException>(()=>fixture.A.Database.EnterGatewayVersionedExecution(new(foreign.Id,1),foreign));await foreign.CommitAsync();}
        await using var tx=await fixture.Owner.BeginReadOnlyAsync();Assert.Throws<InvalidOperationException>(()=>fixture.A.Database.EnterGatewayVersionedExecution(new(Guid.NewGuid(),1),tx));
        var execution=fixture.A.Database.EnterGatewayVersionedExecution(new(tx.Id,1),tx);var context=ExecutionContext.Capture()!;execution.Dispose();
        ExecutionContext.Run(context,_=>Assert.Throws<ObjectDisposedException>(()=>fixture.A.Database.Accounts.Count),null);await tx.CommitAsync();
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    internal sealed class NativeReport{
        internal decimal Total(BankDatabase database)=>database.Accounts.GetGatewayTransactionRows().Where(static row=>row.Id>0).Sum(static row=>row.Balance)+database.Summaries.GetGatewayTransactionRows().Sum(static row=>row.Balance);
    }
    private sealed class Fixture:IAsyncDisposable{
        private readonly ExecutionScope resources;
        private readonly PreparedTransactionJournal journal;
        private readonly TransactionCoordinatorOptions options;
        private readonly ResourceBudget metadata;
        private readonly string path;
        private readonly List<Generation> generations=[];
        private bool disposed;
        internal bool Ready;
        internal readonly TransactionCoordinator Owner;
        internal readonly VersionedTransactionParticipantGroup One,Two;
        internal readonly Generation A,B;
        internal Guid OwnerWalId=>this.journal.WalId;
        internal Fixture(string path){
            this.path=path;Directory.CreateDirectory(path);this.resources=new(new(){MemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=64<<20,TemporaryDirectory=Path.Combine(path,"work")});this.metadata=this.resources.Budget.CreateChild(2<<20,0);
            this.journal=new(Path.Combine(path,"common-wal"),new(){MaxFrameBytes=4096,QueueBytes=16384,SegmentBytes=65536,CatalogIdentity="binding-tests-v1"},this.resources.Budget);
            this.options=new(){Concurrency=TransactionConcurrencyMode.Optimistic,ParentBudget=this.resources.Budget,History=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=4<<20,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};
            this.Owner=new(null,null,this.journal,this.options);this.One=new(1,0,"database-a",this.metadata);this.Two=new(2,0,"database-b",this.metadata);this.Owner.Register(this.One);this.Owner.Register(this.Two);
            this.Owner.ConfigureMaintenance(()=>this.journal.CheckpointAsync((walId,lsn)=>{
                var version=this.Owner.GetDomainDiagnostics().PublishedVersion;foreach(var generation in this.generations){if(generation.Binding.NeedsCheckpoint){generation.Database.SaveGatewayVersionedCheckpoint(lsn,version);}}
                var file=Path.Combine(this.path,"common-state.json");using(var output=new FileStream(file+".new",FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,new Checkpoint(walId,lsn,version));output.Flush(true);}File.Move(file+".new",file,true);return ValueTask.CompletedTask;
            }));
            try{
                this.A=this.Open(this.One,Guid.Parse("00000000-0000-0000-0000-000000000001"),"a");this.B=this.Open(this.Two,Guid.Parse("00000000-0000-0000-0000-000000000002"),"b");
                var checkpointFile=Path.Combine(path,"common-state.json");var checkpoint=new Checkpoint(this.journal.WalId,0,0);if(File.Exists(checkpointFile)){checkpoint=JsonSerializer.Deserialize<Checkpoint>(File.ReadAllBytes(checkpointFile))!;}
                if(checkpoint.WalId!=this.journal.WalId){throw new InvalidDataException("Checkpoint/WAL identity mismatch.");}foreach(var generation in this.generations){if(generation.Database.GatewayVersionedCheckpoint.Lsn<checkpoint.Lsn){throw new InvalidDataException("Incomplete common checkpoint.");}}
                this.Owner.RecoverVersioned(checkpoint.Lsn,checkpoint.Version);this.Ready=true;
            }catch{this.DisposeAsync().AsTask().GetAwaiter().GetResult();throw;}
        }
        internal Generation Open(VersionedTransactionParticipantGroup group,Guid generationId,string name){
            var binding=new GatewayVersionedBinding(this.Owner,group,this.journal.WalId,generationId,this.options,this.metadata,()=>this.Ready);
            var options=this.DatabaseOptions(new(this.journal.WalId,group.Partition){GenerationId=generationId},name) with{GatewayVersionedBinding=binding};
            BankDatabase? database=null;try{database=new(options);var generation=new Generation(database,binding);this.generations.Add(generation);database.InitializeGatewayVersionedStorage();return generation;}catch{if(database is null){binding.Dispose();}throw;}
        }
        internal DatabaseContainerOptions DatabaseOptions(GatewayDatabaseProfile profile,string name)=>DatabaseContainerOptions.OnStorage(Path.Combine(this.path,name)) with{Transactions=DatabaseTransactionMode.SharedV2,TransactionConcurrency=this.options.Concurrency,TransactionHistory=this.options.History,RecordSerialization=DatabaseRecordSerialization.CanonicalV1,ParentBudget=this.resources.Budget,GatewayProfile=profile,IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,ManagedMemoryLimitBytes=8<<20,TemporaryStorageLimitBytes=16<<20,TransactionMemoryLimit=1024,TemporaryDirectory=Path.Combine(this.path,name,"work")};
        public async ValueTask DisposeAsync(){
            if(this.disposed){return;}this.disposed=true;List<Exception>? errors=null;
            foreach(var generation in this.generations){try{generation.Binding.AllowDisposeForShutdown();generation.Database.Dispose();}catch(Exception error){(errors??=[]).Add(error);}}
            this.Ready=false;try{await this.Owner.DisposeAsync();}catch(Exception error){(errors??=[]).Add(error);}this.One.Dispose();this.Two.Dispose();await this.journal.DisposeAsync();this.resources.Dispose();if(errors is not null){throw new AggregateException(errors);}
        }
        private sealed record Checkpoint(Guid WalId,long Lsn,long Version);
    }
    private sealed record Generation(BankDatabase Database,GatewayVersionedBinding Binding);
}
