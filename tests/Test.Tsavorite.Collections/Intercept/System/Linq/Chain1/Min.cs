using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Min:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void int_NoDelegate() => int_NoDelegateImpl();

    private static void int_NoDelegateImpl() {
        var expected=SystemEnumerable.Min(Source());
        var actual=Source().Min();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Min(Rows(),static row => row.Value);
        var actual=Rows().Min(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Min(Rows(),row => row.Value);
        var actual=Rows().Min(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Min(Rows(),row => row.Value+offset);
        var actual=Rows().Min(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Min(Rows(),delegate(Chain1Row row) { return row.Value; });
        var actual=Rows().Min(delegate(Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<Chain1Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Min(Rows(),selector);
        var actual=Rows().Min(selector);
        Assert.Equal(expected,actual);
    }

}
