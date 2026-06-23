using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class GroupJoin(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelectors_resultSelector_Static() {
        var expected=Source().GroupJoin(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => group.Sum()+outerValue).ToList();
        MeasureOptimized(nameof(keySelectors_resultSelector_StaticOptimized),()=>keySelectors_resultSelector_StaticOptimized(expected));
        MeasureOptimized(nameof(keySelectors_resultSelector_StaticNoOptimized),()=>keySelectors_resultSelector_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => group.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => group.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelectors_resultSelector_Lambda() {
        var expected=Source().GroupJoin(Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).ToList();
        MeasureOptimized(nameof(keySelectors_resultSelector_LambdaOptimized),()=>keySelectors_resultSelector_LambdaOptimized(expected));
        MeasureOptimized(nameof(keySelectors_resultSelector_LambdaNoOptimized),()=>keySelectors_resultSelector_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelectors_resultSelector_Query() {
        var expected=Source().GroupJoin(Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).ToList();
        MeasureOptimized(nameof(keySelectors_resultSelector_QueryOptimized),()=>keySelectors_resultSelector_QueryOptimized(expected));
        MeasureOptimized(nameof(keySelectors_resultSelector_QueryNoOptimized),()=>keySelectors_resultSelector_QueryNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from outerValue in Source() join innerValue in Second() on outerValue equals innerValue into grouping select grouping.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        (from outerValue in Source() join innerValue in Second() on outerValue equals innerValue into grouping select grouping.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelectors_resultSelector_Captured() {
        var offset=0;
        var expected=Source().GroupJoin(Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,group) => group.Sum()+outerValue+offset).ToList();
        MeasureOptimized(nameof(keySelectors_resultSelector_CapturedOptimized),()=>keySelectors_resultSelector_CapturedOptimized(expected));
        MeasureOptimized(nameof(keySelectors_resultSelector_CapturedNoOptimized),()=>keySelectors_resultSelector_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,group) => group.Sum()+outerValue+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,group) => group.Sum()+outerValue+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelectors_resultSelector_DelegateSyntax() {
        var expected=Source().GroupJoin(Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,IEnumerable<int> group) { return group.Sum()+outerValue; }).ToList();
        MeasureOptimized(nameof(keySelectors_resultSelector_DelegateSyntaxOptimized),()=>keySelectors_resultSelector_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(keySelectors_resultSelector_DelegateSyntaxNoOptimized),()=>keySelectors_resultSelector_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,IEnumerable<int> group) { return group.Sum()+outerValue; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,IEnumerable<int> group) { return group.Sum()+outerValue; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelectors_resultSelector_Variable() {
        Func<int,int> outerKeySelector=outerValue => outerValue;
        Func<int,int> innerKeySelector=innerValue => innerValue;
        Func<int,IEnumerable<int>,int> resultSelector=(outerValue,group) => group.Sum()+outerValue;
        var expected=Source().GroupJoin(Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        MeasureOptimized(nameof(keySelectors_resultSelector_VariableOptimized),()=>keySelectors_resultSelector_VariableOptimized(expected));
        MeasureOptimized(nameof(keySelectors_resultSelector_VariableNoOptimized),()=>keySelectors_resultSelector_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_VariableOptimized(dynamic expected) {
        Func<int,int> outerKeySelector=outerValue => outerValue;
        Func<int,int> innerKeySelector=innerValue => innerValue;
        Func<int,IEnumerable<int>,int> resultSelector=(outerValue,group) => group.Sum()+outerValue;
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerKeySelector,innerKeySelector,resultSelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelectors_resultSelector_VariableNoOptimized(dynamic expected) {
        Func<int,int> outerKeySelector=outerValue => outerValue;
        Func<int,int> innerKeySelector=innerValue => innerValue;
        Func<int,IEnumerable<int>,int> resultSelector=(outerValue,group) => group.Sum()+outerValue;
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerKeySelector,innerKeySelector,resultSelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
