using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Concat:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() {
        var expected=Source().Concat(Second()).ToList();
        secondOptimized(expected);
        secondNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
