using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class ToLookup(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelector_Static() {
        var expected=Rows().ToLookup(static row => row.Key);
        MeasureOptimized(nameof(keySelector_StaticOptimized),()=>keySelector_StaticOptimized(expected));
        MeasureOptimized(nameof(keySelector_StaticNoOptimized),()=>keySelector_StaticNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key);
        AssertLookupEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key);
        AssertLookupEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_elementSelector_Static() {
        var expected=Rows().ToLookup(static row => row.Key,static row => row.Value);
        MeasureOptimized(nameof(keySelector_elementSelector_StaticOptimized),()=>keySelector_elementSelector_StaticOptimized(expected));
        MeasureOptimized(nameof(keySelector_elementSelector_StaticNoOptimized),()=>keySelector_elementSelector_StaticNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_StaticOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key,static row => row.Value);
        AssertLookupEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_StaticNoOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key,static row => row.Value);
        AssertLookupEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_comparer() {
        var expected=Rows().ToLookup(static row => row.Key,EqualityComparer<int>.Default);
        MeasureOptimized(nameof(keySelector_comparerOptimized),()=>keySelector_comparerOptimized(expected));
        MeasureOptimized(nameof(keySelector_comparerNoOptimized),()=>keySelector_comparerNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key,EqualityComparer<int>.Default);
        AssertLookupEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerNoOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key,EqualityComparer<int>.Default);
        AssertLookupEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_elementSelector_comparer() {
        var expected=Rows().ToLookup(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        MeasureOptimized(nameof(keySelector_elementSelector_comparerOptimized),()=>keySelector_elementSelector_comparerOptimized(expected));
        MeasureOptimized(nameof(keySelector_elementSelector_comparerNoOptimized),()=>keySelector_elementSelector_comparerNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_comparerOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        AssertLookupEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_comparerNoOptimized(dynamic expected) {
        var actual=Rows().ToLookup(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        AssertLookupEqual(expected,actual);
    }

}
