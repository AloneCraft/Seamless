using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class OfType:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=CastObjects().OfType<int>().ToList();
        NoDelegateOptimized(expected);
        NoDelegateNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        CastObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        CastObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void mixedTypes() {
        var expected=MixedObjects().OfType<int>().ToList();
        mixedTypesOptimized(expected);
        mixedTypesNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void mixedTypesOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        MixedObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void mixedTypesNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        MixedObjects().OfType<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
