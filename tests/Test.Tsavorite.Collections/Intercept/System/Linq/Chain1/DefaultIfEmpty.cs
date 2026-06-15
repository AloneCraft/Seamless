using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class DefaultIfEmpty(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=EmptySource().DefaultIfEmpty().ToList();
        MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void defaultValue() {
        var expected=EmptySource().DefaultIfEmpty(42).ToList();
        MeasureOptimized(nameof(defaultValueOptimized),()=>defaultValueOptimized(expected));
        MeasureOptimized(nameof(defaultValueNoOptimized),()=>defaultValueNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void defaultValueOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty(42).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void defaultValueNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        EmptySource().DefaultIfEmpty(42).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
