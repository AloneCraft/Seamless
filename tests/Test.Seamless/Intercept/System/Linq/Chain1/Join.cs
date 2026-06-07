using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Join:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Static() => keySelectors_resultSelector_StaticImpl();

    private static void keySelectors_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.Join(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Join(LinqChain1TestBase.Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Lambda() => keySelectors_resultSelector_LambdaImpl();

    private static void keySelectors_resultSelector_LambdaImpl() {
        var expected=SystemEnumerable.Join(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,innerValue) => outerValue+innerValue).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Join(LinqChain1TestBase.Second(),outerValue => outerValue,innerValue => innerValue,(outerValue,innerValue) => outerValue+innerValue).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Captured() => keySelectors_resultSelector_CapturedImpl();

    private static void keySelectors_resultSelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Join(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,innerValue) => outerValue+innerValue+offset).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Join(LinqChain1TestBase.Second(),outerValue => outerValue+offset,innerValue => innerValue+offset,(outerValue,innerValue) => outerValue+innerValue+offset).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_DelegateSyntax() => keySelectors_resultSelector_DelegateSyntaxImpl();

    private static void keySelectors_resultSelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Join(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,int innerValue) { return outerValue+innerValue; }).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Join(LinqChain1TestBase.Second(),delegate(int outerValue) { return outerValue; },delegate(int innerValue) { return innerValue; },delegate(int outerValue,int innerValue) { return outerValue+innerValue; }).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_Variable() => keySelectors_resultSelector_VariableImpl();

    private static void keySelectors_resultSelector_VariableImpl() {
        Func<int,int> outerKeySelector=outerValue => outerValue;
        Func<int,int> innerKeySelector=innerValue => innerValue;
        Func<int,int,int> resultSelector=(outerValue,innerValue) => outerValue+innerValue;
        var expected=SystemEnumerable.Join(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Join(LinqChain1TestBase.Second(),outerKeySelector,innerKeySelector,resultSelector).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

}
