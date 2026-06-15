using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class SelectMany:LinqChain1TestBase {
    //[Fact,Trait("Category","Unit")]
    //public void 外だし先行評価Anchor() => this.外だし先行評価AnchorImpl();

    //private void 外だし先行評価AnchorImpl() {
    //    var expected=Source().SelectMany(v => new[] { v,this.Func() }).ToList();
    //    var actual=new Consumer<int>();
    //    Source().SelectMany(v => new[] { v,this.Func()}).Consume(actual);
    //    AssertSequenceEqual(expected,actual.Items);
    //}
    int Func() => 0;

    [Fact,Trait("Category","Unit")]
    public static void Static外だし先行評価Anchor() {
        var expected=Source().SelectMany(static v => new[] { v,StaticFunc() }).ToList();
        Static外だし先行評価AnchorOptimized(expected);
        Static外だし先行評価AnchorNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void Static外だし先行評価AnchorOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,StaticFunc()}).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void Static外だし先行評価AnchorNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,StaticFunc()}).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    static int StaticFunc() => 0;

    [Fact,Trait("Category","Unit")]
    public static void selector_Static() {
        var expected=Source().SelectMany(static v => new[] { v,v+10 }).ToList();
        selector_StaticOptimized(expected);
        selector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,v+10 }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,v+10 }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Query() {
        var expected=Source().SelectMany(v => new[] { v,v+10 }).ToList();
        selector_QueryOptimized(expected);
        selector_QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from v in Source() from sub in new[] { v,v+10 } select sub).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from v in Source() from sub in new[] { v,v+10 } select sub).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_index_Static() {
        var expected=Source().SelectMany(static (v,i) => new[] { v,i }).ToList();
        selector_index_StaticOptimized(expected);
        selector_index_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static (v,i) => new[] { v,i }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static (v,i) => new[] { v,i }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void collectionSelector_resultSelector_Static() {
        var expected=Source().SelectMany(static v => new[] { v,v+10 },static (v,sub) => sub+v)
            .ToList();
        collectionSelector_resultSelector_StaticOptimized(expected);
        collectionSelector_resultSelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void collectionSelector_resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,v+10 },static (v,sub) => sub+v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void collectionSelector_resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static v => new[] { v,v+10 },static (v,sub) => sub+v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void collectionSelector_index_resultSelector_Static() {
        var expected=Source().SelectMany(static (v,i) => new[] { v,i },static (v,sub) => sub+v)
            .ToList();
        collectionSelector_index_resultSelector_StaticOptimized(expected);
        collectionSelector_index_resultSelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void collectionSelector_index_resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static (v,i) => new[] { v,i },static (v,sub) => sub+v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void collectionSelector_index_resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(static (v,i) => new[] { v,i },static (v,sub) => sub+v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Lambda() {
        var expected=Source().SelectMany(v => new[] { v,v+10 }).ToList();
        selector_LambdaOptimized(expected);
        selector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(v => new[] { v,v+10 }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(v => new[] { v,v+10 }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Captured() {
        var offset=10;
        var expected=Source().SelectMany(v => new[] { v,v+offset }).ToList();
        selector_CapturedOptimized(expected);
        selector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_CapturedOptimized(dynamic expected) {
        var offset=10;
        var actual=new Consumer<int>();
        Source().SelectMany(v => new[] { v,v+offset }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_CapturedNoOptimized(dynamic expected) {
        var offset=10;
        var actual=new Consumer<int>();
        Source().SelectMany(v => new[] { v,v+offset }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_DelegateSyntax() {
        var expected=Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; }).ToList();
        selector_DelegateSyntaxOptimized(expected);
        selector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_Variable() {
        Func<int,int[]> selector=v => [v,v+10];
        var expected=Source().SelectMany(selector).ToList();
        selector_VariableOptimized(expected);
        selector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_VariableOptimized(dynamic expected) {
        Func<int,int[]> selector=v => [v,v+10];
        var actual=new Consumer<int>();
        Source().SelectMany(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_VariableNoOptimized(dynamic expected) {
        Func<int,int[]> selector=v => [v,v+10];
        var actual=new Consumer<int>();
        Source().SelectMany(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
