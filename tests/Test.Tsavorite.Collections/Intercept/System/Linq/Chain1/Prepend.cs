using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Prepend:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void element() {
        var expected=Source().Prepend(0).ToList();
        elementOptimized(expected);
        elementNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Prepend(0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Prepend(0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
