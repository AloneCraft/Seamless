using Seamless.FLinq;
using Seamless.Linq;

namespace Test.Seamless;

/// <summary>
/// ContiguousLookupBuilder(キーへ初出順のグループ番号を振ってから要素を1本の配列へ並べる)の結果が、
/// System.Linq の GroupBy/ToLookup/Join と同じであること。
/// </summary>
[LinqIntercept(false)]
public sealed class ContiguousLookupBuilderTests {
    private static readonly string?[] Keys = ["b",null,"a","b","c",null,"a","b"];

    private static List<string> Groups(Grouping<string?,int>? first) {
        var result = new List<string>();
        var group = first;
        while(group is not null) {
            result.Add((group.Key??"null")+":"+string.Join(",",group));
            group=group.NextGroupInAddOrder;
            if(group==first) {
                break;
            }
        }
        return result;
    }

    private static List<string> Expected() =>
        global::System.Linq.Enumerable.Select(
            global::System.Linq.Enumerable.GroupBy(global::System.Linq.Enumerable.Range(0,Keys.Length),index => Keys[index]),
            group => (group.Key??"null")+":"+string.Join(",",group)).ToList();

    [Theory]
    [InlineData(0,0)]
    [InlineData(8,8)]
    [InlineData(1,1)]
    public void グループはキーの初出順で要素は追加順になる(int elementCapacity,int groupCapacity) {
        var builder = new ContiguousLookupBuilder<string?,int>(elementCapacity,groupCapacity,null);
        for(var index = 0;index<Keys.Length;index++) {
            builder.Add(Keys[index],index);
        }
        Assert.Equal(Expected(),Groups(builder.GetRootGroupAndClear()));
    }

    [Fact]
    public void 空ならグループは無い() {
        var builder = new ContiguousLookupBuilder<string?,int>(0,0,null);
        Assert.Null(builder.GetRootGroupAndClear());
        var lookupBuilder = new ContiguousLookupBuilder<string?,int>(4,4,null);
        var lookup = lookupBuilder.BuildAndClear();
        Assert.Empty(lookup);
        Assert.Empty(lookup["a"]);
    }

    [Fact]
    public void 索引はキーで引けて列挙は初出順() {
        var builder = new ContiguousLookupBuilder<string?,int>(0,0,StringComparer.OrdinalIgnoreCase);
        for(var index = 0;index<Keys.Length;index++) {
            builder.Add(Keys[index],index);
        }
        builder.Add("A",100);
        var lookup = builder.BuildAndClear();
        Assert.Equal(4,lookup.Count);
        Assert.Equal([2,6,100],lookup["a"].ToArray());
        Assert.Equal([1,5],lookup[null].ToArray());
        Assert.True(lookup.Contains("B"));
        Assert.False(lookup.Contains("z"));
        Assert.Empty(lookup["z"]);
        Assert.Equal(new string?[] { "b",null,"a","c" }.AsEnumerable(),lookup.Select(static group => group.Key));
    }

    [Fact]
    public void 区間を指すグループは読み取り専用で位置は区間の中で数える() {
        var builder = new ContiguousLookupBuilder<string?,int>(0,0,null);
        for(var index = 0;index<Keys.Length;index++) {
            builder.Add(Keys[index],index);
        }
        var groups = new List<Grouping<string?,int>>();
        var first = builder.GetRootGroupAndClear()!;
        var group = first;
        do {
            groups.Add(group);
            group=group.NextGroupInAddOrder!;
        } while(group!=first);
        var a = groups.Single(static group => group.Key=="a");
        Assert.Equal(2,a.Count);
        Assert.Equal(6,a[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => a[2]);
        Assert.Equal(1,a.IndexOf(6));
        Assert.Equal(-1,a.IndexOf(0));
        Assert.True(a.Contains(2));
        Assert.False(a.Contains(3));
        var copy = new int[4];
        a.CopyTo(copy,1);
        Assert.Equal([0,2,6,0],copy);
        Assert.Throws<NotSupportedException>(() => a.Add(1));
    }

    [Fact]
    public void 結合の探索は一致の範囲を返す() {
        var builder = new ContiguousLookupBuilder<string?,int>(2,2,null);
        for(var index = 0;index<Keys.Length;index++) {
            builder.Add(Keys[index],index);
        }
        builder.PrepareProbe();
        try {
            Assert.True(builder.TryGetRange("b",out var start,out var end));
            Assert.Equal([0,3,7],builder.Arranged[start..end]);
            Assert.True(builder.TryGetRange(null,out start,out end));
            Assert.Equal([1,5],builder.Arranged[start..end]);
            Assert.False(builder.TryGetRange("z",out start,out end));
            Assert.Equal(start,end);
        } finally {
            builder.Dispose();
        }
    }

    [Fact]
    public void 件数の見込みを超えても伸びる() {
        var builder = new ContiguousLookupBuilder<int,int>(1,1,null);
        for(var index = 0;index<1000;index++) {
            builder.Add(index%37,index);
        }
        var lookup = builder.BuildAndClear();
        Assert.Equal(37,lookup.Count);
        Assert.Equal(global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Where(global::System.Linq.Enumerable.Range(0,1000),static index => index%37==5)),lookup[5].ToArray());
    }
}
