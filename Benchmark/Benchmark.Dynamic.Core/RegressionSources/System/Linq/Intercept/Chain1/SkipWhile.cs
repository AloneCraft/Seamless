namespace Test.System.Linq.Intercept.Chain1;

public sealed class SkipWhile(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected = Source().SkipWhile(static v => v<3).ToList();
        this.MeasureOptimized(nameof(predicate_StaticOptimized),() => predicate_StaticOptimized(expected));
        this.MeasureOptimized(nameof(predicate_StaticNoOptimized),() => predicate_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_StaticOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(static v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_StaticNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(static v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Nested() {
        var expected = Source().SkipWhile(p => Source().SkipWhile(q => q<p).Any()).ToList();
        this.MeasureOptimized(nameof(predicate_NestedOptimized),() => predicate_NestedOptimized(expected));
        this.MeasureOptimized(nameof(predicate_NestedNoOptimized),() => predicate_NestedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_NestedOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(p => Source().SkipWhile(q => q<p).Any()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_NestedNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(p => Source().SkipWhile(q => q<p).Any()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Lambda() {
        var expected = Source().SkipWhile(v => v<3).ToList();
        this.MeasureOptimized(nameof(predicate_LambdaOptimized),() => predicate_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(predicate_LambdaNoOptimized),() => predicate_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_LambdaOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_LambdaNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Captured() {
        var threshold = 3;
        var expected = Source().SkipWhile(v => v<threshold).ToList();
        this.MeasureOptimized(nameof(predicate_CapturedOptimized),() => predicate_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(predicate_CapturedNoOptimized),() => predicate_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_CapturedOptimized(dynamic expected) {
        var threshold = 3;
        var actual = new Consumer<int>();
        Source().SkipWhile(v => v<threshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_CapturedNoOptimized(dynamic expected) {
        var threshold = 3;
        var actual = new Consumer<int>();
        Source().SkipWhile(v => v<threshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_DelegateSyntax() {
        var expected = Source().SkipWhile(delegate (int v) { return v<3; }).ToList();
        this.MeasureOptimized(nameof(predicate_DelegateSyntaxOptimized),() => predicate_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(predicate_DelegateSyntaxNoOptimized),() => predicate_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(delegate (int v) { return v<3; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(delegate (int v) { return v<3; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Variable() {
        Func<int,bool> predicate = v => v<3;
        var expected = Source().SkipWhile(predicate).ToList();
        this.MeasureOptimized(nameof(predicate_VariableOptimized),() => predicate_VariableOptimized(expected));
        this.MeasureOptimized(nameof(predicate_VariableNoOptimized),() => predicate_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_VariableOptimized(dynamic expected) {
        Func<int,bool> predicate = v => v<3;
        var actual = new Consumer<int>();
        Source().SkipWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_VariableNoOptimized(dynamic expected) {
        Func<int,bool> predicate = v => v<3;
        var actual = new Consumer<int>();
        Source().SkipWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_Static() {
        var expected = Source().SkipWhile(static (v,i) => v>i).ToList();
        this.MeasureOptimized(nameof(predicate_index_StaticOptimized),() => predicate_index_StaticOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_StaticNoOptimized),() => predicate_index_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_StaticOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(static (v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_StaticNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(static (v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_Lambda() {
        var expected = Source().SkipWhile((v,i) => v>i).ToList();
        this.MeasureOptimized(nameof(predicate_index_LambdaOptimized),() => predicate_index_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_LambdaNoOptimized),() => predicate_index_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_LambdaOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile((v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_LambdaNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile((v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_Captured() {
        var predicate = new IntIndexPredicate(0);
        var expected = Source().SkipWhile(predicate.LessThanThreshold).ToList();
        this.MeasureOptimized(nameof(predicate_index_CapturedOptimized),() => predicate_index_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_CapturedNoOptimized),() => predicate_index_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_CapturedOptimized(dynamic expected) {
        var predicate = new IntIndexPredicate(0);
        var actual = new Consumer<int>();
        Source().SkipWhile(predicate.LessThanThreshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_CapturedNoOptimized(dynamic expected) {
        var predicate = new IntIndexPredicate(0);
        var actual = new Consumer<int>();
        Source().SkipWhile(predicate.LessThanThreshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_DelegateSyntax() {
        var expected = Source().SkipWhile(delegate (int v,int i) { return v>i; }).ToList();
        this.MeasureOptimized(nameof(predicate_index_DelegateSyntaxOptimized),() => predicate_index_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_DelegateSyntaxNoOptimized),() => predicate_index_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_DelegateSyntaxOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(delegate (int v,int i) { return v>i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().SkipWhile(delegate (int v,int i) { return v>i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_Variable() {
        Func<int,int,bool> predicate = (v,i) => v>i;
        var expected = Source().SkipWhile(predicate).ToList();
        this.MeasureOptimized(nameof(predicate_index_VariableOptimized),() => predicate_index_VariableOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_VariableNoOptimized),() => predicate_index_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_VariableOptimized(dynamic expected) {
        Func<int,int,bool> predicate = (v,i) => v>i;
        var actual = new Consumer<int>();
        Source().SkipWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_VariableNoOptimized(dynamic expected) {
        Func<int,int,bool> predicate = (v,i) => v>i;
        var actual = new Consumer<int>();
        Source().SkipWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_Empty() {
        var expected = EmptySource().SkipWhile((v,i) => v>i).ToList();
        this.MeasureOptimized(nameof(predicate_index_EmptyOptimized),() => predicate_index_EmptyOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_EmptyNoOptimized),() => predicate_index_EmptyNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_EmptyOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        EmptySource().SkipWhile((v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_EmptyNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        EmptySource().SkipWhile((v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_SingleSkipAll() {
        var expected = SingleSource().SkipWhile((v,i) => v>=0).ToList();
        this.MeasureOptimized(nameof(predicate_index_SingleSkipAllOptimized),() => predicate_index_SingleSkipAllOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_SingleSkipAllNoOptimized),() => predicate_index_SingleSkipAllNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_SingleSkipAllOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SingleSource().SkipWhile((v,i) => v>=0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_SingleSkipAllNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SingleSource().SkipWhile((v,i) => v>=0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_SingleSkipNone() {
        var expected = SingleSource().SkipWhile((v,i) => v<0).ToList();
        this.MeasureOptimized(nameof(predicate_index_SingleSkipNoneOptimized),() => predicate_index_SingleSkipNoneOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_SingleSkipNoneNoOptimized),() => predicate_index_SingleSkipNoneNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_SingleSkipNoneOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SingleSource().SkipWhile((v,i) => v<0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_SingleSkipNoneNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SingleSource().SkipWhile((v,i) => v<0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_LargeN() {
        var expected = LargeSource().SkipWhile((v,i) => i<25).ToList();
        this.MeasureOptimized(nameof(predicate_index_LargeNOptimized),() => predicate_index_LargeNOptimized(expected));
        this.MeasureOptimized(nameof(predicate_index_LargeNNoOptimized),() => predicate_index_LargeNNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_LargeNOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        LargeSource().SkipWhile((v,i) => i<25).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_LargeNNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        LargeSource().SkipWhile((v,i) => i<25).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_Exception() {
        Assert.Throws<InvalidOperationException>(() => predicate_index_ExceptionOptimized());
        Assert.Throws<InvalidOperationException>(() => predicate_index_ExceptionNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_ExceptionOptimized() {
        var actual = new Consumer<int>();
        Source().SkipWhile((v,i) => i==2 ? throw new InvalidOperationException("boom") : v<3).Consume(actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void predicate_index_ExceptionNoOptimized() {
        var actual = new Consumer<int>();
        Source().SkipWhile((v,i) => i==2 ? throw new InvalidOperationException("boom") : v<3).Consume(actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_index_SideEffect() {
        var expectedCounter = 0;
        var expected = Source().SkipWhile((v,i) => { expectedCounter++; return v<i; }).ToList();
        var actualCounterOptimized = predicate_index_SideEffectOptimized(expected);
        Assert.Equal(expectedCounter,actualCounterOptimized);
        var actualCounterNoOptimized = predicate_index_SideEffectNoOptimized(expected);
        Assert.Equal(expectedCounter,actualCounterNoOptimized);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int predicate_index_SideEffectOptimized(dynamic expected) {
        var counter = 0;
        var actual = new Consumer<int>();
        Source().SkipWhile((v,i) => { counter++; return v<i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
        return counter;
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int predicate_index_SideEffectNoOptimized(dynamic expected) {
        var counter = 0;
        var actual = new Consumer<int>();
        Source().SkipWhile((v,i) => { counter++; return v<i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
        return counter;
    }

    private static int[] SingleSource() => [5];
    private static int[] LargeSource() => [.. Enumerable.Range(0,50)];

}
