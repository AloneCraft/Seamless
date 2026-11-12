namespace Test.System.Linq.Intercept.Chain1;

public sealed class ThenByDescending(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelector_Static() {
        var expected = Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name).ToList();
        this.MeasureOptimized(nameof(keySelector_StaticOptimized),() => keySelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_StaticNoOptimized),() => keySelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Query() {
        var expected = Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name).ToList();
        this.MeasureOptimized(nameof(keySelector_QueryOptimized),() => keySelector_QueryOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_QueryNoOptimized),() => keySelector_QueryNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_QueryOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        (from row in Rows() orderby row.Key, row.Name descending select row).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_QueryNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        (from row in Rows() orderby row.Key, row.Name descending select row).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Lambda() {
        var expected = Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name).ToList();
        this.MeasureOptimized(nameof(keySelector_LambdaOptimized),() => keySelector_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_LambdaNoOptimized),() => keySelector_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Captured() {
        var suffix = "";
        var expected = Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name+suffix).ToList();
        this.MeasureOptimized(nameof(keySelector_CapturedOptimized),() => keySelector_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_CapturedNoOptimized),() => keySelector_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var suffix = "";
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name+suffix).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var suffix = "";
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name+suffix).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_DelegateSyntax() {
        var expected = Rows().OrderBy(row => row.Key).ThenByDescending(delegate (Chain1Row row) { return row.Name; }).ToList();
        this.MeasureOptimized(nameof(keySelector_DelegateSyntaxOptimized),() => keySelector_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_DelegateSyntaxNoOptimized),() => keySelector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(delegate (Chain1Row row) { return row.Name; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(delegate (Chain1Row row) { return row.Name; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Variable() {
        Func<Chain1Row,string> keySelector = row => row.Name;
        var expected = Rows().OrderBy(row => row.Key).ThenByDescending(keySelector).ToList();
        this.MeasureOptimized(nameof(keySelector_VariableOptimized),() => keySelector_VariableOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_VariableNoOptimized),() => keySelector_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,string> keySelector = row => row.Name;
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,string> keySelector = row => row.Name;
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenByDescending(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_comparer() {
        var expected = Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name,StringComparer.Ordinal).ToList();
        this.MeasureOptimized(nameof(keySelector_comparerOptimized),() => keySelector_comparerOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_comparerNoOptimized),() => keySelector_comparerNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_comparerOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name,StringComparer.Ordinal).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_comparerNoOptimized(dynamic expected) {
        var actual = new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name,StringComparer.Ordinal).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
