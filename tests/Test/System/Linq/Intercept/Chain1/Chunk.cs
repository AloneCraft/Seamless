using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Chunk(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void size() {
        var expected=Source().Chunk(2).ToList();
        MeasureOptimized(nameof(sizeOptimized),()=>sizeOptimized(expected));
        MeasureOptimized(nameof(sizeNoOptimized),()=>sizeNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void sizeOptimized(dynamic expected) {
        var actual=new Consumer<int[]>();
        Source().Chunk(2).Consume(actual);
        AssertNestedSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void sizeNoOptimized(dynamic expected) {
        var actual=new Consumer<int[]>();
        Source().Chunk(2).Consume(actual);
        AssertNestedSequenceEqual(expected,actual.Items);
    }

}
