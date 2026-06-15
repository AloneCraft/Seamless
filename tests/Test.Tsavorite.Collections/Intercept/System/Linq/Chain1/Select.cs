using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Select:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void selector_Static() {
        var expected=Source().Select(static v => v*2).ToList();
        selector_StaticOptimized(expected);
        selector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() {
        var expected=Source().Select(v => v*2).ToList();
        selector_LambdaOptimized(expected);
        selector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() {
        var multiplier=2;
        var expected=Source().Select(v => v*multiplier).ToList();
        selector_CapturedOptimized(expected);
        selector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_CapturedOptimized(dynamic expected) {
        var multiplier=2;
        var actual=new Consumer<int>();
        Source().Select(v => v*multiplier).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_CapturedNoOptimized(dynamic expected) {
        var multiplier=2;
        var actual=new Consumer<int>();
        Source().Select(v => v*multiplier).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() {
        var expected=Source().Select(delegate(int v) { return v*2; }).ToList();
        selector_DelegateSyntaxOptimized(expected);
        selector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v) { return v*2; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v) { return v*2; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() {
        Func<int,int> selector=v => v*2;
        var expected=Source().Select(selector).ToList();
        selector_VariableOptimized(expected);
        selector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_VariableOptimized(dynamic expected) {
        Func<int,int> selector=v => v*2;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_VariableNoOptimized(dynamic expected) {
        Func<int,int> selector=v => v*2;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Nested() {
        var expected=Source().Select(p => Source().Select(q => q+p).Count()).ToList();
        selector_NestedOptimized(expected);
        selector_NestedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_NestedOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(p => Source().Select(q => q+p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_NestedNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(p => Source().Select(q => q+p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Query() {
        var expected=Source().Select(v => v*2).ToList();
        selector_QueryOptimized(expected);
        selector_QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from v in Source() select v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from v in Source() select v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Static() {
        var expected=Source().Select(static (v,i) => v+i).ToList();
        selector_index_StaticOptimized(expected);
        selector_index_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static (v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(static (v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Lambda() {
        var expected=Source().Select((v,i) => v+i).ToList();
        selector_index_LambdaOptimized(expected);
        selector_index_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Captured() {
        var offset=0;
        var expected=Source().Select((v,i) => v+i+offset).ToList();
        selector_index_CapturedOptimized(expected);
        selector_index_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_DelegateSyntax() {
        var expected=Source().Select(delegate(int v,int i) { return v+i; }).ToList();
        selector_index_DelegateSyntaxOptimized(expected);
        selector_index_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v,int i) { return v+i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Select(delegate(int v,int i) { return v+i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Variable() {
        Func<int,int,int> selector=(v,i) => v+i;
        var expected=Source().Select(selector).ToList();
        selector_index_VariableOptimized(expected);
        selector_index_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_VariableOptimized(dynamic expected) {
        Func<int,int,int> selector=(v,i) => v+i;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_index_VariableNoOptimized(dynamic expected) {
        Func<int,int,int> selector=(v,i) => v+i;
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
