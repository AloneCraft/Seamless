using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class MaxBy(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelector_Static() {
        var expected=Rows().MaxBy(static row => row.Key);
        MeasureOptimized(nameof(keySelector_StaticOptimized),()=>keySelector_StaticOptimized(expected));
        MeasureOptimized(nameof(keySelector_StaticNoOptimized),()=>keySelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=Rows().MaxBy(static row => row.Key);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=Rows().MaxBy(static row => row.Key);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Lambda() {
        var expected=Rows().MaxBy(row => row.Key);
        MeasureOptimized(nameof(keySelector_LambdaOptimized),()=>keySelector_LambdaOptimized(expected));
        MeasureOptimized(nameof(keySelector_LambdaNoOptimized),()=>keySelector_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual=Rows().MaxBy(row => row.Key);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual=Rows().MaxBy(row => row.Key);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Captured() {
        var offset=0;
        var expected=Rows().MaxBy(row => row.Key+offset);
        MeasureOptimized(nameof(keySelector_CapturedOptimized),()=>keySelector_CapturedOptimized(expected));
        MeasureOptimized(nameof(keySelector_CapturedNoOptimized),()=>keySelector_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=Rows().MaxBy(row => row.Key+offset);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=Rows().MaxBy(row => row.Key+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_DelegateSyntax() {
        var expected=Rows().MaxBy(delegate(Chain1Row row) { return row.Key; });
        MeasureOptimized(nameof(keySelector_DelegateSyntaxOptimized),()=>keySelector_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(keySelector_DelegateSyntaxNoOptimized),()=>keySelector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=Rows().MaxBy(delegate(Chain1Row row) { return row.Key; });
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=Rows().MaxBy(delegate(Chain1Row row) { return row.Key; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Variable() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=Rows().MaxBy(keySelector);
        MeasureOptimized(nameof(keySelector_VariableOptimized),()=>keySelector_VariableOptimized(expected));
        MeasureOptimized(nameof(keySelector_VariableNoOptimized),()=>keySelector_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=Rows().MaxBy(keySelector);
        Assert.Equal(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=Rows().MaxBy(keySelector);
        Assert.Equal(expected,actual);
    }

}
