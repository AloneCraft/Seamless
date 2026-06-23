using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Select(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void selector_Static() {
        var expected=Source().Select(static v => v*2).ToList();
        MeasureOptimized(nameof(selector_StaticOptimized),()=>selector_StaticOptimized(expected));
        MeasureOptimized(nameof(selector_StaticNoOptimized),()=>selector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Lambda() {
        var expected=Source().Select(v => v*2).ToList();
        MeasureOptimized(nameof(selector_LambdaOptimized),()=>selector_LambdaOptimized(expected));
        MeasureOptimized(nameof(selector_LambdaNoOptimized),()=>selector_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Captured() {
        var multiplier=2;
        var expected=Source().Select(v => v*multiplier).ToList();
        MeasureOptimized(nameof(selector_CapturedOptimized),()=>selector_CapturedOptimized(expected));
        MeasureOptimized(nameof(selector_CapturedNoOptimized),()=>selector_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_CapturedOptimized(dynamic expected) {
        var multiplier=2;
        var actual=new Consumer<int>();
        Source().Select(v => v*multiplier).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_CapturedNoOptimized(dynamic expected) {
        var multiplier=2;
        var actual=new Consumer<int>();
        Source().Select(v => v*multiplier).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_DelegateSyntax() {
        var expected=Source().Select(delegate(int v) { return v*2; }).ToList();
        MeasureOptimized(nameof(selector_DelegateSyntaxOptimized),()=>selector_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(selector_DelegateSyntaxNoOptimized),()=>selector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v) { return v*2; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v) { return v*2; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Variable() {
        Func<int,int> selector=v => v*2;
        var expected=Source().Select(selector).ToList();
        MeasureOptimized(nameof(selector_VariableOptimized),()=>selector_VariableOptimized(expected));
        MeasureOptimized(nameof(selector_VariableNoOptimized),()=>selector_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_VariableOptimized(dynamic expected) {
        Func<int,int> selector=v => v*2;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_VariableNoOptimized(dynamic expected) {
        Func<int,int> selector=v => v*2;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Nested() {
        var expected=Source().Select(p => Source().Select(q => q+p).Count()).ToList();
        MeasureOptimized(nameof(selector_NestedOptimized),()=>selector_NestedOptimized(expected));
        MeasureOptimized(nameof(selector_NestedNoOptimized),()=>selector_NestedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_NestedOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(p => Source().Select(q => q+p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_NestedNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(p => Source().Select(q => q+p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Query() {
        var expected=Source().Select(v => v*2).ToList();
        MeasureOptimized(nameof(selector_QueryOptimized),()=>selector_QueryOptimized(expected));
        MeasureOptimized(nameof(selector_QueryNoOptimized),()=>selector_QueryNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from v in Source() select v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from v in Source() select v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_index_Static() {
        var expected=Source().Select(static (v,i) => v+i).ToList();
        MeasureOptimized(nameof(selector_index_StaticOptimized),()=>selector_index_StaticOptimized(expected));
        MeasureOptimized(nameof(selector_index_StaticNoOptimized),()=>selector_index_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static (v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static (v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_index_Lambda() {
        var expected=Source().Select((v,i) => v+i).ToList();
        MeasureOptimized(nameof(selector_index_LambdaOptimized),()=>selector_index_LambdaOptimized(expected));
        MeasureOptimized(nameof(selector_index_LambdaNoOptimized),()=>selector_index_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_index_Captured() {
        var offset=0;
        var expected=Source().Select((v,i) => v+i+offset).ToList();
        MeasureOptimized(nameof(selector_index_CapturedOptimized),()=>selector_index_CapturedOptimized(expected));
        MeasureOptimized(nameof(selector_index_CapturedNoOptimized),()=>selector_index_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_index_DelegateSyntax() {
        var expected=Source().Select(delegate(int v,int i) { return v+i; }).ToList();
        MeasureOptimized(nameof(selector_index_DelegateSyntaxOptimized),()=>selector_index_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(selector_index_DelegateSyntaxNoOptimized),()=>selector_index_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v,int i) { return v+i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v,int i) { return v+i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_index_Variable() {
        Func<int,int,int> selector=(v,i) => v+i;
        var expected=Source().Select(selector).ToList();
        MeasureOptimized(nameof(selector_index_VariableOptimized),()=>selector_index_VariableOptimized(expected));
        MeasureOptimized(nameof(selector_index_VariableNoOptimized),()=>selector_index_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_VariableOptimized(dynamic expected) {
        Func<int,int,int> selector=(v,i) => v+i;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_index_VariableNoOptimized(dynamic expected) {
        Func<int,int,int> selector=(v,i) => v+i;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
