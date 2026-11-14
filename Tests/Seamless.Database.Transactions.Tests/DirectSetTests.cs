using Seamless.Database;
namespace Seamless.Database.Transactions.Tests;

[MemoryPack.MemoryPackable]public readonly partial record struct AccessLogEntry(int Key,string Text):Seamless.Collections.IRecord<int>;

// 直接使う表(Set<T>)とトランザクションで使う表(Table<T>)を同じコンテナに宣言する。
public sealed partial class DirectSetDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<int,Account> Accounts=>this.Table<int,Account>(static account=>account.Id);
    public Seamless.Collections.Tsavorite.Set<string> Tags=>this.Set<string>();
    public Seamless.Collections.Tsavorite.Set<int,AccessLogEntry> Logs=>this.Set<int,AccessLogEntry>();
}

// 設計：Document/refactoring-designs/20261112-DBアクセスモデル.md の I1・I6。
public sealed class DirectSetTests{
    private static DatabaseContainerOptions Options(string? directory=null){
        var options=DatabaseContainerOptions.InMemory;
        if(directory is not null){options=DatabaseContainerOptions.OnStorage(directory);}
        return options with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
    }

    [Fact]
    public void DirectSetsAreSharedPerDeclarationAndWrittenWithoutATransaction(){
        using var database=new DirectSetDatabase(Options());
        Assert.True(database.Tags.Add("a"));
        Assert.False(database.Tags.Add("a"));
        Assert.Same(database.Tags,database.Tags);
        Assert.True(database.Logs.Add(new(1,"first")));
        Assert.True(database.Logs.TryGetValue(1,out var row));
        Assert.Equal("first",row.Text);
        Assert.Single(database.Tags);
    }

    [Fact]
    public void DirectSetsAreNotReachableFromTransactionsOrReaders(){
        // 混在はコンパイル時に防ぐ：取引・読み込みの入口には直接使う表のメンバーを生成しない。
        foreach(var type in new[]{typeof(DirectSetDatabase.TypedTransaction),typeof(DirectSetDatabase.Reader),typeof(DirectSetDatabase.IReader)}){
            Assert.Null(type.GetProperty("Tags"));
            Assert.Null(type.GetProperty("Logs"));
            Assert.NotNull(type.GetProperty("Accounts"));
        }
    }

    [Fact]
    public async Task DirectSetsSurviveACleanReopenAndStayApartFromTransactionalTables(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"direct-"+Guid.NewGuid().ToString("N"));
        using(var database=new DirectSetDatabase(Options(path))){
            database.Tags.Add("kept");
            database.Logs.Add(new(7,"seven"));
            await using var update=await database.BeginTransactionAsync();
            update.Accounts.Add(new(1,100));
            await update.CommitAsync();
        }
        Assert.True(Directory.Exists(Path.Combine(path,"direct","Tags")));
        using(var database=new DirectSetDatabase(Options(path))){
            Assert.Equal(["kept"],database.Tags.ToArray());
            Assert.True(database.Logs.TryGetValue(7,out var row));
            Assert.Equal("seven",row.Text);
            using var reader=database.BeginRead();
            Assert.Equal(100,reader.Accounts[1].Balance);
        }
    }
}
