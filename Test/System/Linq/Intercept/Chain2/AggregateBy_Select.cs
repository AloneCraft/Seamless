using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain2;

public sealed class AggregateBy_Select(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void Static() {
        var expected=Rows().AggregateBy(static row => row.Key,0,static (total,row) => total+row.Value).Select(static pair => pair.Key+pair.Value).ToList();
        this.MeasureOptimized(nameof(StaticOptimized),()=>StaticOptimized(expected));
        this.MeasureOptimized(nameof(StaticNoOptimized),()=>StaticNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().AggregateBy(static row => row.Key,0,static (total,row) => total+row.Value).Select(static pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().AggregateBy(static row => row.Key,0,static (total,row) => total+row.Value).Select(static pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void Lambda() {
        var expected=Rows().AggregateBy(row => row.Key,0,(total,row) => total+row.Value).Select(pair => pair.Key+pair.Value).ToList();
        this.MeasureOptimized(nameof(LambdaOptimized),()=>LambdaOptimized(expected));
        this.MeasureOptimized(nameof(LambdaNoOptimized),()=>LambdaNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().AggregateBy(row => row.Key,0,(total,row) => total+row.Value).Select(pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().AggregateBy(row => row.Key,0,(total,row) => total+row.Value).Select(pair => pair.Key+pair.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
