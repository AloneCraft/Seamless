using MemoryPack;
using Seamless.Database;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;
internal static partial class DatabaseGenerator;
[MemoryPackable]public partial record struct Account(int Id,decimal Balance);
public sealed partial class BankDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<int,Account> Accounts=>this.Table<int,Account>(static account=>account.Id);
    public MaterializedView<int,Account> Summaries=>this.MaterializedView<int,Account>(static account=>account.Id);
    public void LoadSummary(Account row)=>AddMaterializedViewRow(this.Summaries,row);
}
public sealed class BankTests{
    private static DatabaseContainerOptions Options(string? directory=null){
        var options=DatabaseContainerOptions.InMemory;
        if(directory is not null){options=DatabaseContainerOptions.OnStorage(directory);}
        return options with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
    }
    [Fact]public async Task GeneratedIndexerTransferAndJoinedScopes(){
        using var database=new BankDatabase(Options());
        await using(var transaction=await database.BeginTransactionAsync()){
            transaction.Accounts.Add(new(1,100));transaction.Accounts.Add(new(2,0));await transaction.CommitAsync();
        }
        await using(var transaction=await database.BeginTransactionAsync()){
            var source=transaction.Accounts[1];var destination=transaction.Accounts[2];
            transaction.Accounts[1]=source with{Balance=source.Balance-40};transaction.Accounts[2]=destination with{Balance=destination.Balance+40};
            Assert.Throws<InvalidOperationException>(()=>database.Accounts[1]);
            Assert.Throws<InvalidOperationException>(()=>database.BeginTransaction());
            await using(var child=await transaction.BeginTransactionAsync()){
                Assert.Equal(transaction.Id,child.Id);Assert.Equal(TransactionCommitKind.Voted,(await child.CommitAsync()).Kind);
            }
            await transaction.CommitAsync();
        }
        Assert.Equal(60,database.Accounts[1].Balance);Assert.Equal(100,database.Accounts.Sum(static row=>row.Balance));
        using(var transaction=database.BeginTransaction()){
            database.Accounts[1]=new(1,50);transaction.Commit();
        }
        Assert.Equal(50,database.Accounts[1].Balance);
    }
    [Fact]public async Task PersistentCheckpointRecoveryAndOutcome(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Bank",Guid.NewGuid().ToString("N"));Guid id;long lsn;
        try{
            using(var database=new BankDatabase(Options(path))){
                using(var load=database.BeginTransaction()){database.LoadSummary(new(1,10));load.Commit();}
                await using var transaction=await database.BeginTransactionAsync();id=transaction.Id;transaction.Accounts.Add(new(1,123));lsn=(await transaction.CommitAsync()).Lsn;
                await database.CheckpointTransactionsAsync();
            }
            using(var database=new BankDatabase(Options(path))){
                Assert.Equal(TransactionOutcomeKind.Committed,(await database.GetTransactionOutcomeAsync(id)).Kind);
                Assert.Equal(123,database.Accounts[1].Balance);
                await using(var read=await database.BeginTransactionAsync()){Assert.Equal(10,read.Summaries[1].Balance);await read.CommitAsync();}
                await database.ForgetTransactionOutcomesThroughAsync(lsn);
            }
            using(var database=new BankDatabase(Options(path))){Assert.Equal(TransactionOutcomeKind.Unknown,(await database.GetTransactionOutcomeAsync(id)).Kind);Assert.Equal(123,database.Accounts[1].Balance);}
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public async Task LegacyModeRejectsSharedMaintenanceAndPreservesLegacyTransactions(){
        using var database=new BankDatabase(Options() with{Transactions=DatabaseTransactionMode.Legacy});
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await database.GetTransactionOutcomeAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await database.ForgetTransactionOutcomesThroughAsync(0));
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await database.CheckpointTransactionsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await database.BeginTransactionAsync());
        using(var transaction=database.BeginTransaction()){database.Accounts.Add(new(1,100));transaction.Commit();}
        Assert.Equal(100,database.Accounts[1].Balance);
    }
    [Fact]public async Task CompetingUpdatesAreSerializedAndIncompleteJoinedScopeAborts(){
        using var database=new BankDatabase(Options());
        await using(var seed=await database.BeginTransactionAsync()){seed.Accounts.Add(new(1,0));await seed.CommitAsync();}
        await Task.WhenAll(Enumerable.Range(0,20).Select(async _=>{
            await using var transaction=await database.BeginTransactionAsync();var row=transaction.Accounts[1];
            await Task.Yield();transaction.Accounts[1]=row with{Balance=row.Balance+1};await transaction.CommitAsync();
        }));
        Assert.Equal(20,database.Accounts[1].Balance);
        await using(var root=await database.BeginTransactionAsync()){
            root.Accounts[1]=new(1,999);
            await using(var child=await root.BeginTransactionAsync()){
                await Assert.ThrowsAsync<InvalidOperationException>(async()=>await root.CommitAsync());
            }
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await root.CommitAsync());
        }
        Assert.Equal(20,database.Accounts[1].Balance);
        await using(var unfinished=await database.BeginTransactionAsync()){unfinished.Accounts[1]=new(1,999);}
        Assert.Equal(20,database.Accounts[1].Balance);
    }
}
