namespace Test.System.Linq.Intercept.Chain1;

public sealed class LongCount(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected = Source().LongCount();
        this.MeasureOptimized(nameof(NoDelegateOptimized),() => NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(NoDelegateNoOptimized),() => NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual = Source().LongCount();
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual = Source().LongCount();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected = Source().LongCount(static v => v>2);
        this.MeasureOptimized(nameof(predicate_StaticOptimized),() => predicate_StaticOptimized(expected));
        this.MeasureOptimized(nameof(predicate_StaticNoOptimized),() => predicate_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_StaticOptimized(dynamic expected) {
        var actual = Source().LongCount(static v => v>2);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_StaticNoOptimized(dynamic expected) {
        var actual = Source().LongCount(static v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Lambda() {
        var expected = Source().LongCount(v => v>2);
        this.MeasureOptimized(nameof(predicate_LambdaOptimized),() => predicate_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(predicate_LambdaNoOptimized),() => predicate_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_LambdaOptimized(dynamic expected) {
        var actual = Source().LongCount(v => v>2);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_LambdaNoOptimized(dynamic expected) {
        var actual = Source().LongCount(v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Captured() {
        var threshold = 2;
        var expected = Source().LongCount(v => v>threshold);
        this.MeasureOptimized(nameof(predicate_CapturedOptimized),() => predicate_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(predicate_CapturedNoOptimized),() => predicate_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_CapturedOptimized(dynamic expected) {
        var threshold = 2;
        var actual = Source().LongCount(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_CapturedNoOptimized(dynamic expected) {
        var threshold = 2;
        var actual = Source().LongCount(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_DelegateSyntax() {
        var expected = Source().LongCount(delegate (int v) { return v>2; });
        this.MeasureOptimized(nameof(predicate_DelegateSyntaxOptimized),() => predicate_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(predicate_DelegateSyntaxNoOptimized),() => predicate_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxOptimized(dynamic expected) {
        var actual = Source().LongCount(delegate (int v) { return v>2; });
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual = Source().LongCount(delegate (int v) { return v>2; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Variable() {
        Func<int,bool> predicate = v => v>2;
        var expected = Source().LongCount(predicate);
        this.MeasureOptimized(nameof(predicate_VariableOptimized),() => predicate_VariableOptimized(expected));
        this.MeasureOptimized(nameof(predicate_VariableNoOptimized),() => predicate_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_VariableOptimized(dynamic expected) {
        Func<int,bool> predicate = v => v>2;
        var actual = Source().LongCount(predicate);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_VariableNoOptimized(dynamic expected) {
        Func<int,bool> predicate = v => v>2;
        var actual = Source().LongCount(predicate);
        Assert.Equal(expected,actual);
    }

}
