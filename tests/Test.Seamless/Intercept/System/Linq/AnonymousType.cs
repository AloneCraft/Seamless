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
        var expected=SystemEnumerable.GroupJoin(Source(),Source(),o=>o+o,i=>i*i,(o,i)=>new{o,i});
        var actual=Source().GroupJoin(Source(),o=>o+o,i=>i*i,(o,i)=>new{o,i});
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void Join() {
        Join_Static();
    }
    private static void Join_Static() {
        var expected=SystemEnumerable.Join(Source(),Source(),o=>o+o,i=>i*i,(o,i)=>new{o,i});
        var actual=Source().Join(Source(),o=>o+o,i=>i*i,(o,i)=>new{o,i});
        Assert.Equal(expected,actual);
    }
    [Fact,Trait("Category","Unit")]public void GroupBy() {
        GroupBy_Static();
    }
    private static void GroupBy_Static() {
        var expected=SystemEnumerable.GroupBy(Source(),p=>p/2,static (l,r) =>new{l,r});
        var actual=Source().GroupBy(p=>p/2,static (l,r) =>new{l,r});
        Assert.Equal(
            expected.Select(static item => $"{item.l}:{string.Join(",",item.r)}"),
            actual.Select(static item => $"{item.l}:{string.Join(",",item.r)}"));
    }
    [Fact,Trait("Category","Unit")]
    public void Select() {
        Select_Static();
        Select_Lambda();
        Select_Captured();
        Select_DelegateSyntax();
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
    private static void Select_DelegateSyntax() {
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

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);
}
