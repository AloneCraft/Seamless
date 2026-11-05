using Seamless.Database;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

public sealed class BankVersionedTests{
    private static DatabaseContainerOptions Options(string? directory=null){
        var options=DatabaseContainerOptions.InMemory;if(directory is not null){options=DatabaseContainerOptions.OnStorage(directory);}
        return options with{Transactions=DatabaseTransactionMode.SharedV2,TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
            IndexSize=16,PageSize=4096,SegmentSize=1<<16,MemorySize=1<<16,TransactionMemoryLimit=1024,ManagedMemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=32<<20,
            TemporaryDirectory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"bank-work"),
            TransactionHistory=new(){MaxRetainedMemoryBytes=64<<10,MaxRetainedStorageBytes=1<<20,MaxMetadataBytes=512<<10,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=4<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}};
    }
    private static Task OutsideCurrent(Func<Task> action){using(ExecutionContext.SuppressFlow()){return Task.Run(action);}}
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
}
