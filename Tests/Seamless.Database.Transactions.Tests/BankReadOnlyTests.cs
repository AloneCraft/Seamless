using Seamless.Database;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

public sealed class BankReadOnlyTests{
    [Fact]
    public async Task GeneratedReadOnlyEntryRejectsWritesAndLeavesNormalEntryUsable(){
        using var database=new BankDatabase(DatabaseContainerOptions.InMemory with{
            IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20
        });
        await using(var seed=await database.BeginTransactionAsync()){
            seed.Accounts.Add(new(1,100));
            await seed.CommitAsync();
        }
        await using(var read=await database.BeginReadOnlyTransactionAsync()){
            Assert.Equal(100,read.Accounts[1].Balance);
            Assert.Throws<InvalidOperationException>(()=>read.Accounts[1]=new(1,50));
            Assert.Throws<InvalidOperationException>(()=>read.Accounts.Add(new(1,50)));
            Assert.Throws<InvalidOperationException>(()=>read.Accounts.Remove(999));
            Assert.Equal(TransactionCommitKind.NoChanges,(await read.CommitAsync()).Kind);
        }
        await using(var update=await database.BeginTransactionAsync()){
            update.Accounts[1]=new(1,90);
            await update.CommitAsync();
        }
        Assert.Equal(90,database.Accounts[1].Balance);
    }
}
