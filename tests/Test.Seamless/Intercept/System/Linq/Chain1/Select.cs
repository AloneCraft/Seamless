using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Select:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),static v => v*2).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(static v => v*2).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),v => v*2).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(v => v*2).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var multiplier=2;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),v => v*multiplier).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(v => v*multiplier).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),delegate(int v) { return v*2; }).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(delegate(int v) { return v*2; }).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<int,int> selector=v => v*2;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),selector).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(selector).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Static() => selector_index_StaticImpl();

    private static void selector_index_StaticImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),static (v,i) => v+i).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(static (v,i) => v+i).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Lambda() => selector_index_LambdaImpl();

    private static void selector_index_LambdaImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),(v,i) => v+i).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select((v,i) => v+i).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Captured() => selector_index_CapturedImpl();

    private static void selector_index_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),(v,i) => v+i+offset).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select((v,i) => v+i+offset).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_DelegateSyntax() => selector_index_DelegateSyntaxImpl();

    private static void selector_index_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),delegate(int v,int i) { return v+i; }).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(delegate(int v,int i) { return v+i; }).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Variable() => selector_index_VariableImpl();

    private static void selector_index_VariableImpl() {
        Func<int,int,int> selector=(v,i) => v+i;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),selector).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Select(selector).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

}
