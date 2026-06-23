using System.Collections.Generic;
using System.Linq;

using NonGenericEnumerable = System.Collections.IEnumerable;

namespace Test.System.Linq.Intercept;

public sealed class AnonymousType {
    [Fact,Trait("Category","Unit")]
    public void SelectMany() {
        var expected=Source().SelectMany(p=>Source().Select(q=>new{p,q}))
            .Select(static item => $"{item.p}:{item.q}")
            .ToList();
        Assert.Equal(expected,SelectMany_Static());
    }

    private static List<string> SelectMany_Static() =>
        Source().SelectMany(p=>Source().Select(q=>$"{p}:{q}")).ToList();

    [Fact,Trait("Category","Unit")]
    public void GroupJoin() {
        var expected=Source().GroupJoin(Source(),o=>(o,o),i=>(i,i),(o,i)=>new{o,i})
            .Select(static item => $"{item.o}:{string.Join(",",item.i)}")
            .ToList();
        Assert.Equal(expected,GroupJoin_Static());
    }

    private static List<string> GroupJoin_Static() =>
        Source().GroupJoin(Source(),o=>(o,o),i=>(i,i),(o,i)=>$"{o}:{string.Join(",",i)}").ToList();

    [Fact,Trait("Category","Unit")]
    public void Join() {
        var expected=Source().Join(Source(),o=>(o,o),i=>(i,i),(o,i)=>new{o,i})
            .Select(static item => $"{item.o}:{item.i}")
            .ToList();
        Assert.Equal(expected,Join_Static());
    }

    private static List<string> Join_Static() =>
        Source().Join(Source(),o=>(o,o),i=>(i,i),(o,i)=>$"{o}:{i}").ToList();

    [Fact,Trait("Category","Unit")]
    public void GroupBy() {
        var x=2;
        var keySelector=static (int p) => p/2;
        var elementSelector=(int l,IEnumerable<int>r)=>new{l,r,x};

        Assert.Equal(FormatGroupBy(Source().GroupBy(p=>p/2,static (l,r) =>new{l,r})).ToList(),GroupBy_Static());
        Assert.Equal(FormatGroupBy(Source().GroupBy((p)=>p/2,(l,r) =>new{l,r})).ToList(),GroupBy_Lambda());
        Assert.Equal(Source().GroupBy(static(p)=>p/2,(l,r) =>new{l,r,x}).Select(static item => $"{item.l}:{string.Join(",",item.r)}").ToList(),GroupBy_Captured());
        Assert.Equal(FormatGroupBy(Source().GroupBy(static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return new{l,r};})).ToList(),GroupBy_Delegate());
        Assert.Equal(Source().GroupBy(keySelector,elementSelector).Select(static item => $"{item.l}:{string.Join(",",item.r)}").ToList(),GroupBy_Variable());
        Assert.Equal(Source().Select(p=>new{p}).GroupBy(p=>p,p=>new{x=p})
            .Select(static g=>$"{g.Key.p}:{string.Join(",",g.Select(static e=>e.x.p))}").ToList(),GroupBy_Identity());
    }

    private static List<string> GroupBy_Static() =>
        Source().GroupBy(p=>p/2,static (l,r) => $"{l}:{string.Join(",",r)}").ToList();

    private static List<string> GroupBy_Lambda() =>
        Source().GroupBy((p)=>p/2,(l,r) => $"{l}:{string.Join(",",r)}").ToList();

    private static List<string> GroupBy_Captured() {
        return Source().GroupBy(static(p)=>p/2,(l,r) => $"{l}:{string.Join(",",r)}").ToList();
    }

    private static List<string> GroupBy_Delegate() =>
        Source().GroupBy(static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return $"{l}:{string.Join(",",r)}";}).ToList();

    private static List<string> GroupBy_Variable() {
        var keySelector=static (int p) => p/2;
        var elementSelector=(int l,IEnumerable<int>r)=>$"{l}:{string.Join(",",r)}";
        return Source().GroupBy(keySelector,elementSelector).ToList();
    }

    private static List<string> GroupBy_Identity() =>
        Source().GroupBy(static p=>p,static p=>p)
            .Select(static g=>$"{g.Key}:{string.Join(",",g)}")
            .ToList();

    [Fact,Trait("Category","Unit")]
    public void GenericMethod() {
        var expectedNested=Source().Select(p=>new{a=p}).Select(p=>new{p}).Select(r=>r.p.a).ToList();
        Assert.Equal(expectedNested,GenericNestedAnonymousLocal<int>(Source()).ToList());
        var expectedGroup=Source().Select(p=>new{p}).GroupBy(p=>p,p=>new{x=p})
            .Select(static g=>$"{g.Key.p}:{string.Join(",",g.Select(static e=>e.x.p))}").ToList();
        Assert.Equal(expectedGroup,GenericGroupByIdentityLocal<int>(Source()).ToList());
    }

    private static IEnumerable<int> GenericNestedAnonymousLocal<TAnonymous>(int[]source) =>
        source.Select(p=>new{a=p}).Select(p=>new{p}).Select(r=>r.p.a);

    private static IEnumerable<string> GenericGroupByIdentityLocal<TAnonymous>(int[]source) =>
        source.Select(p=>new{p}).GroupBy(p=>p,p=>new{x=p})
            .Select(g=>$"{g.Key.p}:{string.Join(",",g.Select(e=>e.x.p))}");

    [Fact,Trait("Category","Unit")]
    public void Select() {
        var offset=0;
        Func<int,int,int> func=(l,r) => l+r;

        Assert.Equal(Source().Select(static (l,r) =>new{l,r}).Select(static item => $"{item.l}:{item.r}").ToList(),Select_Static());
        Assert.Equal(Source().Select((l,r) =>new{l,r}).Select(static item => $"{item.l}:{item.r}").ToList(),Select_Lambda());
        Assert.Equal(Source().Select((l,r) =>new{l,r,offset}).Select(static item => $"{item.l}:{item.r}:{item.offset}").ToList(),Select_Captured());
        Assert.Equal(Source().Select(delegate(int l,int r) { return l+r; }).ToList(),Select_Delegate());
        Assert.Equal(Source().Select(func).ToList(),Select_Variable());
    }

    private static List<string> Select_Static() =>
        Source().Select(static (l,r) =>$"{l}:{r}").ToList();

    private static List<string> Select_Lambda() =>
        Source().Select((l,r) =>$"{l}:{r}").ToList();

    private static List<string> Select_Captured() {
        var offset=0;
        return Source().Select((l,r) =>$"{l}:{r}:{offset}").ToList();
    }

    private static List<int> Select_Delegate() =>
        Source().Select(delegate(int l,int r) { return l+r; }).ToList();

    private static List<int> Select_Variable() {
        Func<int,int,int> func=(l,r) => l+r;
        return Source().Select(func).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public void Where() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Where(r=>r.Value>15).Select(static r => $"{r.Key}:{r.Value}").ToList(),WhereActual());
    }

    private static List<string> WhereActual() =>
        RowsForActual().Where(static r=>r.Value>15).Select(static r => $"{r.Key}:{r.Value}").ToList();

    [Fact,Trait("Category","Unit")]
    public void Take() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Take(2).Select(static r => $"{r.Key}:{r.Value}").ToList(),TakeActual());
    }

    private static List<string> TakeActual() =>
        RowsForActual().Take(2).Select(static r => $"{r.Key}:{r.Value}").ToList();

    [Fact,Trait("Category","Unit")]
    public void Skip() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Skip(1).Select(static r => $"{r.Key}:{r.Value}").ToList(),SkipActual());
    }

    private static List<string> SkipActual() =>
        RowsForActual().Skip(1).Select(static r => $"{r.Key}:{r.Value}").ToList();

    [Fact,Trait("Category","Unit")]
    public void Reverse() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Reverse().Select(static r => $"{r.Key}:{r.Value}").ToList(),ReverseActual());
    }

    private static List<string> ReverseActual() =>
        RowsForActual().Reverse().Select(static r => $"{r.Key}:{r.Value}").ToList();

    [Fact,Trait("Category","Unit")]
    public void Distinct() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Distinct().Select(static r => $"{r.Key}:{r.Value}").ToList(),DistinctActual());
    }

    private static List<string> DistinctActual() =>
        RowsForActual().Distinct().Select(static r => $"{r.Key}:{r.Value}").ToList();

    [Fact,Trait("Category","Unit")]
    public void OrderBy() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.OrderBy(r=>r.Value).Select(static r => $"{r.Key}:{r.Value}").ToList(),OrderByActual());
    }

    private static List<string> OrderByActual() =>
        RowsForActual().OrderBy(static r=>r.Value).Select(static r => $"{r.Key}:{r.Value}").ToList();

    [Fact,Trait("Category","Unit")]
    public void Concat() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Concat(rows).Select(static r => $"{r.Key}:{r.Value}").ToList(),ConcatActual());
    }

    private static List<string> ConcatActual() {
        var rows=RowsForActual();
        return rows.Concat(rows).Select(static r => $"{r.Key}:{r.Value}").ToList();
    }

    [Fact,Trait("Category","Unit")]
    public void Count() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Count(r=>r.Key==2),CountActual());
    }

    private static int CountActual() =>
        RowsForActual().Count(static r=>r.Key==2);

    [Fact,Trait("Category","Unit")]
    public void Any() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.Any(r=>r.Key==3),AnyActual());
    }

    private static bool AnyActual() =>
        RowsForActual().Any(static r=>r.Key==3);

    [Fact,Trait("Category","Unit")]
    public void All() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.All(r=>r.Value>0),AllActual());
    }

    private static bool AllActual() =>
        RowsForActual().All(static r=>r.Value>0);

    [Fact,Trait("Category","Unit")]
    public void First() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=rows.First(r=>r.Key==2);
        Assert.Equal($"{expected.Key}:{expected.Value}",FirstActual());
    }

    private static string FirstActual() =>
        RowsForActual().Where(static r=>r.Key==2).Select(static r => $"{r.Key}:{r.Value}").First();

    [Fact,Trait("Category","Unit")]
    public void ToArray() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        Assert.Equal(rows.ToArray().Select(static r => $"{r.Key}:{r.Value}").ToList(),ToArrayActual());
    }

    private static List<string> ToArrayActual() =>
        RowsForActual().ToArray().Select(static r => $"{r.Key}:{r.Value}").ToList();

    private static IEnumerable<string> FormatGroupBy<T>(IEnumerable<T> rows) =>
        rows.Select(static item => {
            dynamic value=item!;
            return $"{value.l}:{string.Join(",",value.r)}";
        });

    private static AnonymousRow[] RowsForActual() =>
        [new(1,10),new(2,20),new(2,30),new(3,40)];

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);

    public readonly record struct AnonymousRow(int Key,int Value);
}
