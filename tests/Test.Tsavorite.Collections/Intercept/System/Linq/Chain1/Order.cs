using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Order:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=SourceWithDuplicates().Order().ToList();
        NoDelegateOptimized(expected);
        NoDelegateNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() {
        var expected=SourceWithDuplicates().Order(Comparer<int>.Default).ToList();
        comparerOptimized(expected);
        comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void comparerOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order(Comparer<int>.Default).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void comparerNoOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Order(Comparer<int>.Default).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
