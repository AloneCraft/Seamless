using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class LongCount(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=Source().LongCount();
        MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=Source().LongCount();
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=Source().LongCount();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected=Source().LongCount(static v => v>2);
        MeasureOptimized(nameof(predicate_StaticOptimized),()=>predicate_StaticOptimized(expected));
        MeasureOptimized(nameof(predicate_StaticNoOptimized),()=>predicate_StaticNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_StaticOptimized(dynamic expected) {
        var actual=Source().LongCount(static v => v>2);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_StaticNoOptimized(dynamic expected) {
        var actual=Source().LongCount(static v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Lambda() {
        var expected=Source().LongCount(v => v>2);
        MeasureOptimized(nameof(predicate_LambdaOptimized),()=>predicate_LambdaOptimized(expected));
        MeasureOptimized(nameof(predicate_LambdaNoOptimized),()=>predicate_LambdaNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_LambdaOptimized(dynamic expected) {
        var actual=Source().LongCount(v => v>2);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_LambdaNoOptimized(dynamic expected) {
        var actual=Source().LongCount(v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Captured() {
        var threshold=2;
        var expected=Source().LongCount(v => v>threshold);
        MeasureOptimized(nameof(predicate_CapturedOptimized),()=>predicate_CapturedOptimized(expected));
        MeasureOptimized(nameof(predicate_CapturedNoOptimized),()=>predicate_CapturedNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_CapturedOptimized(dynamic expected) {
        var threshold=2;
        var actual=Source().LongCount(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_CapturedNoOptimized(dynamic expected) {
        var threshold=2;
        var actual=Source().LongCount(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_DelegateSyntax() {
        var expected=Source().LongCount(delegate(int v) { return v>2; });
        MeasureOptimized(nameof(predicate_DelegateSyntaxOptimized),()=>predicate_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(predicate_DelegateSyntaxNoOptimized),()=>predicate_DelegateSyntaxNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxOptimized(dynamic expected) {
        var actual=Source().LongCount(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=Source().LongCount(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=Source().LongCount(predicate);
        MeasureOptimized(nameof(predicate_VariableOptimized),()=>predicate_VariableOptimized(expected));
        MeasureOptimized(nameof(predicate_VariableNoOptimized),()=>predicate_VariableNoOptimized(expected));
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_VariableOptimized(dynamic expected) {
        Func<int,bool> predicate=v => v>2;
        var actual=Source().LongCount(predicate);
        Assert.Equal(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_VariableNoOptimized(dynamic expected) {
        Func<int,bool> predicate=v => v>2;
        var actual=Source().LongCount(predicate);
        Assert.Equal(expected,actual);
    }

}
