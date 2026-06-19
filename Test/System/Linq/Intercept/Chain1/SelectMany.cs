using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class SelectMany(ITestOutputHelper output):LinqChain1TestBase(output) {
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
    public void Static外だし先行評価Anchor() {
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
    public void selector_Static() {
        var expected=Source().SelectMany(static v => new[] { v,v+10 }).ToList();
        this.MeasureOptimized(nameof(selector_StaticOptimized),()=>selector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(selector_StaticNoOptimized),()=>selector_StaticNoOptimized(expected));
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
    public void selector_Query() {
        var expected=Source().SelectMany(v => new[] { v,v+10 }).ToList();
        this.MeasureOptimized(nameof(selector_QueryOptimized),()=>selector_QueryOptimized(expected));
        this.MeasureOptimized(nameof(selector_QueryNoOptimized),()=>selector_QueryNoOptimized(expected));
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
    public void selector_index_Static() {
        var expected=Source().SelectMany(static (v,i) => new[] { v,i }).ToList();
        this.MeasureOptimized(nameof(selector_index_StaticOptimized),()=>selector_index_StaticOptimized(expected));
        this.MeasureOptimized(nameof(selector_index_StaticNoOptimized),()=>selector_index_StaticNoOptimized(expected));
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
    public void collectionSelector_resultSelector_Static() {
        var expected=Source().SelectMany(static v => new[] { v,v+10 },static (v,sub) => sub+v)
            .ToList();
        this.MeasureOptimized(nameof(collectionSelector_resultSelector_StaticOptimized),()=>collectionSelector_resultSelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(collectionSelector_resultSelector_StaticNoOptimized),()=>collectionSelector_resultSelector_StaticNoOptimized(expected));
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
    public void collectionSelector_index_resultSelector_Static() {
        var expected=Source().SelectMany(static (v,i) => new[] { v,i },static (v,sub) => sub+v)
            .ToList();
        this.MeasureOptimized(nameof(collectionSelector_index_resultSelector_StaticOptimized),()=>collectionSelector_index_resultSelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(collectionSelector_index_resultSelector_StaticNoOptimized),()=>collectionSelector_index_resultSelector_StaticNoOptimized(expected));
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
    public void selector_Lambda() {
        var expected=Source().SelectMany(v => new[] { v,v+10 }).ToList();
        this.MeasureOptimized(nameof(selector_LambdaOptimized),()=>selector_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(selector_LambdaNoOptimized),()=>selector_LambdaNoOptimized(expected));
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
    public void selector_Captured() {
        var offset=10;
        var expected=Source().SelectMany(v => new[] { v,v+offset }).ToList();
        this.MeasureOptimized(nameof(selector_CapturedOptimized),()=>selector_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(selector_CapturedNoOptimized),()=>selector_CapturedNoOptimized(expected));
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
    public void selector_DelegateSyntax() {
        var expected=Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; }).ToList();
        this.MeasureOptimized(nameof(selector_DelegateSyntaxOptimized),()=>selector_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(selector_DelegateSyntaxNoOptimized),()=>selector_DelegateSyntaxNoOptimized(expected));
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
    public void selector_Variable() {
        Func<int,int[]> selector=v => [v,v+10];
        var expected=Source().SelectMany(selector).ToList();
        this.MeasureOptimized(nameof(selector_VariableOptimized),()=>selector_VariableOptimized(expected));
        this.MeasureOptimized(nameof(selector_VariableNoOptimized),()=>selector_VariableNoOptimized(expected));
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
