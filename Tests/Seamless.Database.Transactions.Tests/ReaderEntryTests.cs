using Seamless.Database;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

// 読み込み専用の入口 BeginRead と、読み書きの取引が共有する IReader の外から見える振る舞い。
// 設計：docs/refactoring-designs/20261112-0017-database-access-model.md の I2〜I4。
public sealed class ReaderEntryTests{
    // 並行する読み書きを確かめるため、版付きの楽観方式(Optimistic)で作る。既定のExclusiveは読み込みを含めて取引を1つずつ通す。
    private static BankDatabase Create()=>new(DatabaseContainerOptions.InMemory with{
        TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
        IndexSize=16,PageSize=4096,SegmentSize=1<<16,MemorySize=1<<16,TransactionMemoryLimit=1024,ManagedMemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=32<<20,
        TemporaryDirectory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"reader-work"),
        TransactionHistory=new(){MaxRetainedMemoryBytes=64<<10,MaxRetainedStorageBytes=1<<20,MaxMetadataBytes=512<<10,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=4<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}
    });

    private static async Task Seed(BankDatabase database,params Account[] rows){
        await using var seed=await database.BeginTransactionAsync();
        foreach(var row in rows){seed.Accounts.Add(row);}
        await seed.CommitAsync();
    }

    // 取引は非同期の流れ(AsyncLocal)ごとに1つの根しか開けない。並行する別の取引は、流れを切り離した別のタスクで開く。
    private static Task<T> Isolated<T>(Func<Task<T>> body){
        using(ExecutionContext.SuppressFlow()){return Task.Run(body);}
    }

    // ビュー・関数の本体と同じく、入口の種類を問わず IReader だけを受け取る。
    private static long Total(BankDatabase.IReader reader)=>reader.Accounts.Sum(static account=>(long)account.Balance);

    [Fact(Timeout=60_000)]
    public async Task ReaderStaysOnTheSnapshotTakenWhenItBegan(){
        using var database=Create();
        await Seed(database,new(1,100),new(2,50));
        long seen;
        using(var reader=database.BeginRead()){
            await Isolated(async()=>{
                await using var update=await database.BeginTransactionAsync();
                update.Accounts[1]=new(1,90);
                return await update.CommitAsync();
            });
            Assert.Equal(150,Total(reader));
            Assert.Equal(100,reader.Accounts[1].Balance);
        }
        using(var later=database.BeginRead()){seen=Total(later);}
        Assert.Equal(140,seen);
    }

    [Fact(Timeout=60_000)]
    public async Task UncommittedChangesAreVisibleOnlyInsideTheirTransaction(){
        using var database=Create();
        await Seed(database,new Account(1,100));
        await using var update=await database.BeginTransactionAsync();
        update.Accounts.Add(new(2,7));
        Assert.Equal(107,Total(update));
        var outside=await Isolated(()=>{
            using var reader=database.BeginRead();
            return Task.FromResult((Total(reader),reader.Accounts.ContainsKey(2)));
        });
        Assert.Equal((100L,false),outside);
        update.Rollback();
        await update.DisposeAsync();
        using var after=database.BeginRead();
        Assert.Equal(100,Total(after));
    }

    [Fact(Timeout=60_000)]
    public async Task ReaderTablesAreReadOnlySetsWithRowEquality(){
        using var database=Create();
        await Seed(database,new(1,100),new(2,50));
        using var reader=database.BeginRead();
        Seamless.Collections.ISet<Account> accounts=((BankDatabase.IReader)reader).Accounts;
        Assert.Equal(2,accounts.Count);
        Assert.Equal(2L,accounts.GetLongCount());
        Assert.True(accounts.Contains(new(1,100)));
        Assert.False(accounts.Contains(new(1,99)));
        Assert.False(accounts.Contains(new(3,100)));
        // 読み込み専用の入口は確定・書き込みの入口を持たない。
        Assert.Null(typeof(BankDatabase.Reader).GetMethod("Commit"));
        Assert.Null(typeof(BankDatabase.Reader).GetMethod("CommitAsync"));
        await Task.CompletedTask;
    }

    [Fact(Timeout=60_000)]
    public async Task SynchronousCommitPublishesAndDisposeWithoutCommitDiscards(){
        using var database=Create();
        await Seed(database,new Account(1,100));
        using(var update=await database.BeginTransactionAsync()){
            update.Accounts[1]=new(1,80);
            Assert.NotEqual(TransactionCommitKind.NoChanges,update.Commit().Kind);
        }
        using(var discarded=await database.BeginTransactionAsync()){
            discarded.Accounts[1]=new(1,0);
        }
        using var reader=await database.BeginReadAsync();
        Assert.Equal(80,reader.Accounts[1].Balance);
    }

}
