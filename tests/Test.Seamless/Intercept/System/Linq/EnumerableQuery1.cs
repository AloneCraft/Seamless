using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableQuery1 {
    [Fact, Trait("Category","Unit")]
    public void Select_projection() {
        var expected=SystemEnumerable.Select(Source(),static v => v*2).ToList();
        var actual=Select_projection_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Select_projection_Actual() {
        return (from v in Source() select v*2).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_identity_select() {
        var expected=SystemEnumerable.Where(Source(),static v => v>2).ToList();
        var actual=Where_identity_select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_identity_select_Actual() {
        return (from v in Source() where v>2 select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_projection_select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static v => v>2),static v => v*10).ToList();
        var actual=Where_projection_select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_projection_select_Actual() {
        return (from v in Source() where v>2 select v*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static v => v>1),static v => v<4),
            static v => v*10).ToList();
        var actual=Where_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_Where_Select_Actual() {
        return (from v in Source() where v>1 where v<4 select v*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Let_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static v => (v: v,doubled: v*2)),static t => t.doubled>4),
            static t => t.doubled).ToList();
        var actual=Let_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Let_Where_Select_Actual() {
        return (from v in Source() let doubled=v*2 where doubled>4 select doubled).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Let_Item1_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static v => (v: v,v*2)),static t => t.Item2>4),
            static t => t.Item2).ToList();
        var actual=Let_Item1_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Let_Item1_Where_Select_Actual() {
        return (from v in Source() let Item1=v*2 where Item1>4 select Item1).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_identity_select() {
        var expected=SystemEnumerable.OrderBy(Unordered(),static v => v).ToList();
        var actual=Orderby_identity_select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_identity_select_Actual() {
        return (from v in Unordered() orderby v select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_Ascending() {
        var expected=SystemEnumerable.OrderBy(Unordered(),static v => v).ToList();
        var actual=Orderby_Ascending_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_Ascending_Actual() {
        return (from v in Unordered() orderby v ascending select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_Descending() {
        var expected=SystemEnumerable.OrderByDescending(Unordered(),static v => v).ToList();
        var actual=Orderby_Descending_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_Descending_Actual() {
        return (from v in Unordered() orderby v descending select v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_projection_select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.OrderBy(Unordered(),static v => v),static v => v*10).ToList();
        var actual=Orderby_projection_select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_projection_select_Actual() {
        return (from v in Unordered() orderby v select v*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Orderby_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.OrderBy(SystemEnumerable.Where(Unordered(),static v => v>1),static v => v),
            static v => v*10).ToList();
        var actual=Where_Orderby_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_Orderby_Select_Actual() {
        return (from v in Unordered() where v>1 orderby v select v*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(SystemEnumerable.OrderBy(Unordered(),static v => v),static v => v>1),
            static v => v*10).ToList();
        var actual=Orderby_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Orderby_Where_Select_Actual() {
        return (from v in Unordered() orderby v where v>1 select v*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_ThenBy() {
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Rows(),static r => r.Key),static r => r.Value).ToList();
        var actual=Orderby_ThenBy_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<Row> Orderby_ThenBy_Actual() {
        return (from r in Rows() orderby r.Key, r.Value select r).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Orderby_ThenByDescending() {
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Rows(),static r => r.Key),static r => r.Value).ToList();
        var actual=Orderby_ThenByDescending_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<Row> Orderby_ThenByDescending_Actual() {
        return (from r in Rows() orderby r.Key, r.Value descending select r).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void OrderbyDescending_ThenBy() {
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderByDescending(Rows(),static r => r.Key),static r => r.Value).ToList();
        var actual=OrderbyDescending_ThenBy_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<Row> OrderbyDescending_ThenBy_Actual() {
        return (from r in Rows() orderby r.Key descending, r.Value select r).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void OrderbyDescending_ThenByDescending() {
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderByDescending(Rows(),static r => r.Key),static r => r.Value).ToList();
        var actual=OrderbyDescending_ThenByDescending_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<Row> OrderbyDescending_ThenByDescending_Actual() {
        return (from r in Rows() orderby r.Key descending, r.Value descending select r).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Orderby_ThenBy_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.ThenBy(
                SystemEnumerable.OrderBy(SystemEnumerable.Where(Rows(),static r => r.Value>=20),static r => r.Key),
                static r => r.Name),
            static r => r.Name).ToList();
        var actual=Where_Orderby_ThenBy_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<string> Where_Orderby_ThenBy_Select_Actual() {
        return (from r in Rows() where r.Value>=20 orderby r.Key, r.Name select r.Name).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_into_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static v => v*2),static doubled => doubled>4),
            static doubled => doubled+1).ToList();
        var actual=Select_into_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Select_into_Where_Select_Actual() {
        return (from v in Source() select v*2 into doubled where doubled>4 select doubled+1).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Select_into_Orderby_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.OrderBy(
                SystemEnumerable.Select(SystemEnumerable.Where(Source(),static v => v>1),static v => v*10),
                static scaled => scaled),
            static scaled => scaled+1).ToList();
        var actual=Where_Select_into_Orderby_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_Select_into_Orderby_Select_Actual() {
        return (from v in Source() where v>1 select v*10 into scaled orderby scaled select scaled+1).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void MultipleFrom_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(
                SystemEnumerable.SelectMany(Source(),static v => Second(),static (v,w) => (v: v,w: w)),
                static t => t.v+t.w>5),
            static t => t.v*t.w).ToList();
        var actual=MultipleFrom_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> MultipleFrom_Where_Select_Actual() {
        return (from v in Source() from w in Second() where v+w>5 select v*w).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void MultipleFrom_Item1_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(
                SystemEnumerable.SelectMany(Source(),static v => Second(),static (v,Item1) => (v: v,(Item1))),
                static t => t.Item2>3),
            static t => t.v+t.Item2).ToList();
        var actual=MultipleFrom_Item1_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> MultipleFrom_Item1_Where_Select_Actual() {
        return (from v in Source() from Item1 in Second() where Item1>3 select v+Item1).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Join_Where_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.Where(
                SystemEnumerable.Join(Source(),Second(),static v => v,static w => w,static (v,w) => (v: v,w: w)),
                static t => t.v+t.w>6),
            static t => t.v*t.w).ToList();
        var actual=Join_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Join_Where_Select_Actual() {
        return (from v in Source()
                join w in Second() on v equals w
                where v+w>6
                select v*w).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void GroupJoin_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.GroupJoin(Source(),Second(),static v => v,static w => w,static (v,matched) => (v: v,matched: matched)),
            static t => t.v*10+t.matched.Count()).ToList();
        var actual=GroupJoin_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupJoin_Select_Actual() {
        return (from v in Source()
                join w in Second() on v equals w into matched
                select v*10+matched.Count()).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.OrderBy(SystemEnumerable.GroupBy(Source(),static v => v%2,static v => v),static g => g.Key),
            static g => g.Sum()).ToList();
        var actual=GroupBy_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupBy_Select_Actual() {
        return (from v in Source()
                group v by v%2)
            .OrderBy(g => g.Key)
            .Select(g => g.Sum())
            .ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_Into_Orderby_Select() {
        var expected=SystemEnumerable.Select(
            SystemEnumerable.OrderBy(SystemEnumerable.GroupBy(Source(),static v => v%2,static v => v),static g => g.Key),
            static g => g.Sum()).ToList();
        var actual=GroupBy_Into_Orderby_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupBy_Into_Orderby_Select_Actual() {
        return (from v in Source()
                group v by v%2 into g
                orderby g.Key
                select g.Sum()).ToList();
    }

    private static readonly int[] SourceValue=[1,2,3,4];
    private static int[] Source() => SourceValue;

    private static readonly int[] SecondValue=[3,4,5];
    private static int[] Second() => SecondValue;

    private static readonly int[] UnorderedValue=[3,1,4,2];
    private static int[] Unordered() => UnorderedValue;

    public sealed record Row(int Key,int Value,string Name);
    private static readonly Row[] RowsValue=[new(2,30,"delta"),new(1,10,"beta"),new(2,20,"alpha"),new(1,30,"gamma")];
    private static Row[] Rows() => RowsValue;
}
