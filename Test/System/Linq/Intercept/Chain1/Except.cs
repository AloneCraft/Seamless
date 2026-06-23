using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Except(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void second() {
        var expected=Source().Except(Second()).ToList();
        MeasureOptimized(nameof(secondOptimized),()=>secondOptimized(expected));
        MeasureOptimized(nameof(secondNoOptimized),()=>secondNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Except(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Except(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void second_comparer() {
        var expected=Source().Except(Second(),EqualityComparer<int>.Default).ToList();
        MeasureOptimized(nameof(second_comparerOptimized),()=>second_comparerOptimized(expected));
        MeasureOptimized(nameof(second_comparerNoOptimized),()=>second_comparerNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_comparerOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Except(Second(),EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void second_comparerNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Except(Second(),EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
