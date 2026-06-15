using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Chunk:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void size() {
        var expected=Source().Chunk(2).ToList();
        sizeOptimized(expected);
        sizeNoOptimized(expected);
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
