using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableQuery1 {
    [Fact, Trait("Category","Unit")]
    public void Select1() {
        var expected=SystemEnumerable.Select(Source(),static v => v*2);
        var actual0=Select_Actual0();
        var actual=Select_Actual1();
        Assert.Equal(expected,actual);
    }
    private static IEnumerable<int> Select_Actual0()=>(from v in Source() select v*2);
    private static IEnumerable<int> Select_Actual1() {
        return (from v in Source() select v*2);
    }
    [Fact, Trait("Category","Unit")]
    public void Select() {
        var expected=SystemEnumerable.Select(Source(),static v => v*2);
        var actual=Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Select_Actual() {
        return (from v in Source() select v*2).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where() {
        var expected=SystemEnumerable.Where(Source(),static v => v>2);
        var actual=Where_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_Actual() {
        return (from v in Source() where v>2 select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static v => v>2),static v => v*10);
        var actual=Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_Select_Actual() {
        return (from v in Source() where v>2 select v*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby() {
        var expected=SystemEnumerable.OrderBy(Unordered(),static v => v);
        var actual=Orderby_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_Actual() {
        return (from v in Unordered() orderby v select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_Ascending() {
        var expected=SystemEnumerable.OrderBy(Unordered(),static v => v);
        var actual=Orderby_Ascending_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_Ascending_Actual() {
        return (from v in Unordered() orderby v ascending select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_Descending() {
        var expected=SystemEnumerable.OrderByDescending(Unordered(),static v => v);
        var actual=Orderby_Descending_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_Descending_Actual() {
        return (from v in Unordered() orderby v descending select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_ThenBy() {
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Rows(),static r => r.Key),static r => r.Value);
        var actual=Orderby_ThenBy_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<Row> Orderby_ThenBy_Actual() {
        return (from r in Rows() orderby r.Key, r.Value select r).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_ThenByDescending() {
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Rows(),static r => r.Key),static r => r.Value);
        var actual=Orderby_ThenByDescending_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<Row> Orderby_ThenByDescending_Actual() {
        return (from r in Rows() orderby r.Key, r.Value descending select r).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Let() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(SystemEnumerable.Select(Source(),static v => new { v,doubled=v*2 }),static t => t.doubled>4),static t => t.doubled);
        var actual=Let_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Let_Actual() {
        return (from v in Source() let doubled=v*2 where doubled>4 select doubled).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_Into() {
        var expected=SystemEnumerable.Select(SystemEnumerable.OrderBy(SystemEnumerable.GroupBy(Source(),static v => v%2),static g => g.Key),static g => g.Sum());
        var actual=GroupBy_Into_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupBy_Into_Actual() {
        return (from v in Source()
                group v by v%2 into g
                orderby g.Key
                select g.Sum()).ToList();
    }

    private static readonly int[] SourceValue=[1,2,3,4];
    private static int[] Source() => SourceValue;

    private static readonly int[] UnorderedValue=[3,1,4,2];
    private static int[] Unordered() => UnorderedValue;

    private sealed record Row(int Key,int Value,string Name);
    private static readonly Row[] RowsValue=[new(2,30,"delta"),new(1,10,"beta"),new(2,20,"alpha"),new(1,30,"gamma")];
    private static Row[] Rows() => RowsValue;
}
