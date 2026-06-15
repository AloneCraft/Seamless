using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class CountBy_Select(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Rows().CountBy(static row => row.Key).Select(static pair => pair.Key+pair.Value).ToList();
        MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().CountBy(static row => row.Key).Select(static pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().CountBy(static row => row.Key).Select(static pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void Lambda() {
        var expected=Rows().CountBy(row => row.Key).Select(pair => pair.Key+pair.Value).ToList();
        MeasureOptimized(nameof(LambdaOptimized),()=>LambdaOptimized(expected));
        MeasureOptimized(nameof(LambdaNoOptimized),()=>LambdaNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().CountBy(row => row.Key).Select(pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().CountBy(row => row.Key).Select(pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
