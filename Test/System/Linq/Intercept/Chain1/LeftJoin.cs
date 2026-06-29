namespace Test.System.Linq.Intercept.Chain1;

public sealed class LeftJoin(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact,Trait("Category","Unit")]
    public void keySelectors_resultSelector() {
        var expected=Rows().LeftJoin(OtherRows(),static row => row.Key,static other => other.Key,static (row,other) => $"{row.Name}:{other?.Name??"none"}").ToList();
        this.MeasureOptimized(nameof(keySelectors_resultSelectorOptimized),()=>keySelectors_resultSelectorOptimized(expected));
        this.MeasureOptimized(nameof(keySelectors_resultSelectorNoOptimized),()=>keySelectors_resultSelectorNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelectorOptimized(dynamic expected) {
        var actual=Rows()
            .LeftJoin(
                OtherRows(),
                static row => row.Key,
                static other => other.Key,
                static (row,other) => $"{row.Name}:{other?.Name??"none"}")
            .ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelectorNoOptimized(dynamic expected) {
        var actual=Rows()
            .LeftJoin(
                OtherRows(),
                static row => row.Key,
                static other => other.Key,
                static (row,other) => $"{row.Name}:{other?.Name??"none"}")
            .ToList();
        AssertSequenceEqual(expected,actual);
    }
}
