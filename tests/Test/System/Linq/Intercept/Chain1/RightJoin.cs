using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class RightJoin(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact,Trait("Category","Unit")]
    public void keySelectors_resultSelector() {
        var expected=Rows().RightJoin(OtherRows(),static row => row.Key,static other => other.Key,static (row,other) => $"{row?.Name??"none"}:{other.Name}").ToList();
        MeasureOptimized(nameof(keySelectors_resultSelectorOptimized),()=>keySelectors_resultSelectorOptimized(expected));
        MeasureOptimized(nameof(keySelectors_resultSelectorNoOptimized),()=>keySelectors_resultSelectorNoOptimized(expected));
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
