using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class DistinctBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() {
        var expected=Rows().DistinctBy(static row => row.Key).ToList();
        keySelector_StaticOptimized(expected);
        keySelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Nested() {
        var expected=Source().DistinctBy(p => Source().DistinctBy(q => q%p).Count()).ToList();
        keySelector_NestedOptimized(expected);
        keySelector_NestedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_NestedOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().DistinctBy(p => Source().DistinctBy(q => q%p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_NestedNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().DistinctBy(p => Source().DistinctBy(q => q%p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() {
        var expected=Rows().DistinctBy(row => row.Key).ToList();
        keySelector_LambdaOptimized(expected);
        keySelector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() {
        var offset=0;
        var expected=Rows().DistinctBy(row => row.Key+offset).ToList();
        keySelector_CapturedOptimized(expected);
        keySelector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() {
        var expected=Rows().DistinctBy(delegate(Chain1Row row) { return row.Key; }).ToList();
        keySelector_DelegateSyntaxOptimized(expected);
        keySelector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=Rows().DistinctBy(keySelector).ToList();
        keySelector_VariableOptimized(expected);
        keySelector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
