using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Max:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void int_NoDelegate() => int_NoDelegateImpl();

    private static void int_NoDelegateImpl() {
        var expected=SystemEnumerable.Max(Source());
        var actual=Source().Max();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Nested() => selector_NestedImpl();

    private static void selector_NestedImpl() {
        var expected=SystemEnumerable.Max<int,int>(Source(),p => SystemEnumerable.Max(Source(),int (q) => q+p));
        var actual=Source().Max(p => Source().Max(q => q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Max(Rows(),static row => row.Value);
        var actual=Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Max(Rows(),row => row.Value);
        var actual=Rows().Max(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Max(Rows(),row => row.Value+offset);
        var actual=Rows().Max(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Max(Rows(),delegate(Chain1Row row) { return row.Value; });
        var actual=Rows().Max(delegate(Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<Chain1Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Max(Rows(),selector);
        var actual=Rows().Max(selector);
        Assert.Equal(expected,actual);
    }

}
