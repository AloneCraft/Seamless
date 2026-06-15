using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class ToHashSet:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=Source().ToHashSet();
        NoDelegateOptimized(expected);
        NoDelegateNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=Source().ToHashSet();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=Source().ToHashSet();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() {
        var expected=Source().ToHashSet(EqualityComparer<int>.Default);
        comparerOptimized(expected);
        comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void comparerOptimized(dynamic expected) {
        var actual=Source().ToHashSet(EqualityComparer<int>.Default);
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void comparerNoOptimized(dynamic expected) {
        var actual=Source().ToHashSet(EqualityComparer<int>.Default);
        AssertSequenceEqual(expected,actual);
    }

}
