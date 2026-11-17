using Seamless.Database;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

public sealed class BankVersionedTests{
    [Fact]
    public async Task SharedStoreFixedGrowthRejectsAnOversizedMemoryCommitBeforePublishingAnyOfItsRows(){
        var options=Options();options=options with{MemorySize=32768,TransactionHistory=options.TransactionHistory! with{MaxRetainedMemoryBytes=1<<20},SharedStoreOptions=new(){Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()}};
        using var db=new BankDatabase(options);
        await using(var seed=await db.BeginTransactionAsync()){seed.Accounts.Add(new(1,100));await seed.CommitAsync();}
        await using(var update=await db.BeginTransactionAsync()){
            for(var id=2;id<=1024;id++){update.Accounts.Add(new(id,id));}
            var error=await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await update.CommitAsync());
            Assert.Equal(TransactionAbortReason.ResourceLimit,error.Reason);Assert.Equal(global::Seamless.Execution.ResourceLimitKind.Memory,Assert.IsType<global::Seamless.Execution.ResourceLimitException>(error.InnerException).Kind);
        }
        await using var verify=await db.BeginReadOnlyTransactionAsync();Assert.Single(verify.Accounts);Assert.Equal(100,verify.Accounts[1].Balance);Assert.False(verify.Accounts.ContainsKey(2));await verify.CommitAsync();
    }
    private static DatabaseContainerOptions Options(string? directory=null){
        var options=DatabaseContainerOptions.InMemory;if(directory is not null){options=DatabaseContainerOptions.OnStorage(directory);}
        return options with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
            IndexSize=16,PageSize=4096,SegmentSize=1<<16,MemorySize=1<<16,TransactionMemoryLimit=1024,ManagedMemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=32<<20,
            TemporaryDirectory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"bank-work"),
            TransactionHistory=new(){MaxRetainedMemoryBytes=64<<10,MaxRetainedStorageBytes=1<<20,MaxMetadataBytes=512<<10,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=4<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}};
    }
    private static Task OutsideCurrent(Func<Task> action){using(ExecutionContext.SuppressFlow()){return Task.Run(action);}}
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnpublishedSnapshotStreamsIntoTheVersionedStoreAndSurvivesReopening(bool persistent){
        string? path=null;if(persistent){path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"snapshot-"+Guid.NewGuid().ToString("N"));}
        var options=Options(path) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,TransactionMemoryLimit=1};
        using(var db=new BankDatabase(options)){
            using(var snapshot=db.BeginUnpublishedSnapshot()){
                for(var id=1;id<=256;id++){snapshot.Insert(db.Accounts,new Account(id,id*10));}
                Assert.Equal(256,snapshot.RowCount);await Assert.ThrowsAsync<InvalidOperationException>(async()=>await db.BeginReadOnlyTransactionAsync());snapshot.Complete();
            }
            await using(var read=await db.BeginReadOnlyTransactionAsync()){Assert.Equal(256,read.Accounts.Count());Assert.Equal(2560,read.Accounts[256].Balance);Assert.Empty(read.Summaries);Assert.Equal(0,(await read.CommitAsync()).Lsn);}
            Assert.Throws<InvalidOperationException>(()=>db.BeginUnpublishedSnapshot());await db.CheckpointTransactionsAsync();
        }
        if(persistent){using var reopened=new BankDatabase(options);await using var read=await reopened.BeginReadOnlyTransactionAsync();Assert.Equal(256,read.Accounts.Count());Assert.Equal(2560,read.Accounts[256].Balance);await read.CommitAsync();}
    }
    [Fact]
    public async Task DuplicateOrAbandonedUnpublishedSnapshotCannotBecomeAUsableDatabase(){
        foreach(var duplicate in new[]{false,true}){
            using var db=new BankDatabase(Options() with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1});
            using(var snapshot=db.BeginUnpublishedSnapshot()){
                snapshot.Insert(db.Accounts,new Account(1,100));
                if(duplicate){Assert.Throws<InvalidDataException>(()=>snapshot.Insert(db.Accounts,new Account(1,999)));Assert.Throws<InvalidOperationException>(()=>snapshot.Complete());}
            }
            await Assert.ThrowsAsync<DatabaseFaultedException>(async()=>await db.BeginReadOnlyTransactionAsync());
        }
    }
    [Fact]
    public async Task GeneratedOptimisticScopesAllowDifferentKeysAndRejectConflictingWrites(){
        using var db=new BankDatabase(Options());
        await using(var seed=await db.BeginTransactionAsync()){seed.Accounts.Add(new(1,100));seed.Accounts.Add(new(2,100));await seed.CommitAsync();}
        await using(var first=await db.BeginTransactionAsync()){
            first.Accounts[1]=new(1,90);
            await OutsideCurrent(async()=>{await using var second=await db.BeginTransactionAsync();second.Accounts[2]=new(2,80);await second.CommitAsync();}).WaitAsync(TimeSpan.FromSeconds(5));
            await first.CommitAsync();
        }
        await using(var first=await db.BeginTransactionAsync()){
            Assert.Equal(90,first.Accounts[1].Balance);first.Accounts[1]=new(1,70);
            await OutsideCurrent(async()=>{await using var second=await db.BeginTransactionAsync();second.Accounts[1]=new(1,60);await second.CommitAsync();});
            Assert.Equal(TransactionAbortReason.Conflict,(await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await first.CommitAsync())).Reason);
        }
        await using var verify=await db.BeginReadOnlyTransactionAsync();Assert.Equal(60,verify.Accounts[1].Balance);Assert.Equal(80,verify.Accounts[2].Balance);
        Assert.Throws<InvalidOperationException>(()=>verify.Accounts[1]=new(1,0));
    }

    [Fact]
    public async Task PersistentVersionedDataCanBeReopenedWithExclusiveConcurrency(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"bank-"+Guid.NewGuid().ToString("N"));var options=Options(path);Guid id;
        using(var db=new BankDatabase(options)){
            await using(var seed=await db.BeginTransactionAsync()){id=seed.Id;seed.Accounts.Add(new(1,123));await seed.CommitAsync();}
            await db.CheckpointTransactionsAsync();
        }
        using(var db=new BankDatabase(options with{TransactionConcurrency=TransactionConcurrencyMode.Exclusive})){
            await using(var read=await db.BeginReadOnlyTransactionAsync()){Assert.Equal(123,read.Accounts[1].Balance);await read.CommitAsync();}
            await using(var update=await db.BeginTransactionAsync()){update.Accounts[1]=new(1,100);await update.CommitAsync();}
            Assert.Equal(TransactionOutcomeKind.Committed,(await db.GetTransactionOutcomeAsync(id)).Kind);
        }
        using(var db=new BankDatabase(options)){await using var read=await db.BeginReadOnlyTransactionAsync();Assert.Equal(100,read.Accounts[1].Balance);}
        using(var incompatible=new BankDatabase(options with{TransactionHistory=null,TransactionConcurrency=TransactionConcurrencyMode.Exclusive})){
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await incompatible.BeginTransactionAsync());
        }
    }

    [Fact]
    public void OptimisticRequiresAnExplicitCommonProfileAndFiniteBudgets(){
        Assert.Equal(TransactionConcurrencyMode.Exclusive,DatabaseContainerOptions.InMemory.TransactionConcurrency);
        Assert.Throws<ArgumentException>(()=>new BankDatabase(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic}));
        Assert.Throws<ArgumentException>(()=>new BankDatabase(Options() with{TransactionHistory=null}));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportCommitsTablesAndMaterializedViewsTogetherAndDiscardsAFailedSource(bool persistent){
        string? path=null;if(persistent){path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"import-"+Guid.NewGuid().ToString("N"));}
        var options=Options(path);
        using(var db=new BankDatabase(options)){
            Assert.Throws<IOException>(()=>db.ImportTables(new ImportSource(true)));
            await using(var empty=await db.BeginReadOnlyTransactionAsync()){Assert.Empty(empty.Accounts);Assert.Empty(empty.Summaries);await empty.CommitAsync();}
            db.ImportTables(new ImportSource(false));
            await using var read=await db.BeginReadOnlyTransactionAsync();Assert.Equal(2,read.Accounts.Count());Assert.Equal(100,read.Accounts[1].Balance);Assert.Equal(300,read.Summaries[10].Balance);await read.CommitAsync();
        }
        if(persistent){
            using var reopened=new BankDatabase(options);await using var read=await reopened.BeginReadOnlyTransactionAsync();Assert.Equal(200,read.Accounts[2].Balance);Assert.Equal(300,read.Summaries[10].Balance);
        }
    }
    [Fact]
    public async Task AsyncImportHonorsCancellationAndJoinsTheCallingTransaction(){
        using var db=new BankDatabase(Options());using var cancellation=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>db.ImportTablesAsync(new ImportSource(false,cancellation.Cancel),cancellation.Token));
        await using(var empty=await db.BeginReadOnlyTransactionAsync()){Assert.Empty(empty.Accounts);Assert.Empty(empty.Summaries);await empty.CommitAsync();}
        await using(var parent=await db.BeginTransactionAsync()){
            await parent.ImportTablesAsync(new ImportSource(false));Assert.Equal(100,parent.Accounts[1].Balance);Assert.Equal(300,parent.Summaries[10].Balance);
            await parent.RollbackAsync();
        }
        await using var verify=await db.BeginReadOnlyTransactionAsync();Assert.Empty(verify.Accounts);Assert.Empty(verify.Summaries);
    }
    private sealed class ImportSource(bool fail,Action? afterFirst=null):IDatabaseImportSource{
        public IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key){
            if(key.Name==nameof(BankDatabase.Accounts)){
                yield return (TRecord)(object)new Account(1,100);afterFirst?.Invoke();yield return (TRecord)(object)new Account(1,999);yield return (TRecord)(object)new Account(2,200);
            }else{
                if(fail){throw new IOException("source failed after the first table");}
                yield return (TRecord)(object)new Account(10,300);
            }
        }
    }
}
