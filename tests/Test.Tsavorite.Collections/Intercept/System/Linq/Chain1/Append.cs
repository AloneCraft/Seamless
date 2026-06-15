using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Append(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void element() {
        var expected=Source().Append(5).ToList();
        MeasureOptimized(nameof(elementOptimized),()=>elementOptimized(expected));
        MeasureOptimized(nameof(elementNoOptimized),()=>elementNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Append(5).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Append(5).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
