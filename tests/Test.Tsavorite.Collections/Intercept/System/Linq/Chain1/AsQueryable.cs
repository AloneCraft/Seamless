using System.Linq;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class AsQueryable:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=Queryable.AsQueryable(Source());
        NoDelegateOptimized(expected);
        NoDelegateNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=Source().AsQueryable();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=Source().AsQueryable();
        AssertSequenceEqual(expected,actual);
    }

}
