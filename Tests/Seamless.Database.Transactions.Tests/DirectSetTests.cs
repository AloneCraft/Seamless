using Seamless.Database;
namespace Seamless.Database.Transactions.Tests;

[MemoryPack.MemoryPackable]public readonly partial record struct AccessLogEntry(int Key,string Text):Seamless.Collections.IRecord<int>;

// 直接使う表(Set<T>)とトランザクションで使う表(Table<T>)を同じコンテナに宣言する。
public sealed partial class DirectSetDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<int,Account> Accounts=>this.Table<int,Account>(static account=>account.Id);
    public Set<string> Tags=>this.Set<string>();
    public Set<int,AccessLogEntry> Logs=>this.Set<int,AccessLogEntry>();
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
        Assert.True(Directory.Exists(Path.Combine(path,"direct-v2")));
        using(var database=new DirectSetDatabase(Options(path))){
            Assert.Equal(["kept"],database.Tags.ToArray());
            Assert.True(database.Logs.TryGetValue(7,out var row));
            Assert.Equal("seven",row.Text);
            using var reader=database.BeginRead();
            Assert.Equal(100,reader.Accounts[1].Balance);
        }
    }
    // 11/15 より前の形(表ごとのフォルダ direct/<名前>)は自動では読み替えず、明示の例外にする。
    [Fact]
    public void LegacyDirectSetFoldersAreNotConvertedAutomatically(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"direct-legacy-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path,"direct","Tags"));
        using var database=new DirectSetDatabase(Options(path));
        var error=Assert.Throws<InvalidOperationException>(()=>database.Tags.Add("x"));
        Assert.Contains("自動では読み替えません",error.Message);
    }

    // 複数のスレッドから同時に書ける(常に開いた並列区間とスレッドごとの置き場)。ログが満杯・件数の見込み超えで止めて広げても、行は欠けない。
    [Fact]
    public void DirectSetsAcceptConcurrentWritersAndGrowWithoutLosingRows(){
        using var database=new DirectSetDatabase(Options());
        const int threads=4,perThread=20_000;
        var workers=Enumerable.Range(0,threads).Select(index=>new Thread(()=>{
            for(var i=0;i<perThread;i++){var key=index*perThread+i;Assert.True(database.Logs.Add(new(key,"v"+key)));}
        })).ToArray();
        foreach(var worker in workers){worker.Start();}
        foreach(var worker in workers){worker.Join();}
        Assert.Equal(threads*perThread,database.Logs.LongCount);
        Assert.Equal(threads*perThread,database.Logs.Version);
        for(var key=0;key<threads*perThread;key++){Assert.True(database.Logs.TryGetValue(key,out var row));Assert.Equal("v"+key,row.Text);}
        Assert.Equal(threads*perThread,database.Logs.Count());
    }

    // 走査の中で同じ表を読める(入れ子)。走査は破棄までそのスレッドの置き場を使用中にする。
    [Fact]
    public void DirectSetsCanBeReadInsideTheirOwnEnumeration(){
        using var database=new DirectSetDatabase(Options());
        for(var key=0;key<100;key++){database.Logs.Add(new(key,"x"));}
        var seen=0;
        foreach(var row in database.Logs){Assert.True(database.Logs.ContainsKey(row.Key));seen++;}
        Assert.Equal(100,seen);
        Assert.Equal(100,database.Logs.Where(row=>database.Logs.ContainsKey(row.Key)).Count());
    }

    // 表の版は成功した書き込みだけで増える。値が同じなら、その間に書き込みが無かった。
    [Fact]
    public void DirectSetVersionCountsOnlySuccessfulWrites(){
        using var database=new DirectSetDatabase(Options());
        var start=database.Tags.Version;
        Assert.True(database.Tags.Add("a"));Assert.Equal(start+1,database.Tags.Version);
        Assert.False(database.Tags.Add("a"));Assert.Equal(start+1,database.Tags.Version);
        Assert.False(database.Tags.Remove("b"));Assert.Equal(start+1,database.Tags.Version);
        Assert.True(database.Tags.Remove("a"));Assert.Equal(start+2,database.Tags.Version);
        Assert.Equal(0,database.Tags.Count);
    }

    // 広げるときに、他のスレッドが走査を開いたままなら上限までしか待たず、広げるのをやめて書き込みを続ける(互いに待ち合って止まらない)。
    // 走査が終われば、次の確かめのときに実際に止めて広げる。
    [Fact]
    public void GrowingWaitsOnlyUpToTheLimitWhileAnotherThreadKeepsAnEnumerationOpen(){
        var previous=DirectSetCore<global::Seamless.Collections.Tsavorite.ParallelSet<int,AccessLogEntry>>.MaintenanceWaitLimit;
        DirectSetCore<global::Seamless.Collections.Tsavorite.ParallelSet<int,AccessLogEntry>>.MaintenanceWaitLimit=TimeSpan.FromMilliseconds(200);
        try{
            using var database=new DirectSetDatabase(Options());
            database.Logs.Add(new(-1,"held"));
            using var opened=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            var holder=new Thread(()=>{using var enumerator=database.Logs.GetEnumerator();enumerator.MoveNext();opened.Set();release.Wait();});
            holder.Start();opened.Wait();
            try{
                // 区間を開いたときの見込み(1024件)を超えて書く。広げようとしても走査が開いたままなので、上限で諦めて続ける。
                for(var key=0;key<10_000;key++){Assert.True(database.Logs.Add(new(key,"while-held")));}
                Assert.Equal(0,database.Logs.MaintenanceCount);
            }finally{release.Set();holder.Join();}
            for(var key=10_000;key<20_000;key++){Assert.True(database.Logs.Add(new(key,"after")));}
            Assert.True(database.Logs.MaintenanceCount>0);
            Assert.Equal(20_001,database.Logs.LongCount);
            for(var key=0;key<20_000;key++){Assert.True(database.Logs.ContainsKey(key));}
        }finally{DirectSetCore<global::Seamless.Collections.Tsavorite.ParallelSet<int,AccessLogEntry>>.MaintenanceWaitLimit=previous;}
    }
}
