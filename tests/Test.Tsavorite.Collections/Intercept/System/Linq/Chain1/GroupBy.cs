using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class GroupBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key).ToList();
        keySelector_StaticOptimized(expected);
        keySelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(static row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(static row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key,static row => row.Value).ToList();
        elementSelector_StaticOptimized(expected);
        elementSelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,int>>();
        Rows().GroupBy(static row => row.Key,static row => row.Value).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,int>>();
        Rows().GroupBy(static row => row.Key,static row => row.Value).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void resultSelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).ToList();
        resultSelector_StaticOptimized(expected);
        resultSelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_resultSelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).ToList();
        elementSelector_resultSelector_StaticOptimized(expected);
        elementSelector_resultSelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementSelector_resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void elementSelector_resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Nested() {
        var expected=Source().GroupBy(p => Source().GroupBy(q => q%p).Count()).Select(group => group.Key).ToList();
        keySelector_NestedOptimized(expected);
        keySelector_NestedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_NestedOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupBy(p => Source().GroupBy(q => q%p).Count()).Select(group => group.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_NestedNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupBy(p => Source().GroupBy(q => q%p).Count()).Select(group => group.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Query() {
        var expected=Rows().GroupBy(row => row.Key).ToList();
        keySelector_QueryOptimized(expected);
        keySelector_QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        (from row in Rows() group row by row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        (from row in Rows() group row by row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() {
        var expected=Rows().GroupBy(row => row.Key).ToList();
        keySelector_LambdaOptimized(expected);
        keySelector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() {
        var offset=0;
        var expected=Rows().GroupBy(row => row.Key+offset).ToList();
        keySelector_CapturedOptimized(expected);
        keySelector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key+offset).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key+offset).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() {
        var expected=Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).ToList();
        keySelector_DelegateSyntaxOptimized(expected);
        keySelector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=Rows().GroupBy(keySelector).ToList();
        keySelector_VariableOptimized(expected);
        keySelector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(keySelector).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(keySelector).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

}
