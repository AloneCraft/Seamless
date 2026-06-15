using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class ThenBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() {
        var expected=Rows().OrderBy(static row => row.Key).ThenBy(static row => row.Name).ToList();
        keySelector_StaticOptimized(expected);
        keySelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenBy(static row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenBy(static row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Query() {
        var expected=Rows().OrderBy(row => row.Key).ThenBy(row => row.Name).ToList();
        keySelector_QueryOptimized(expected);
        keySelector_QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        (from row in Rows() orderby row.Key,row.Name select row).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        (from row in Rows() orderby row.Key,row.Name select row).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() {
        var expected=Rows().OrderBy(row => row.Key).ThenBy(row => row.Name).ToList();
        keySelector_LambdaOptimized(expected);
        keySelector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(row => row.Name).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() {
        var suffix="";
        var expected=Rows().OrderBy(row => row.Key).ThenBy(row => row.Name+suffix).ToList();
        keySelector_CapturedOptimized(expected);
        keySelector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var suffix="";
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(row => row.Name+suffix).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var suffix="";
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(row => row.Name+suffix).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() {
        var expected=Rows().OrderBy(row => row.Key).ThenBy(delegate(Chain1Row row) { return row.Name; }).ToList();
        keySelector_DelegateSyntaxOptimized(expected);
        keySelector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(delegate(Chain1Row row) { return row.Name; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(delegate(Chain1Row row) { return row.Name; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() {
        Func<Chain1Row,string> keySelector=row => row.Name;
        var expected=Rows().OrderBy(row => row.Key).ThenBy(keySelector).ToList();
        keySelector_VariableOptimized(expected);
        keySelector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,string> keySelector=row => row.Name;
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,string> keySelector=row => row.Name;
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).ThenBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() {
        var expected=Rows().OrderBy(static row => row.Key).ThenBy(static row => row.Name,StringComparer.Ordinal).ToList();
        keySelector_comparerOptimized(expected);
        keySelector_comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenBy(static row => row.Name,StringComparer.Ordinal).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerNoOptimized(dynamic expected) {
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).ThenBy(static row => row.Name,StringComparer.Ordinal).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
