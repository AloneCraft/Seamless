using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class SelectMany:LinqChain1TestBase {
    //[Fact,Trait("Category","Unit")]
    //public void 外だし先行評価Anchor() => this.外だし先行評価AnchorImpl();

    //private void 外だし先行評価AnchorImpl() {
    //    var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,this.Func() }).ToList();
    //    var actual=new Consumer<int>();
    //    Source().SelectMany(v => new[] { v,this.Func()}).Consume(actual);
    //    AssertSequenceEqual(expected,actual.Items);
    //}
    int Func() => 0;
    [Fact,Trait("Category","Unit")]
    public static void Static外だし先行評価Anchor() => Static外だし先行評価AnchorImpl();

    private static void Static外だし先行評価AnchorImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),static v => new[] { v,StaticFunc() }).ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,StaticFunc()}).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
    static int StaticFunc() => 0;
    [Fact,Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),static v => new[] { v,v+10 }).ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,v+10 }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Query() => selector_QueryImpl();

    private static void selector_QueryImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,v+10 }).ToList();
        var actual=new Consumer<int>();
        (from v in Source() from sub in new[] { v,v+10 } select sub).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_index_Static() => selector_index_StaticImpl();

    private static void selector_index_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),static (v,i) => new[] { v,i }).ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(static (v,i) => new[] { v,i }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void collectionSelector_resultSelector_Static() => collectionSelector_resultSelector_StaticImpl();

    private static void collectionSelector_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),static v => new[] { v,v+10 },static (v,sub) => sub+v)
            .ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,v+10 },static (v,sub) => sub+v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void collectionSelector_index_resultSelector_Static() =>
        collectionSelector_index_resultSelector_StaticImpl();

    private static void collectionSelector_index_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),static (v,i) => new[] { v,i },static (v,sub) => sub+v)
            .ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(static (v,i) => new[] { v,i },static (v,sub) => sub+v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,v+10 }).ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(v => new[] { v,v+10 }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var offset=10;
        var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,v+offset }).ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(v => new[] { v,v+offset }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.SelectMany(Source(),delegate(int v) { return new[] { v,v+10 }; }).ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<int,int[]> selector=v => [v,v+10];
        var expected=SystemEnumerable.SelectMany(Source(),selector).ToList();
        var actual=new Consumer<int>();
        Source().SelectMany(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
