using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class ElementAt:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void index() {
        var expected=Source().ElementAt(2);
        indexOptimized(expected);
        indexNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexOptimized(dynamic expected) {
        var actual=Source().ElementAt(2);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexNoOptimized(dynamic expected) {
        var actual=Source().ElementAt(2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void indexFromEnd() {
        var expected=Source().ElementAt(^1);
        indexFromEndOptimized(expected);
        indexFromEndNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexFromEndOptimized(dynamic expected) {
        var actual=Source().ElementAt(^1);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void indexFromEndNoOptimized(dynamic expected) {
        var actual=Source().ElementAt(^1);
        Assert.Equal(expected,actual);
    }

}
