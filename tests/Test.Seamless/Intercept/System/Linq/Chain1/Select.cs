using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Select:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),static v => v*2);
        var actual=LinqChain1TestBase.Source().Select(static v => v*2);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),v => v*2);
        var actual=LinqChain1TestBase.Source().Select(v => v*2);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var multiplier=2;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),v => v*multiplier);
        var actual=LinqChain1TestBase.Source().Select(v => v*multiplier);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),delegate(int v) { return v*2; });
        var actual=LinqChain1TestBase.Source().Select(delegate(int v) { return v*2; });
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<int,int> selector=v => v*2;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),selector);
        var actual=LinqChain1TestBase.Source().Select(selector);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Static() => selector_index_StaticImpl();

    private static void selector_index_StaticImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),static (v,i) => v+i);
        var actual=LinqChain1TestBase.Source().Select(static (v,i) => v+i);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Lambda() => selector_index_LambdaImpl();

    private static void selector_index_LambdaImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),(v,i) => v+i);
        var actual=LinqChain1TestBase.Source().Select((v,i) => v+i);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Captured() => selector_index_CapturedImpl();

    private static void selector_index_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),(v,i) => v+i+offset);
        var actual=LinqChain1TestBase.Source().Select((v,i) => v+i+offset);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_DelegateSyntax() => selector_index_DelegateSyntaxImpl();

    private static void selector_index_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),delegate(int v,int i) { return v+i; });
        var actual=LinqChain1TestBase.Source().Select(delegate(int v,int i) { return v+i; });
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Variable() => selector_index_VariableImpl();

    private static void selector_index_VariableImpl() {
        Func<int,int,int> selector=(v,i) => v+i;
        var expected=SystemEnumerable.Select(LinqChain1TestBase.Source(),selector);
        var actual=LinqChain1TestBase.Source().Select(selector);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
