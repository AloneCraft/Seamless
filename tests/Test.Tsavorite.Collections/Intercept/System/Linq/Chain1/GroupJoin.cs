using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class GroupJoin:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Static() => keySelectors_resultSelector_StaticImpl();

    private static void keySelectors_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => group.Sum()+outerValue).ToList();
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => group.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Lambda() => keySelectors_resultSelector_LambdaImpl();

    private static void keySelectors_resultSelector_LambdaImpl() {
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).ToList();
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Query() => keySelectors_resultSelector_QueryImpl();

    private static void keySelectors_resultSelector_QueryImpl() {
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).ToList();
        var actual=new Consumer<int>();
        (from outerValue in Source() join innerValue in Second() on outerValue equals innerValue into grouping select grouping.Sum()+outerValue).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Captured() => keySelectors_resultSelector_CapturedImpl();

    private static void keySelectors_resultSelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,group) => group.Sum()+outerValue+offset).ToList();
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,group) => group.Sum()+outerValue+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_DelegateSyntax() => keySelectors_resultSelector_DelegateSyntaxImpl();

    private static void keySelectors_resultSelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,IEnumerable<int> group) { return group.Sum()+outerValue; }).ToList();
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,IEnumerable<int> group) { return group.Sum()+outerValue; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Variable() => keySelectors_resultSelector_VariableImpl();

    private static void keySelectors_resultSelector_VariableImpl() {
        Func<int,int> outerKeySelector=outerValue => outerValue;
        Func<int,int> innerKeySelector=innerValue => innerValue;
        Func<int,IEnumerable<int>,int> resultSelector=(outerValue,group) => group.Sum()+outerValue;
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        var actual=new Consumer<int>();
        Source().GroupJoin(Second(),outerKeySelector,innerKeySelector,resultSelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
