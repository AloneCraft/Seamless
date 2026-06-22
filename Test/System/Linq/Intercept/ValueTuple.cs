using System.Linq;

using NonGenericEnumerable = global::System.Collections.IEnumerable;

namespace Test.System.Linq.Intercept;

public sealed class ValueTuple{
    [Fact,Trait("Category","Unit")]
    public void SelectMany() {
        var expected=Source().SelectMany(p=>Source().Select(q=>(p,q))).ToList();
        Assert.Equal(expected,SelectMany_StaticOptimized());
        Assert.Equal(expected,SelectMany_StaticNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int p,int q)> SelectMany_StaticOptimized() =>
        Source().SelectMany(p=>Source().Select(q=>(p,q))).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int p,int q)> SelectMany_StaticNoOptimized() =>
        Source().SelectMany(p=>Source().Select(q=>(p,q))).ToList();

    [Fact,Trait("Category","Unit")]
    public void GroupJoin() {
        var expected=FormatGroupJoin(Source().GroupJoin(Source(),o=>(o,o),i=>(i,i),(o,i)=>(o,i))).ToList();
        Assert.Equal(expected,GroupJoin_StaticOptimized());
        Assert.Equal(expected,GroupJoin_StaticNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupJoin_StaticOptimized() =>
        FormatGroupJoin(Source().GroupJoin(Source(),o=>(o,o),i=>(i,i),(o,i)=>(o,i))).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupJoin_StaticNoOptimized() =>
        FormatGroupJoin(Source().GroupJoin(Source(),o=>(o,o),i=>(i,i),(o,i)=>(o,i))).ToList();

    [Fact,Trait("Category","Unit")]
    public void Join() {
        var expected=Source().Join(Source(),o=>(o,o),i=>(i,i),(o,i)=>(o,i)).ToList();
        Assert.Equal(expected,Join_StaticOptimized());
        Assert.Equal(expected,Join_StaticNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int o,int i)> Join_StaticOptimized() =>
        Source().Join(Source(),o=>(o,o),i=>(i,i),(o,i)=>(o,i)).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int o,int i)> Join_StaticNoOptimized() =>
        Source().Join(Source(),o=>(o,o),i=>(i,i),(o,i)=>(o,i)).ToList();

    [Fact,Trait("Category","Unit")]
    public void GroupBy() {
        var x=2;
        Func<int,int> keySelector=static p => p/2;
        Func<int,IEnumerable<int>,(int l,IEnumerable<int> r,int x)> elementSelector=(l,r)=>(l,r,x);

        Assert.Equal(FormatGroupBy(Source().GroupBy(p=>p/2,static (l,r) =>(l,r))).ToList(),GroupBy_StaticOptimized());
        Assert.Equal(FormatGroupBy(Source().GroupBy(p=>p/2,static (l,r) =>(l,r))).ToList(),GroupBy_StaticNoOptimized());
        Assert.Equal(FormatGroupBy(Source().GroupBy((p)=>p/2,(l,r) =>(l,r))).ToList(),GroupBy_LambdaOptimized());
        Assert.Equal(FormatGroupBy(Source().GroupBy((p)=>p/2,(l,r) =>(l,r))).ToList(),GroupBy_LambdaNoOptimized());
        Assert.Equal(Source().GroupBy(static(p)=>p/2,(l,r) =>(l,r,x)).Select(static item => $"{item.l}:{string.Join(",",item.r)}").ToList(),GroupBy_CapturedOptimized());
        Assert.Equal(Source().GroupBy(static(p)=>p/2,(l,r) =>(l,r,x)).Select(static item => $"{item.l}:{string.Join(",",item.r)}").ToList(),GroupBy_CapturedNoOptimized());
        Assert.Equal(FormatGroupBy(Source().GroupBy(static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return (l,r);})).ToList(),GroupBy_DelegateOptimized());
        Assert.Equal(FormatGroupBy(Source().GroupBy(static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return (l,r);})).ToList(),GroupBy_DelegateNoOptimized());
        Assert.Equal(Source().GroupBy(keySelector,elementSelector).Select(static item => $"{item.l}:{string.Join(",",item.r)}").ToList(),GroupBy_VariableOptimized());
        Assert.Equal(Source().GroupBy(keySelector,elementSelector).Select(static item => $"{item.l}:{string.Join(",",item.r)}").ToList(),GroupBy_VariableNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_StaticOptimized() =>
        FormatGroupBy(Source().GroupBy(p=>p/2,static (l,r) =>(l,r))).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_StaticNoOptimized() =>
        FormatGroupBy(Source().GroupBy(p=>p/2,static (l,r) =>(l,r))).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_LambdaOptimized() =>
        FormatGroupBy(Source().GroupBy((p)=>p/2,(l,r) =>(l,r))).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_LambdaNoOptimized() =>
        FormatGroupBy(Source().GroupBy((p)=>p/2,(l,r) =>(l,r))).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_CapturedOptimized() {
        var x=2;
        return Source().GroupBy(static(p)=>p/2,(l,r) =>(l,r,x))
            .Select(static item => $"{item.l}:{string.Join(",",item.r)}")
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_CapturedNoOptimized() {
        var x=2;
        return Source().GroupBy(static(p)=>p/2,(l,r) =>(l,r,x))
            .Select(static item => $"{item.l}:{string.Join(",",item.r)}")
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_DelegateOptimized() =>
        FormatGroupBy(Source().GroupBy(static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return (l,r);})).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_DelegateNoOptimized() =>
        FormatGroupBy(Source().GroupBy(static delegate(int p){return p/2;},static delegate(int l,IEnumerable<int>r){return (l,r);})).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_VariableOptimized() {
        var x=2;
        var keySelector=static (int p) => p/2;
        var elementSelector=(int l,IEnumerable<int>r)=>(l,r,x);
        return Source().GroupBy(keySelector,elementSelector)
            .Select(static item => $"{item.l}:{string.Join(",",item.r)}")
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> GroupBy_VariableNoOptimized() {
        var x=2;
        var keySelector=static (int p) => p/2;
        var elementSelector=(int l,IEnumerable<int>r)=>(l,r,x);
        return Source().GroupBy(keySelector,elementSelector)
            .Select(static item => $"{item.l}:{string.Join(",",item.r)}")
            .ToList();
    }

    [Fact,Trait("Category","Unit")]
    public void Select() {
        var offset=0;
        Func<int,int,int> func=(l,r) => l+r;

        Assert.Equal(Source().Select(static (l,r) =>(l,r)).ToList(),Select_StaticOptimized());
        Assert.Equal(Source().Select(static (l,r) =>(l,r)).ToList(),Select_StaticNoOptimized());
        Assert.Equal(Source().Select((l,r) =>(l,r)).ToList(),Select_LambdaOptimized());
        Assert.Equal(Source().Select((l,r) =>(l,r)).ToList(),Select_LambdaNoOptimized());
        Assert.Equal(Source().Select((l,r) =>(l,r,offset)).ToList(),Select_CapturedOptimized());
        Assert.Equal(Source().Select((l,r) =>(l,r,offset)).ToList(),Select_CapturedNoOptimized());
        Assert.Equal(Source().Select(delegate(int l,int r) { return l+r; }).ToList(),Select_DelegateSyntaxOptimized());
        Assert.Equal(Source().Select(delegate(int l,int r) { return l+r; }).ToList(),Select_DelegateSyntaxNoOptimized());
        Assert.Equal(Source().Select(func).ToList(),Select_VariableOptimized());
        Assert.Equal(Source().Select(func).ToList(),Select_VariableNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int l,int r)> Select_StaticOptimized() =>
        Source().Select(static (l,r) =>(l,r)).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int l,int r)> Select_StaticNoOptimized() =>
        Source().Select(static (l,r) =>(l,r)).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int l,int r)> Select_LambdaOptimized() =>
        Source().Select((l,r) =>(l,r)).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int l,int r)> Select_LambdaNoOptimized() =>
        Source().Select((l,r) =>(l,r)).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int l,int r,int offset)> Select_CapturedOptimized() {
        var offset=0;
        return Source().Select((l,r) =>(l,r,offset)).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int l,int r,int offset)> Select_CapturedNoOptimized() {
        var offset=0;
        return Source().Select((l,r) =>(l,r,offset)).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> Select_DelegateSyntaxOptimized() =>
        Source().Select(delegate(int l,int r) { return l+r; }).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> Select_DelegateSyntaxNoOptimized() =>
        Source().Select(delegate(int l,int r) { return l+r; }).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> Select_VariableOptimized() {
        Func<int,int,int> func=(l,r) => l+r;
        return Source().Select(func).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> Select_VariableNoOptimized() {
        Func<int,int,int> func=(l,r) => l+r;
        return Source().Select(func).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public void Where() {
        var expected=Tuples().Where(r=>r.Value>15).ToList();
        Assert.Equal(expected,WhereOptimized());
        Assert.Equal(expected,WhereNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> WhereOptimized() => Tuples().Where(r=>r.Value>15).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> WhereNoOptimized() => Tuples().Where(r=>r.Value>15).ToList();

    [Fact,Trait("Category","Unit")]
    public void Take() {
        var expected=Tuples().Take(2).ToList();
        Assert.Equal(expected,TakeOptimized());
        Assert.Equal(expected,TakeNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> TakeOptimized() => Tuples().Take(2).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> TakeNoOptimized() => Tuples().Take(2).ToList();

    [Fact,Trait("Category","Unit")]
    public void Skip() {
        var expected=Tuples().Skip(1).ToList();
        Assert.Equal(expected,SkipOptimized());
        Assert.Equal(expected,SkipNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> SkipOptimized() => Tuples().Skip(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> SkipNoOptimized() => Tuples().Skip(1).ToList();

    [Fact,Trait("Category","Unit")]
    public void Reverse() {
        var expected=Tuples().Reverse().ToList();
        Assert.Equal(expected,ReverseOptimized());
        Assert.Equal(expected,ReverseNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> ReverseOptimized() => Tuples().Reverse().ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> ReverseNoOptimized() => Tuples().Reverse().ToList();

    [Fact,Trait("Category","Unit")]
    public void Distinct() {
        var expected=Tuples().Distinct().ToList();
        Assert.Equal(expected,DistinctOptimized());
        Assert.Equal(expected,DistinctNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> DistinctOptimized() => Tuples().Distinct().ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> DistinctNoOptimized() => Tuples().Distinct().ToList();

    [Fact,Trait("Category","Unit")]
    public void OrderBy() {
        var expected=Tuples().OrderBy(r=>r.Value).ToList();
        Assert.Equal(expected,OrderByOptimized());
        Assert.Equal(expected,OrderByNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> OrderByOptimized() => Tuples().OrderBy(r=>r.Value).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> OrderByNoOptimized() => Tuples().OrderBy(r=>r.Value).ToList();

    [Fact,Trait("Category","Unit")]
    public void Concat() {
        var rows=Tuples();
        var expected=rows.Concat(rows).ToList();
        Assert.Equal(expected,ConcatOptimized());
        Assert.Equal(expected,ConcatNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> ConcatOptimized() {
        var rows=Tuples();
        return rows.Concat(rows).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<(int Key,int Value)> ConcatNoOptimized() {
        var rows=Tuples();
        return rows.Concat(rows).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public void Count() {
        var expected=Tuples().Count(r=>r.Key==2);
        Assert.Equal(expected,CountOptimized());
        Assert.Equal(expected,CountNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int CountOptimized() => Tuples().Count(r=>r.Key==2);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int CountNoOptimized() => Tuples().Count(r=>r.Key==2);

    [Fact,Trait("Category","Unit")]
    public void Any() {
        var expected=Tuples().Any(r=>r.Key==3);
        Assert.Equal(expected,AnyOptimized());
        Assert.Equal(expected,AnyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool AnyOptimized() => Tuples().Any(r=>r.Key==3);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool AnyNoOptimized() => Tuples().Any(r=>r.Key==3);

    [Fact,Trait("Category","Unit")]
    public void All() {
        var expected=Tuples().All(r=>r.Value>0);
        Assert.Equal(expected,AllOptimized());
        Assert.Equal(expected,AllNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool AllOptimized() => Tuples().All(r=>r.Value>0);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool AllNoOptimized() => Tuples().All(r=>r.Value>0);

    [Fact,Trait("Category","Unit")]
    public void First() {
        var expected=Tuples().First(r=>r.Key==2);
        Assert.Equal(expected,FirstOptimized());
        Assert.Equal(expected,FirstNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static (int Key,int Value) FirstOptimized() => Tuples().First(r=>r.Key==2);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static (int Key,int Value) FirstNoOptimized() => Tuples().First(r=>r.Key==2);

    [Fact,Trait("Category","Unit")]
    public void ToArray() {
        var expected=Tuples().ToArray();
        Assert.Equal(expected,ToArrayOptimized());
        Assert.Equal(expected,ToArrayNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static (int Key,int Value)[] ToArrayOptimized() => Tuples().ToArray();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static (int Key,int Value)[] ToArrayNoOptimized() => Tuples().ToArray();

    private static IEnumerable<string> FormatGroupJoin(IEnumerable<(int o,IEnumerable<int> i)> rows) =>
        rows.Select(static row => $"{row.o}:{string.Join(",",row.i)}");

    private static IEnumerable<string> FormatGroupBy(IEnumerable<(int l,IEnumerable<int> r)> rows) =>
        rows.Select(static item => $"{item.l}:{string.Join(",",item.r)}");

    private static (int Key,int Value)[] Tuples() =>
        [(1,10),(2,20),(2,30),(3,40)];

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);
}
