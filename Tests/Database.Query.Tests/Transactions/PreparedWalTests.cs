using Seamless.Transactions;

namespace Seamless.Database.Transactions.Tests;

public sealed class PreparedWalTests{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root."),"pw-"+Guid.NewGuid().ToString("N"));
    private static DatabaseContainerOptions Options(string root)=>DatabaseContainerOptions.OnStorage(root) with{
        PreparedTransactionWal=true,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20
    };

    [Fact]
    public async Task CheckpointReopenAndOutcomeUseThePreparedJournal(){
        var root=Root();Guid id;long lsn;
        using(var database=new BankDatabase(Options(root))){
            await using(var transaction=await database.BeginTransactionAsync()){
                id=transaction.Id;transaction.Accounts.Add(new(1,123));lsn=(await transaction.CommitAsync()).Lsn;
            }
            using(var uncommitted=database.BeginTransaction()){uncommitted.Accounts.Add(new(2,456));}
            Assert.Equal(123,Assert.Single(database.Accounts).Balance);
            await database.CheckpointTransactionsAsync();
        }
        Assert.True(Directory.Exists(Path.Combine(root,"wal-prepared-v1")));
        using(var wrong=new BankDatabase(Options(root) with{PreparedTransactionWal=false})){
            Assert.Throws<InvalidOperationException>(()=>wrong.Accounts);
        }
        using(var reopened=new BankDatabase(Options(root))){
            Assert.Equal(123,Assert.Single(reopened.Accounts).Balance);
            Assert.Equal(TransactionOutcomeKind.Committed,(await reopened.GetTransactionOutcomeAsync(id)).Kind);
            await reopened.ForgetTransactionOutcomesThroughAsync(lsn);
            Assert.Equal(TransactionOutcomeKind.Unknown,(await reopened.GetTransactionOutcomeAsync(id)).Kind);
        }
    }

    [Fact]
    public void AnExistingSingleFrameDatasetRequiresExplicitMigration(){
        var root=Root();
        using(var database=new BankDatabase(Options(root) with{PreparedTransactionWal=false})){
            using var transaction=database.BeginTransaction();transaction.Accounts.Add(new(1,9));transaction.Commit();
        }
        using(var wrong=new BankDatabase(Options(root))){Assert.Throws<InvalidOperationException>(()=>wrong.Accounts);}
        using var restored=new BankDatabase(Options(root) with{PreparedTransactionWal=false});
        Assert.Equal(9,restored.Accounts[1].Balance);
    }
}
