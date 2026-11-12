namespace Test.System.Linq.Intercept.Chain1;

public sealed class Concat(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void second() {
        var expected = Source().Concat(Second()).ToList();
        this.MeasureOptimized(nameof(secondOptimized),() => secondOptimized(expected));
        this.MeasureOptimized(nameof(secondNoOptimized),() => secondNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void second_BothEmpty() {
        var expected = EmptySource().Concat(EmptySource()).ToList();
        this.MeasureOptimized(nameof(second_BothEmptyOptimized),() => second_BothEmptyOptimized(expected));
        this.MeasureOptimized(nameof(second_BothEmptyNoOptimized),() => second_BothEmptyNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_BothEmptyOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        EmptySource().Concat(EmptySource()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_BothEmptyNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        EmptySource().Concat(EmptySource()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void second_FirstEmpty() {
        var expected = EmptySource().Concat(Second()).ToList();
        this.MeasureOptimized(nameof(second_FirstEmptyOptimized),() => second_FirstEmptyOptimized(expected));
        this.MeasureOptimized(nameof(second_FirstEmptyNoOptimized),() => second_FirstEmptyNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_FirstEmptyOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        EmptySource().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_FirstEmptyNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        EmptySource().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void second_SecondEmpty() {
        var expected = Source().Concat(EmptySource()).ToList();
        this.MeasureOptimized(nameof(second_SecondEmptyOptimized),() => second_SecondEmptyOptimized(expected));
        this.MeasureOptimized(nameof(second_SecondEmptyNoOptimized),() => second_SecondEmptyNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_SecondEmptyOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Concat(EmptySource()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_SecondEmptyNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        Source().Concat(EmptySource()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void second_Single() {
        var expected = SingleSource().Concat(SingleSecond()).ToList();
        this.MeasureOptimized(nameof(second_SingleOptimized),() => second_SingleOptimized(expected));
        this.MeasureOptimized(nameof(second_SingleNoOptimized),() => second_SingleNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_SingleOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SingleSource().Concat(SingleSecond()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_SingleNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SingleSource().Concat(SingleSecond()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void second_LargeN() {
        var expected = LargeSource().Concat(LargeSecond()).ToList();
        this.MeasureOptimized(nameof(second_LargeNOptimized),() => second_LargeNOptimized(expected));
        this.MeasureOptimized(nameof(second_LargeNNoOptimized),() => second_LargeNNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_LargeNOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        LargeSource().Concat(LargeSecond()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_LargeNNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        LargeSource().Concat(LargeSecond()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    private static int[] SingleSource() => [7];
    private static int[] SingleSecond() => [9];
    private static int[] LargeSource() => [.. Enumerable.Range(0,50)];
    private static int[] LargeSecond() => [.. Enumerable.Range(50,50)];

}
