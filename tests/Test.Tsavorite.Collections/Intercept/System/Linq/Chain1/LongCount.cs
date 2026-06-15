using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class LongCount:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=Source().LongCount();
        NoDelegateOptimized(expected);
        NoDelegateNoOptimized(expected);
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
    public static void predicate_Static() {
        var expected=Source().LongCount(static v => v>2);
        predicate_StaticOptimized(expected);
        predicate_StaticNoOptimized(expected);
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
    public static void predicate_Lambda() {
        var expected=Source().LongCount(v => v>2);
        predicate_LambdaOptimized(expected);
        predicate_LambdaNoOptimized(expected);
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
    public static void predicate_Captured() {
        var threshold=2;
        var expected=Source().LongCount(v => v>threshold);
        predicate_CapturedOptimized(expected);
        predicate_CapturedNoOptimized(expected);
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
    public static void predicate_DelegateSyntax() {
        var expected=Source().LongCount(delegate(int v) { return v>2; });
        predicate_DelegateSyntaxOptimized(expected);
        predicate_DelegateSyntaxNoOptimized(expected);
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
    public static void predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=Source().LongCount(predicate);
        predicate_VariableOptimized(expected);
        predicate_VariableNoOptimized(expected);
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
