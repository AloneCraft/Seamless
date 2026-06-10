using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class Aggregate:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void func_Static() => func_StaticImpl();

    private static void func_StaticImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),static (l,r) => l+r);
        var actual=Source().Aggregate(static (l,r) => l+r);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void func_Lambda() => func_LambdaImpl();

    private static void func_LambdaImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),(l,r) => l+r);
        var actual=Source().Aggregate((l,r) => l+r);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void func_Captured() => func_CapturedImpl();

    private static void func_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Aggregate(Source(),(l,r) => l+r+offset);
        var actual=Source().Aggregate((l,r) => l+r+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void func_DelegateSyntax() => func_DelegateSyntaxImpl();

    private static void func_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),delegate(int l,int r) { return l+r; });
        var actual=Source().Aggregate(delegate(int l,int r) { return l+r; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void func_Variable() => func_VariableImpl();

    private static void func_VariableImpl() {
        Func<int,int,int> func=(l,r) => l+r;
        var expected=SystemEnumerable.Aggregate(Source(),func);
        var actual=Source().Aggregate(func);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_Static() => seed_func_StaticImpl();

    private static void seed_func_StaticImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),0,static (sum,v) => sum+v);
        var actual=Source().Aggregate(0,static (sum,v) => sum+v);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_Lambda() => seed_func_LambdaImpl();

    private static void seed_func_LambdaImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v);
        var actual=Source().Aggregate(0,(sum,v) => sum+v);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_Captured() => seed_func_CapturedImpl();

    private static void seed_func_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v+offset);
        var actual=Source().Aggregate(0,(sum,v) => sum+v+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_DelegateSyntax() => seed_func_DelegateSyntaxImpl();

    private static void seed_func_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { return sum+v; });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { return sum+v; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_Variable() => seed_func_VariableImpl();

    private static void seed_func_VariableImpl() {
        Func<int,int,int> func=(sum,v) => sum+v;
        var expected=SystemEnumerable.Aggregate(Source(),0,func);
        var actual=Source().Aggregate(0,func);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_resultSelector_Static() => seed_func_resultSelector_StaticImpl();

    private static void seed_func_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),0,static (sum,v) => sum+v,static sum => sum*2);
        var actual=Source().Aggregate(0,static (sum,v) => sum+v,static sum => sum*2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_resultSelector_Lambda() => seed_func_resultSelector_LambdaImpl();

    private static void seed_func_resultSelector_LambdaImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v,sum => sum*2);
        var actual=Source().Aggregate(0,(sum,v) => sum+v,sum => sum*2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_resultSelector_Captured() => seed_func_resultSelector_CapturedImpl();

    private static void seed_func_resultSelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v+offset,sum => sum*2+offset);
        var actual=Source().Aggregate(0,(sum,v) => sum+v+offset,sum => sum*2+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_resultSelector_DelegateSyntax() => seed_func_resultSelector_DelegateSyntaxImpl();

    private static void seed_func_resultSelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return sum*2; });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return sum*2; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void seed_func_resultSelector_Variable() => seed_func_resultSelector_VariableImpl();

    private static void seed_func_resultSelector_VariableImpl() {
        Func<int,int,int> func=(sum,v) => sum+v;
        Func<int,int> resultSelector=sum => sum*2;
        var expected=SystemEnumerable.Aggregate(Source(),0,func,resultSelector);
        var actual=Source().Aggregate(0,func,resultSelector);
        Assert.Equal(expected,actual);
    }

}
