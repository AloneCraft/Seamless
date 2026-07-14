namespace Test.System.Linq.Intercept.Chain1;

public sealed class Distinct(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected = SourceWithDuplicates().Distinct().ToList();
        this.MeasureOptimized(nameof(NoDelegateOptimized),() => NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(NoDelegateNoOptimized),() => NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SourceWithDuplicates().Distinct().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SourceWithDuplicates().Distinct().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void comparer() {
        var expected = SourceWithDuplicates().Distinct(EqualityComparer<int>.Default).ToList();
        this.MeasureOptimized(nameof(comparerOptimized),() => comparerOptimized(expected));
        this.MeasureOptimized(nameof(comparerNoOptimized),() => comparerNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void comparerOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SourceWithDuplicates().Distinct(EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void comparerNoOptimized(dynamic expected) {
        var actual = new Consumer<int>();
        SourceWithDuplicates().Distinct(EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
