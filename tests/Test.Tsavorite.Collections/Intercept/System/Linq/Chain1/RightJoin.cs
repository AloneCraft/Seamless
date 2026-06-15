using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class RightJoin:LinqChain1TestBase {
    [Fact,Trait("Category","Unit")]
    public static void keySelectors_resultSelector() {
        var expected=Rows().RightJoin(OtherRows(),static row => row.Key,static other => other.Key,static (row,other) => $"{row?.Name??"none"}:{other.Name}").ToList();
        keySelectors_resultSelectorOptimized(expected);
        keySelectors_resultSelectorNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelectorOptimized(dynamic expected) {
        var actual=Rows()
            .RightJoin(
                OtherRows(),
                static row => row.Key,
                static other => other.Key,
                static (row,other) => $"{row?.Name??"none"}:{other.Name}")
            .ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelectorNoOptimized(dynamic expected) {
        var actual=Rows()
            .RightJoin(
                OtherRows(),
                static row => row.Key,
                static other => other.Key,
                static (row,other) => $"{row?.Name??"none"}:{other.Name}")
            .ToList();
        AssertSequenceEqual(expected,actual);
    }
}
