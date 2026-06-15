using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Union:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() {
        var expected=Source().Union(Second()).ToList();
        secondOptimized(expected);
        secondNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Union(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Union(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_comparer() {
        var expected=Source().Union(Second(),EqualityComparer<int>.Default).ToList();
        second_comparerOptimized(expected);
        second_comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_comparerOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Union(Second(),EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_comparerNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Union(Second(),EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
