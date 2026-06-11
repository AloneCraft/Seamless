using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Sum:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void int_NoDelegate() => int_NoDelegateImpl();

    private static void int_NoDelegateImpl() {
        var expected=SystemEnumerable.Sum(Source());
        var actual=Source().Sum();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Nested() => selector_NestedImpl();

    private static void selector_NestedImpl() {
        var expected=SystemEnumerable.Sum<int>(Source(),p => SystemEnumerable.Sum(Source(),int (q) => q+p));
        var actual=Source().Sum(p => Source().Sum(q => q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.Sum(Rows(),static row => row.Value);
        var actual=Rows().Sum(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.Sum(Rows(),row => row.Value);
        var actual=Rows().Sum(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Sum(Rows(),row => row.Value+offset);
        var actual=Rows().Sum(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Sum(Rows(),delegate(Chain1Row row) { return row.Value; });
        var actual=Rows().Sum(delegate(Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<Chain1Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Sum(Rows(),selector);
        var actual=Rows().Sum(selector);
        Assert.Equal(expected,actual);
    }

}
