namespace Test.System.Linq.Intercept.Chain1;

public sealed class DistinctBy(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelector_Static() {
        var expected = Rows().DistinctBy(static row => row.Key).ToList();
        this.MeasureOptimized(nameof(keySelector_StaticOptimized),() => keySelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_StaticNoOptimized),() => keySelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Nested() {
        var expected = Source().DistinctBy(p => Source().DistinctBy(q => q%p).Count()).ToList();
        this.MeasureOptimized(nameof(keySelector_NestedOptimized),() => keySelector_NestedOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_NestedNoOptimized),() => keySelector_NestedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_NestedOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().DistinctBy(p => Source().DistinctBy(q => q%p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_NestedNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().DistinctBy(p => Source().DistinctBy(q => q%p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Lambda() {
        var expected = Rows().DistinctBy(row => row.Key).ToList();
        this.MeasureOptimized(nameof(keySelector_LambdaOptimized),() => keySelector_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_LambdaNoOptimized),() => keySelector_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Captured() {
        var offset = 0;
        var expected = Rows().DistinctBy(row => row.Key+offset).ToList();
        this.MeasureOptimized(nameof(keySelector_CapturedOptimized),() => keySelector_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_CapturedNoOptimized),() => keySelector_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var offset = 0;
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var offset = 0;
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_DelegateSyntax() {
        var expected = Rows().DistinctBy(delegate (Chain1Row row) { return row.Key; }).ToList();
        this.MeasureOptimized(nameof(keySelector_DelegateSyntaxOptimized),() => keySelector_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_DelegateSyntaxNoOptimized),() => keySelector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(delegate (Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(delegate (Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Variable() {
        Func<Chain1Row,int> keySelector = row => row.Key;
        var expected = Rows().DistinctBy(keySelector).ToList();
        this.MeasureOptimized(nameof(keySelector_VariableOptimized),() => keySelector_VariableOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_VariableNoOptimized),() => keySelector_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector = row => row.Key;
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector = row => row.Key;
        var actual = new Consumer<Chain1Row>();
        Rows().DistinctBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
