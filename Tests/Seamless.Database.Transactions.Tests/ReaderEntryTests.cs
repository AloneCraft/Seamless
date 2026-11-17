using Seamless.Database;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

// 読み込み専用の入口 BeginRead と、読み書きの取引が共有する IReader の外から見える振る舞い。
// 設計：Document/refactoring-designs/20261112-DBアクセスモデル.md の I2〜I4。
public sealed class ReaderEntryTests{
    // 並行する読み書きを確かめるため、既定では版付きの楽観方式(Optimistic)で作る。
    // 既定のExclusiveは書き込みの取引だけを1つずつ通し、読み込み専用の取引は版付きの過去の版から読むので待たない(2026-11-15の決定)。
    private static BankDatabase Create(TransactionConcurrencyMode mode=TransactionConcurrencyMode.Optimistic)=>new(DatabaseContainerOptions.InMemory with{
        TransactionConcurrency=mode,
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

    [Theory(Timeout=60_000)]
    [InlineData(TransactionConcurrencyMode.Optimistic)]
    [InlineData(TransactionConcurrencyMode.Exclusive)]
    public async Task ReaderStaysOnTheSnapshotTakenWhenItBegan(TransactionConcurrencyMode mode){
        using var database=Create(mode);
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

    // Exclusiveでも、書き込みの取引を開いたまま読み込み専用の取引を始められる。書き込みどうしは今までどおり1つずつ動く。
    [Fact(Timeout=60_000)]
    public async Task ExclusiveReaderDoesNotWaitForAnOpenWriterButWritersStaySerialized(){
        using var database=Create(TransactionConcurrencyMode.Exclusive);
        await Seed(database,new(1,100),new(2,50));
        await using var update=await database.BeginTransactionAsync();
        update.Accounts[1]=new(1,90);
        var seen=await Isolated(()=>{
            using var reader=database.BeginRead();
            return Task.FromResult(Total(reader));
        }).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(150,seen);
        var second=Isolated(async()=>{
            await using var other=await database.BeginTransactionAsync();
            other.Accounts[2]=new(2,40);
            return await other.CommitAsync();
        });
        await Task.Delay(300);
        Assert.False(second.IsCompleted);
        await update.CommitAsync();
        await second.WaitAsync(TimeSpan.FromSeconds(10));
        using var after=database.BeginRead();
        Assert.Equal(130,Total(after));
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
