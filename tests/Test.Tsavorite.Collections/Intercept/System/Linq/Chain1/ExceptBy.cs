using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class ExceptBy(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelector_Static() {
        var expected=Rows().ExceptBy(Second(),static row => row.Key).ToList();
        MeasureOptimized(nameof(keySelector_StaticOptimized),()=>keySelector_StaticOptimized(expected));
        MeasureOptimized(nameof(keySelector_StaticNoOptimized),()=>keySelector_StaticNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=Rows().ExceptBy(Second(),static row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=Rows().ExceptBy(Second(),static row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Lambda() {
        var expected=Rows().ExceptBy(Second(),row => row.Key).ToList();
        MeasureOptimized(nameof(keySelector_LambdaOptimized),()=>keySelector_LambdaOptimized(expected));
        MeasureOptimized(nameof(keySelector_LambdaNoOptimized),()=>keySelector_LambdaNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual=Rows().ExceptBy(Second(),row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual=Rows().ExceptBy(Second(),row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Captured() {
        var offset=0;
        var expected=Rows().ExceptBy(Second(),row => row.Key+offset).ToList();
        MeasureOptimized(nameof(keySelector_CapturedOptimized),()=>keySelector_CapturedOptimized(expected));
        MeasureOptimized(nameof(keySelector_CapturedNoOptimized),()=>keySelector_CapturedNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=Rows().ExceptBy(Second(),row => row.Key+offset).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=Rows().ExceptBy(Second(),row => row.Key+offset).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_DelegateSyntax() {
        var expected=Rows().ExceptBy(Second(),delegate(Chain1Row row) { return row.Key; }).ToList();
        MeasureOptimized(nameof(keySelector_DelegateSyntaxOptimized),()=>keySelector_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(keySelector_DelegateSyntaxNoOptimized),()=>keySelector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=Rows().ExceptBy(Second(),delegate(Chain1Row row) { return row.Key; }).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=Rows().ExceptBy(Second(),delegate(Chain1Row row) { return row.Key; }).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Variable() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=Rows().ExceptBy(Second(),keySelector).ToList();
        MeasureOptimized(nameof(keySelector_VariableOptimized),()=>keySelector_VariableOptimized(expected));
        MeasureOptimized(nameof(keySelector_VariableNoOptimized),()=>keySelector_VariableNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=Rows().ExceptBy(Second(),keySelector).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=Rows().ExceptBy(Second(),keySelector).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
