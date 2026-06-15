using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Max:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void int_NoDelegate() {
        var expected=Source().Max();
        int_NoDelegateOptimized(expected);
        int_NoDelegateNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void int_NoDelegateOptimized(dynamic expected) {
        var actual=Source().Max();
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void int_NoDelegateNoOptimized(dynamic expected) {
        var actual=Source().Max();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Nested() {
        var expected=Source().Max<int,int>(p => Source().Max(int (q) => q+p));
        selector_NestedOptimized(expected);
        selector_NestedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_NestedOptimized(dynamic expected) {
        var actual=Source().Max(p => Source().Max(q => q+p));
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_NestedNoOptimized(dynamic expected) {
        var actual=Source().Max(p => Source().Max(q => q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Static() {
        var expected=Rows().Max(static row => row.Value);
        selector_StaticOptimized(expected);
        selector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_StaticOptimized(dynamic expected) {
        var actual=Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_StaticNoOptimized(dynamic expected) {
        var actual=Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() {
        var expected=Rows().Max(row => row.Value);
        selector_LambdaOptimized(expected);
        selector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_LambdaOptimized(dynamic expected) {
        var actual=Rows().Max(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_LambdaNoOptimized(dynamic expected) {
        var actual=Rows().Max(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() {
        var offset=0;
        var expected=Rows().Max(row => row.Value+offset);
        selector_CapturedOptimized(expected);
        selector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=Rows().Max(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=Rows().Max(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() {
        var expected=Rows().Max(delegate(Chain1Row row) { return row.Value; });
        selector_DelegateSyntaxOptimized(expected);
        selector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=Rows().Max(delegate(Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=Rows().Max(delegate(Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() {
        Func<Chain1Row,int> selector=row => row.Value;
        var expected=Rows().Max(selector);
        selector_VariableOptimized(expected);
        selector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> selector=row => row.Value;
        var actual=Rows().Max(selector);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void selector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> selector=row => row.Value;
        var actual=Rows().Max(selector);
        Assert.Equal(expected,actual);
    }

}
