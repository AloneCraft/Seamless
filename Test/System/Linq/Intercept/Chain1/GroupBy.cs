using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class GroupBy(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key).ToList();
        this.MeasureOptimized(nameof(keySelector_StaticOptimized),()=>keySelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_StaticNoOptimized),()=>keySelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(static row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(static row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void elementSelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key,static row => row.Value).ToList();
        this.MeasureOptimized(nameof(elementSelector_StaticOptimized),()=>elementSelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(elementSelector_StaticNoOptimized),()=>elementSelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void elementSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,int>>();
        Rows().GroupBy(static row => row.Key,static row => row.Value).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void elementSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,int>>();
        Rows().GroupBy(static row => row.Key,static row => row.Value).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void resultSelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).ToList();
        this.MeasureOptimized(nameof(resultSelector_StaticOptimized),()=>resultSelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(resultSelector_StaticNoOptimized),()=>resultSelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void elementSelector_resultSelector_Static() {
        var expected=Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).ToList();
        this.MeasureOptimized(nameof(elementSelector_resultSelector_StaticOptimized),()=>elementSelector_resultSelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(elementSelector_resultSelector_StaticNoOptimized),()=>elementSelector_resultSelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void elementSelector_resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void elementSelector_resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Nested() {
        var expected=Source().GroupBy(p => Source().GroupBy(q => q%p).Count()).Select(group => group.Key).ToList();
        this.MeasureOptimized(nameof(keySelector_NestedOptimized),()=>keySelector_NestedOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_NestedNoOptimized),()=>keySelector_NestedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_NestedOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupBy(p => Source().GroupBy(q => q%p).Count()).Select(group => group.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_NestedNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupBy(p => Source().GroupBy(q => q%p).Count()).Select(group => group.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Query() {
        var expected=Rows().GroupBy(row => row.Key).ToList();
        this.MeasureOptimized(nameof(keySelector_QueryOptimized),()=>keySelector_QueryOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_QueryNoOptimized),()=>keySelector_QueryNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        (from row in Rows() group row by row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        (from row in Rows() group row by row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Lambda() {
        var expected=Rows().GroupBy(row => row.Key).ToList();
        this.MeasureOptimized(nameof(keySelector_LambdaOptimized),()=>keySelector_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_LambdaNoOptimized),()=>keySelector_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Captured() {
        var offset=0;
        var expected=Rows().GroupBy(row => row.Key+offset).ToList();
        this.MeasureOptimized(nameof(keySelector_CapturedOptimized),()=>keySelector_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_CapturedNoOptimized),()=>keySelector_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key+offset).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key+offset).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_DelegateSyntax() {
        var expected=Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).ToList();
        this.MeasureOptimized(nameof(keySelector_DelegateSyntaxOptimized),()=>keySelector_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_DelegateSyntaxNoOptimized),()=>keySelector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_Variable() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=Rows().GroupBy(keySelector).ToList();
        this.MeasureOptimized(nameof(keySelector_VariableOptimized),()=>keySelector_VariableOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_VariableNoOptimized),()=>keySelector_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(keySelector).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(keySelector).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

}
