namespace Test.System.Linq.Intercept.Chain1;

public sealed class Max(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void int_NoDelegate() {
        var expected = Source().Max();
        this.MeasureOptimized(nameof(int_NoDelegateOptimized),() => int_NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(int_NoDelegateNoOptimized),() => int_NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void int_NoDelegateOptimized(dynamic expected) {
        var actual = Source().Max();
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void int_NoDelegateNoOptimized(dynamic expected) {
        var actual = Source().Max();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Nested() {
        var expected = Source().Max<int,int>(p => Source().Max(int (q) => q+p));
        this.MeasureOptimized(nameof(selector_NestedOptimized),() => selector_NestedOptimized(expected));
        this.MeasureOptimized(nameof(selector_NestedNoOptimized),() => selector_NestedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_NestedOptimized(dynamic expected) {
        var actual = Source().Max(p => Source().Max(q => q+p));
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_NestedNoOptimized(dynamic expected) {
        var actual = Source().Max(p => Source().Max(q => q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Static() {
        var expected = Rows().Max(static row => row.Value);
        this.MeasureOptimized(nameof(selector_StaticOptimized),() => selector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(selector_StaticNoOptimized),() => selector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_StaticOptimized(dynamic expected) {
        var actual = Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_StaticNoOptimized(dynamic expected) {
        var actual = Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Lambda() {
        var expected = Rows().Max(row => row.Value);
        this.MeasureOptimized(nameof(selector_LambdaOptimized),() => selector_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(selector_LambdaNoOptimized),() => selector_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_LambdaOptimized(dynamic expected) {
        var actual = Rows().Max(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_LambdaNoOptimized(dynamic expected) {
        var actual = Rows().Max(row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Captured() {
        var offset = 0;
        var expected = Rows().Max(row => row.Value+offset);
        this.MeasureOptimized(nameof(selector_CapturedOptimized),() => selector_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(selector_CapturedNoOptimized),() => selector_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_CapturedOptimized(dynamic expected) {
        var offset = 0;
        var actual = Rows().Max(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_CapturedNoOptimized(dynamic expected) {
        var offset = 0;
        var actual = Rows().Max(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_DelegateSyntax() {
        var expected = Rows().Max(delegate (Chain1Row row) { return row.Value; });
        this.MeasureOptimized(nameof(selector_DelegateSyntaxOptimized),() => selector_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(selector_DelegateSyntaxNoOptimized),() => selector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxOptimized(dynamic expected) {
        var actual = Rows().Max(delegate (Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual = Rows().Max(delegate (Chain1Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void selector_Variable() {
        Func<Chain1Row,int> selector = row => row.Value;
        var expected = Rows().Max(selector);
        this.MeasureOptimized(nameof(selector_VariableOptimized),() => selector_VariableOptimized(expected));
        this.MeasureOptimized(nameof(selector_VariableNoOptimized),() => selector_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> selector = row => row.Value;
        var actual = Rows().Max(selector);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void selector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> selector = row => row.Value;
        var actual = Rows().Max(selector);
        Assert.Equal(expected,actual);
    }

}
