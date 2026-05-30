using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class GroupJoin:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Static() => keySelectors_resultSelector_StaticImpl();

    private static void keySelectors_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupJoin(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => group.Sum()+outerValue).ToList();
        var actual=LinqChain1TestBase.Source().GroupJoin(LinqChain1TestBase.Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => group.Sum()+outerValue).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Lambda() => keySelectors_resultSelector_LambdaImpl();

    private static void keySelectors_resultSelector_LambdaImpl() {
        var expected=SystemEnumerable.GroupJoin(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).ToList();
        var actual=LinqChain1TestBase.Source().GroupJoin(LinqChain1TestBase.Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,group) => group.Sum()+outerValue).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Captured() => keySelectors_resultSelector_CapturedImpl();

    private static void keySelectors_resultSelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.GroupJoin(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,group) => group.Sum()+outerValue+offset).ToList();
        var actual=LinqChain1TestBase.Source().GroupJoin(LinqChain1TestBase.Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,group) => group.Sum()+outerValue+offset).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_DelegateSyntax() => keySelectors_resultSelector_DelegateSyntaxImpl();

    private static void keySelectors_resultSelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.GroupJoin(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,IEnumerable<int> group) { return group.Sum()+outerValue; }).ToList();
        var actual=LinqChain1TestBase.Source().GroupJoin(LinqChain1TestBase.Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,IEnumerable<int> group) { return group.Sum()+outerValue; }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Variable() => keySelectors_resultSelector_VariableImpl();

    private static void keySelectors_resultSelector_VariableImpl() {
        Func<int,int> outerKeySelector=outerValue => outerValue;
        Func<int,int> innerKeySelector=innerValue => innerValue;
        Func<int,IEnumerable<int>,int> resultSelector=(outerValue,group) => group.Sum()+outerValue;
        var expected=SystemEnumerable.GroupJoin(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        var actual=LinqChain1TestBase.Source().GroupJoin(LinqChain1TestBase.Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
