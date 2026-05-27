using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Max:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void int_NoDelegate() => int_NoDelegateImpl();

    private static void int_NoDelegateImpl() {
        var expected=SystemEnumerable.Max(LinqChain1TestBase.Source());
        var actual=LinqChain1TestBase.Source().Max();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Max(LinqChain1TestBase.Rows(),static row => row.Value);
        var actual=LinqChain1TestBase.Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Max(LinqChain1TestBase.Rows(),row => row.Value);
        var actual=LinqChain1TestBase.Rows().Max(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Max(LinqChain1TestBase.Rows(),row => row.Value+offset);
        var actual=LinqChain1TestBase.Rows().Max(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Max(LinqChain1TestBase.Rows(),delegate(Chain1Row row) { return row.Value; });
        var actual=LinqChain1TestBase.Rows().Max(delegate(Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<Chain1Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Max(LinqChain1TestBase.Rows(),selector);
        var actual=LinqChain1TestBase.Rows().Max(selector);
        Assert.Equal(expected,actual);
    }

}
