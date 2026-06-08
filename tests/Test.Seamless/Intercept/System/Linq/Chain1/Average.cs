using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Average:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void int_NoDelegate() => int_NoDelegateImpl();

    private static void int_NoDelegateImpl() {
        var expected=SystemEnumerable.Average(Source());
        var actual=Source().Average();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Nested() => selector_NestedImpl();

    private static void selector_NestedImpl() {
        var expected=SystemEnumerable.Average<int>(Source(),p => SystemEnumerable.Average(Source(),int (q) => q+p));
        var actual=Source().Average(p => Source().Average(q => q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Average(Rows(),static row => row.Value);
        var actual=Rows().Average(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Average(Rows(),row => row.Value);
        var actual=Rows().Average(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Average(Rows(),row => row.Value+offset);
        var actual=Rows().Average(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Average(Rows(),delegate(Chain1Row row) { return row.Value; });
        var actual=Rows().Average(delegate(Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<Chain1Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Average(Rows(),selector);
        var actual=Rows().Average(selector);
        Assert.Equal(expected,actual);
    }

}
