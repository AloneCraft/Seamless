using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Select:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Select(Source(),static v => v*2).ToList();
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Select(Source(),v => v*2).ToList();
        var actual=new Consumer<int>();
        Source().Select(v => v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var multiplier=2;
        var expected=SystemEnumerable.Select(Source(),v => v*multiplier).ToList();
        var actual=new Consumer<int>();
        Source().Select(v => v*multiplier).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Select(Source(),delegate(int v) { return v*2; }).ToList();
        var actual=new Consumer<int>();
        Source().Select(delegate(int v) { return v*2; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<int,int> selector=v => v*2;
        var expected=SystemEnumerable.Select(Source(),selector).ToList();
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Nested() => selector_NestedImpl();

    private static void selector_NestedImpl() {
        var expected=SystemEnumerable.Select(Source(),p => SystemEnumerable.Count(SystemEnumerable.Select(Source(),q => q+p))).ToList();
        var actual=new Consumer<int>();
        Source().Select(p => Source().Select(q => q+p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Query() => selector_QueryImpl();

    private static void selector_QueryImpl() {
        var expected=SystemEnumerable.Select(Source(),v => v*2).ToList();
        var actual=new Consumer<int>();
        (from v in Source() select v*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Static() => selector_index_StaticImpl();

    private static void selector_index_StaticImpl() {
        var expected=SystemEnumerable.Select(Source(),static (v,i) => v+i).ToList();
        var actual=new Consumer<int>();
        Source().Select(static (v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Lambda() => selector_index_LambdaImpl();

    private static void selector_index_LambdaImpl() {
        var expected=SystemEnumerable.Select(Source(),(v,i) => v+i).ToList();
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Captured() => selector_index_CapturedImpl();

    private static void selector_index_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Select(Source(),(v,i) => v+i+offset).ToList();
        var actual=new Consumer<int>();
        Source().Select((v,i) => v+i+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_DelegateSyntax() => selector_index_DelegateSyntaxImpl();

    private static void selector_index_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Select(Source(),delegate(int v,int i) { return v+i; }).ToList();
        var actual=new Consumer<int>();
        Source().Select(delegate(int v,int i) { return v+i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Variable() => selector_index_VariableImpl();

    private static void selector_index_VariableImpl() {
        Func<int,int,int> selector=(v,i) => v+i;
        var expected=SystemEnumerable.Select(Source(),selector).ToList();
        var actual=new Consumer<int>();
        Source().Select(selector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
