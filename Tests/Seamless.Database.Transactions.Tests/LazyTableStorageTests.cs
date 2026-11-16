using Seamless.Database;
namespace Seamless.Database.Transactions.Tests;

// 使う表だけを開く(設計 Document/refactoring-designs/20261116-使う表だけを開く.md)：
// 表の保存の仕組みは初めて使うときに開き、そのときに、その表の分の変更の記録(WAL)だけを当て直す。
public sealed partial class LazyTableDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<int,Account> Accounts=>this.Table<int,Account>(static account=>account.Id);
    public Table<int,Account> Others=>this.Table<int,Account>(static account=>account.Id);
}
public sealed class LazyTableStorageTests{
    private static DatabaseContainerOptions Options(string directory)=>DatabaseContainerOptions.OnStorage(directory) with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};

    [Fact]public async Task UnopenedTablesKeepTheirDataAndReplayTheirOwnChangesWhenOpened(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.LazyTables",Guid.NewGuid().ToString("N"));
        try{
            // 1. 両方の表に書いて保存する。
            using(var database=new LazyTableDatabase(Options(path))){
                await using var transaction=await database.BeginTransactionAsync();
                transaction.Accounts.Add(new(1,10));transaction.Others.Add(new(1,100));await transaction.CommitAsync();
                await database.CheckpointTransactionsAsync();
            }
            // 2. 開き直す。取引を始めても、使っていない表の保存の仕組みは開かない。Others にだけ書いて、保存せずに落とす。
            using(var database=new LazyTableDatabase(Options(path))){
                await using(var transaction=await database.BeginTransactionAsync()){
                    Assert.False(database.Accounts.IsStorageOpen);
                    transaction.Others.Add(new(2,200));await transaction.CommitAsync();
                }
                Assert.True(database.Others.IsStorageOpen);
                Assert.False(database.Accounts.IsStorageOpen);
                database.DiscardOnDispose=true;
            }
            // 3. 開き直す。Others は開かずに、Accounts にだけ書いて保存する(Others の記録は当てずに残す)。
            using(var database=new LazyTableDatabase(Options(path))){
                await using(var transaction=await database.BeginTransactionAsync()){transaction.Accounts.Add(new(3,30));await transaction.CommitAsync();}
                await database.CheckpointTransactionsAsync();
                Assert.False(database.Others.IsStorageOpen);
            }
            // 4. 開き直して Others を開くと、2 で書いた行が当て直されている。保存を挟んでも前の行は残る。
            using(var database=new LazyTableDatabase(Options(path))){
                Assert.Equal(100,database.Others[1].Balance);
                Assert.Equal(200,database.Others[2].Balance);
                Assert.Equal(10,database.Accounts[1].Balance);
                Assert.Equal(30,database.Accounts[3].Balance);
                await database.CheckpointTransactionsAsync();
            }
            // 5. 全部の表を開いて保存した後も、同じ内容で開ける。
            using(var database=new LazyTableDatabase(Options(path))){
                Assert.Equal(2,database.Others.Count);
                Assert.Equal(2,database.Accounts.Count);
            }
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }

    // 開いていない表の保存の仕組みは、DB の上限からメモリを取り置かない。
    [Fact]public async Task UnopenedTablesDoNotReserveMemory(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.LazyTables",Guid.NewGuid().ToString("N"));
        try{
            using(var database=new LazyTableDatabase(Options(path))){
                await using(var transaction=await database.BeginTransactionAsync()){transaction.Accounts.Add(new(1,10));await transaction.CommitAsync();}
                var oneTable=database.ResourceUsage.MemoryBytes;
                Assert.False(database.Others.IsStorageOpen);
                Assert.Equal(0,database.Others.Count);
                Assert.True(database.Others.IsStorageOpen);
                Assert.True(database.ResourceUsage.MemoryBytes>oneTable,"表を開いたら取り置きが増えるはずです。");
            }
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
}
