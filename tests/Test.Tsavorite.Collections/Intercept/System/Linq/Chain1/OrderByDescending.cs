using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class OrderByDescending:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() {
        var expected=Rows().OrderByDescending(static row => row.Key).ToList();
        keySelector_StaticOptimized(expected);
        keySelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Query() {
        var expected=Rows().OrderByDescending(row => row.Key).ToList();
        keySelector_QueryOptimized(expected);
        keySelector_QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        (from row in Rows() orderby row.Key descending select row).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        (from row in Rows() orderby row.Key descending select row).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() {
        var expected=Rows().OrderByDescending(row => row.Key).ToList();
        keySelector_LambdaOptimized(expected);
        keySelector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() {
        var offset=0;
        var expected=Rows().OrderByDescending(row => row.Key+offset).ToList();
        keySelector_CapturedOptimized(expected);
        keySelector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() {
        var expected=Rows().OrderByDescending(delegate(Chain1Row row) { return row.Key; }).ToList();
        keySelector_DelegateSyntaxOptimized(expected);
        keySelector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=Rows().OrderByDescending(keySelector).ToList();
        keySelector_VariableOptimized(expected);
        keySelector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() {
        var expected=Rows().OrderByDescending(static row => row.Key,Comparer<int>.Default).ToList();
        keySelector_comparerOptimized(expected);
        keySelector_comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(static row => row.Key,Comparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderByDescending(static row => row.Key,Comparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
