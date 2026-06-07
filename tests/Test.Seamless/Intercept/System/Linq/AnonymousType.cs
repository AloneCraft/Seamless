using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;
public sealed class AnonymousType {
    [Fact,Trait("Category","Unit")]public void SelectMany() {
        SelectMany_Static();
    }
    private static void SelectMany_Static() {
        var expected=SystemEnumerable.SelectMany(Source(),p=>Source().Select(q=>new{p,q}));
        var actual=Source().SelectMany(p=>Source().Select(q=>new{p,q}));
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void GroupJoin() {
        GroupJoin_Static();
    }
    private static void GroupJoin_Static() {
        var expected=SystemEnumerable.GroupJoin(Source(),Source(),o=>(o,o),i=>(i,i),(o,i)=>new{o,i});
        var actual=Source().GroupJoin(Source(),o=>(o,o),i=>(i,i),(o,i)=>new{o,i});
        //Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Join() {
        Join_Static();
    }
    private static void Join_Static() {
        var expected=SystemEnumerable.Join(Source(),Source(),o=>(o,o),i=>(i,i),(o,i)=>new{o,i});
        var actual=Source().Join(Source(),o=>(o,o),i=>(i,i),(o,i)=>new{o,i});
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void GroupBy() {
        GroupBy_Static();
        GroupBy_Lambda();
        GroupBy_Captured();
        GroupBy_Delegate();
        GroupBy_Variable();
    }
    private static void GroupBy_Static() {
        var expected=SystemEnumerable.GroupBy(Source(),p=>p/2,static (l,r) =>new{l,r});
        var actual=Source().GroupBy(p=>p/2,static (l,r) =>new{l,r});
        Assert.Equal(
            expected.Select(static item => $"{item.l}:{string.Join(",",item.r)}"),
            actual.Select(static item => $"{item.l}:{string.Join(",",item.r)}"));
    }
    private static void GroupBy_Lambda() {
        var expected=SystemEnumerable.GroupBy(Source(),(p)=>p/2,(l,r) =>new{l,r});
        var actual=Source().GroupBy(static(p)=>p/2,(l,r) =>new{l,r});
        Assert.Equal(
            expected.Select(static item => $"{item.l}:{string.Join(",",item.r)}"),
            actual.Select(static item => $"{item.l}:{string.Join(",",item.r)}"));
    }
    private static void GroupBy_Captured() {
        var x=2;
        var expected=SystemEnumerable.GroupBy(Source(),static(p)=>p/2,(l,r) =>new{l,r,x});
        var actual=Source().GroupBy(static(p)=>p/2,(l,r) =>new{l,r,x});
        Assert.Equal(
            expected.Select(static item => $"{item.l}:{string.Join(",",item.r)}"),
            actual.Select(static item => $"{item.l}:{string.Join(",",item.r)}"));
    }
    private static void GroupBy_Delegate() {
        var expected=SystemEnumerable.GroupBy(Source(),static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return new{l,r};});
        var actual=Source().GroupBy(static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return new{l,r};});
        Assert.Equal(
            expected.Select(static item => $"{item.l}:{string.Join(",",item.r)}"),
            actual.Select(static item => $"{item.l}:{string.Join(",",item.r)}"));
    }
    private static void GroupBy_Variable() {
        var x=2;
        var keySelector=static (int p) => p/2;
        var elementSelector=(int l,IEnumerable<int>r)=>new{l,r,x };
        var expected=SystemEnumerable.GroupBy(Source(),keySelector,elementSelector);
        var actual=Source().GroupBy(keySelector,elementSelector);
        Assert.Equal(expected.Select(static item => $"{item.l}:{string.Join(",",item.r)}"),
            actual.Select(static item => $"{item.l}:{string.Join(",",item.r)}"));
    }
    [Fact,Trait("Category","Unit")]
    public void Select() {
        Select_Static();
        Select_Lambda();
        Select_Captured();
        Select_Delegate();
        Select_Variable();
    }
    private static void Select_Static() {
        var expected=SystemEnumerable.Select(Source(),static (l,r) =>new{l,r});
        var actual=Source().Select(static (l,r) =>new{l,r});
        Assert.Equal(expected,actual);
    }
    private static void Select_Lambda() {
        var expected=SystemEnumerable.Select(Source(),(l,r) =>new{l,r});
        var actual=Source().Select((l,r) =>new{l,r});
        Assert.Equal(expected,actual);
    }
    private static void Select_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Select(Source(),(l,r) =>new{l,r,offset});
        var actual=Source().Select((l,r) =>new{l,r,offset});
        Assert.Equal(expected,actual);
    }
    private static void Select_Delegate() {
        var expected=SystemEnumerable.Select(Source(),delegate(int l,int r) { return l+r; });
        var actual=Source().Select(delegate(int l,int r) { return l+r; });
        Assert.Equal(expected,actual);
    }
    private static void Select_Variable() {
        Func<int,int,int> func=(l,r) => l+r;
        var expected=SystemEnumerable.Select(Source(),func);
        var actual=Source().Select(func);
        Assert.Equal(expected,actual);
    }

    // 匿名型配列をソースにして各 LINQ メソッドを対象化する。匿名型はメソッド戻り値に
    // できないため各テスト内でインライン生成する。actual はインスタンス形（intercept）、
    // expected は完全修飾 SystemEnumerable（参照）。
    [Fact,Trait("Category","Unit")]public void Where() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Where(rows,r=>r.Value>15).ToList();
        var actual=rows.Where(r=>r.Value>15).ToList();
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Take() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Take(rows,2).ToList();
        var actual=rows.Take(2).ToList();
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Skip() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Skip(rows,1).ToList();
        var actual=rows.Skip(1).ToList();
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Reverse() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Reverse(rows).ToList();
        var actual=rows.Reverse().ToList();
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Distinct() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Distinct(rows).ToList();
        var actual=rows.Distinct().ToList();
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void OrderBy() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.OrderBy(rows,r=>r.Value).ToList();
        var actual=rows.OrderBy(r=>r.Value).ToList();
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Concat() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Concat(rows,rows).ToList();
        var actual=rows.Concat(rows).ToList();
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Count() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Count(rows,r=>r.Key==2);
        var actual=rows.Count(r=>r.Key==2);
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Any() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.Any(rows,r=>r.Key==3);
        var actual=rows.Any(r=>r.Key==3);
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void All() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.All(rows,r=>r.Value>0);
        var actual=rows.All(r=>r.Value>0);
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void First() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.First(rows,r=>r.Key==2);
        var actual=rows.First(r=>r.Key==2);
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void ToArray() {
        var rows=new[]{new{Key=1,Value=10},new{Key=2,Value=20},new{Key=2,Value=30},new{Key=3,Value=40}};
        var expected=SystemEnumerable.ToArray(rows);
        var actual=rows.ToArray();
        Assert.Equal(expected,actual);
    }

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);
}
