namespace Test.System.Linq.Intercept;

/// <summary>入れ子の LINQ のラムダが、外側のメソッドの引数を使っても展開できる(TPC-E の測定で CS0103 になった)。</summary>
public sealed class NestedLambdaOuterParameter {
    private sealed class Database {
        public required int[] Rows { get; init; }
        public IndexTable Index { get; } = new();
        public TradeTable Trade { get; } = new();
    }

    private sealed class IndexTable {
        public IEnumerable<Entry> All(int account) => [new Entry(account),new Entry(account+1)];
    }

    private sealed class TradeTable {
        public Entry Get(int id) => new(id*10);
    }

    private readonly record struct Entry(int Id) {
        public int Quantity => this.Id;
    }

    private sealed record Row(int Account,int Total);

    private static List<Row> Totals(int[] accounts,Database db) =>
        accounts.Select(a => new Row(a,db.Index.All(a).Sum(e => db.Trade.Get(e.Id).Quantity))).ToList();

    [Fact, Trait("Category","Unit")]
    public void InnerLambdaUsesOuterMethodParameterAfterHoistedReceiver() =>
        Assert.Equal([new Row(1,30),new Row(2,50)],Totals([1,2],new Database { Rows = [] }));

    private static int[] CountAbove(int[] source,Database db) =>
        source.Select(x => db.Rows.Where(y => y>x).Count()).ToArray();

    private static int[] CountAboveWithSum(int[] source,Database db,int bonus) =>
        source.Where(x => x>=0).Select(x => db.Rows.Where(y => y>x+bonus).Sum()).ToArray();

    private static List<int> AnyMatch(List<int> source,Database db) =>
        source.Where(x => db.Rows.Any(y => y==x)).ToList();

    [Fact, Trait("Category","Unit")]
    public void SelectWithInnerWhereCount() =>
        Assert.Equal([3,2,1],CountAbove([1,2,3],new Database { Rows = [2,3,4] }));

    [Fact, Trait("Category","Unit")]
    public void SelectWithInnerWhereSumUsingTwoOuterParameters() =>
        Assert.Equal([9,7],CountAboveWithSum([1,2],new Database { Rows = [2,3,4] },0));

    [Fact, Trait("Category","Unit")]
    public void WhereWithInnerAny() =>
        Assert.Equal([2,3],AnyMatch([1,2,3],new Database { Rows = [2,3,4] }));
}
