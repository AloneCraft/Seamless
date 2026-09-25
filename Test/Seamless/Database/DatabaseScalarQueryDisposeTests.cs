namespace Test.Seamless.Database;

// FLinq で展開した集計・要素取得・実体化のあとも、テーブルの読み取りセッションが残らず DatabaseContainer を破棄できる(BUG-0007)。
// 読み取りセッションが残ると、同じ実行コンテキストでの Dispose は例外になり、別コンテキストでの Dispose は書き込みロック待ちで止まる(BUG-0005)。
public sealed class DatabaseScalarQueryDisposeTests {
    [Fact,Trait("Category","Unit")]
    public void Dispose_AfterInterceptedQueries_Succeeds() {
        var database=new ScalarQueryDatabase();
        try {
            for(var id=1;id<=5;id++) {
                Assert.True(database.Items.Add(new(id,id.ToString())));
            }

            Assert.Equal(15,database.SumIds());
            Assert.Equal(3,database.CountIdsAbove(2));
            Assert.Equal(4,database.MinIdAbove(3));
            Assert.Equal(5,database.ItemIds().Count);
        } catch {
            try{database.Dispose();}catch(InvalidOperationException){}
            throw;
        }

        Assert.Null(Record.Exception(database.Dispose));
    }

    [Fact,Trait("Category","Unit")]
    public void Dispose_FromAnotherContext_AfterInterceptedSum_Returns() {
        var database=new ScalarQueryDatabase();
        Assert.True(database.Items.Add(new(1,"1")));
        Assert.Equal(1,database.SumIds());

        // xUnit のフィクスチャ破棄と同じく、実行コンテキスト(AsyncLocal)を引き継がずに破棄する。
        // 読み取りロックが残っていると、この破棄は書き込みロック待ちで戻らない。
        Task disposing;
        using(ExecutionContext.SuppressFlow()) {
            disposing=Task.Factory.StartNew(
                database.Dispose,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach|TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        Assert.True(disposing.Wait(TimeSpan.FromSeconds(30)),"DatabaseContainer.Dispose did not return within 30 seconds after an intercepted Sum.");
    }
}

internal sealed class ScalarQueryDatabase()
    :global::Seamless.Database.DatabaseContainer(global::Seamless.Database.DatabaseContainerOptions.InMemory with { IndexSize=1L<<8,MemorySize=1L<<20 }) {
    public global::Seamless.Database.Table<global::Test.Seamless.Database.Spec.Records.Item> Items=>
        this.Table<global::Test.Seamless.Database.Spec.Records.Item>();

    [global::Seamless.FLinq.LinqIntercept(true)]
    public int SumIds()=>this.Items.Sum(item=>item.Id);

    [global::Seamless.FLinq.LinqIntercept(true)]
    public int CountIdsAbove(int minimumId)=>this.Items.Count(item=>item.Id>minimumId);

    [global::Seamless.FLinq.LinqIntercept(true)]
    public int MinIdAbove(int minimumId)=>this.Items.Where(item=>item.Id>minimumId).Min(item=>item.Id);

    [global::Seamless.FLinq.LinqIntercept(true)]
    public List<int> ItemIds()=>this.Items.Select(item=>item.Id).ToList();
}
