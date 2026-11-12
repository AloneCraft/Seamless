using Seamless.Linq;
using SeamlessSet = Seamless.Collections;

namespace Seamless.Core.Tests;

// Seamless.Linq.Set の外から見える振る舞い：結果が集合であること、Contains・Count、重複除去、
// 中身を読んだ時点で元の集合から計算すること（コピーを作らないこと）。
public sealed class SetOperations {
    [Fact]
    public void WhereKeepsTheSubsetAndAnswersContainsFromTheSourceAndCondition() {
        var source = new CountingSet<int>(1,2,3,4,5,6);
        var even = source.Where(x => x%2==0);

        Assert.Equal([2,4,6],even.Order());
        Assert.Equal(3,even.Count);
        Assert.Equal(3L,even.GetLongCount());
        Assert.True(even.Contains(4));
        Assert.False(even.Contains(3));
        Assert.False(even.Contains(8));
    }

    [Fact]
    public void SelectRemovesDuplicatedProjections() {
        var source = new CountingSet<int>(1,2,3,4,5,6);
        var remainders = source.Select(x => x%3);

        Assert.Equal([0,1,2],remainders.Order());
        Assert.Equal(3,remainders.Count);
        Assert.True(remainders.Contains(2));
        Assert.False(remainders.Contains(3));
    }

    [Fact]
    public void ResultsAreComputedWhenReadAndFollowTheSource() {
        var source = new CountingSet<int>(1,2,3);
        var projected = source.Where(x => x>1).Select(x => x*10);
        Assert.Equal(0,source.Enumerations);

        source.Add(4);
        Assert.Equal([20,30,40],projected.Order());
        Assert.Equal(1,source.Enumerations);

        source.Remove(2);
        Assert.Equal([30,40],projected.Order());
        Assert.Equal(2,source.Enumerations);
    }

    [Fact]
    public void SelectManyRemovesDuplicatedElements() {
        var source = new CountingSet<string>("ab","bc","cd");
        var letters = source.SelectMany(x => x.ToCharArray());
        Assert.Equal(['a','b','c','d'],letters.Order());

        var pairs = source.SelectMany(x => x.ToCharArray(),(_,letter) => char.ToUpperInvariant(letter));
        Assert.Equal(['A','B','C','D'],pairs.Order());
        Assert.True(pairs.Contains('C'));
    }

    [Fact]
    public void JoinRemovesDuplicatedRowsAndSkipsNullKeys() {
        var orders = new CountingSet<(int Customer, int Amount)>((1,10),(1,20),(2,30),(3,40));
        var customers = new[] { (Id: 1, Name: "A"),(Id: 2, Name: "B"),(Id: 2, Name: "B") };
        var names = orders.Join(customers,o => o.Customer,c => c.Id,(_,c) => c.Name);
        Assert.Equal(["A","B"],names.Order());

        var nullable = new CountingSet<string?>("x",null);
        var joined = nullable.Join(new string?[] { "x",null },x => x,y => y,(x,_) => x);
        Assert.Equal(["x"],joined);
    }

    [Fact]
    public void SetOperationsAnswerFromBothSets() {
        var first = new CountingSet<int>(1,2,3,4);
        var second = new CountingSet<int>(3,4,5);

        var union = first.Union(second);
        Assert.Equal([1,2,3,4,5],union.Order());
        Assert.Equal(5,union.Count);
        Assert.True(union.Contains(5));

        var intersect = first.Intersect(second);
        Assert.Equal([3,4],intersect.Order());
        Assert.False(intersect.Contains(1));

        var except = first.Except(second);
        Assert.Equal([1,2],except.Order());
        Assert.False(except.Contains(3));

        Assert.Same(first,first.Distinct());
    }

    [Fact]
    public void ResultsChainAsSets() {
        var source = new CountingSet<int>(1,2,3,4,5,6,7,8);
        SeamlessSet.ISet<int> chained = source.Where(x => x>2).Select(x => x/2).Where(x => x!=3);
        Assert.Equal([1,2,4],chained.Order());
        Assert.True(chained.Contains(4));
        Assert.False(chained.Contains(3));
    }

    [Fact]
    public void ArgumentsAreValidated() {
        var source = new CountingSet<int>(1);
        Assert.Throws<ArgumentNullException>(() => source.Where(null!));
        Assert.Throws<ArgumentNullException>(() => source.Select<int,int>(null!));
        Assert.Throws<ArgumentNullException>(() => Set.Where<int>(null!,x => true));
        Assert.Throws<ArgumentNullException>(() => source.Union(null!));
    }

    // 列挙された回数を数える最小の集合。結果がコピーではなく元を読み直すことを確認するために使う。
    private sealed class CountingSet<T>(params T[] items):SeamlessSet.ISet<T> {
        private readonly HashSet<T> items = [.. items];

        public int Enumerations { get; private set; }
        public int Count => this.items.Count;
        public void Add(T item) => this.items.Add(item);
        public void Remove(T item) => this.items.Remove(item);
        public bool Contains(T item) => this.items.Contains(item);

        public IEnumerator<T> GetEnumerator() {
            this.Enumerations++;
            return this.items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
