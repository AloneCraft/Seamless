using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Zip:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.Zip(LinqChain1TestBase.Source(),LinqChain1TestBase.Second());
        var actual=LinqChain1TestBase.Source().Zip(LinqChain1TestBase.Second());
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_third() => second_thirdImpl();

    private static void second_thirdImpl() {
        var expected=SystemEnumerable.Zip(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),LinqChain1TestBase.EqualSource());
        var actual=LinqChain1TestBase.Source().Zip(LinqChain1TestBase.Second(),LinqChain1TestBase.EqualSource());
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Static() => second_resultSelector_StaticImpl();

    private static void second_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.Zip(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),static (a,b) => a+b);
        var actual=LinqChain1TestBase.Source().Zip(LinqChain1TestBase.Second(),static (a,b) => a+b);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Lambda() => second_resultSelector_LambdaImpl();

    private static void second_resultSelector_LambdaImpl() {
        var expected=SystemEnumerable.Zip(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),(a,b) => a+b);
        var actual=LinqChain1TestBase.Source().Zip(LinqChain1TestBase.Second(),(a,b) => a+b);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Captured() => second_resultSelector_CapturedImpl();

    private static void second_resultSelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Zip(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),(a,b) => a+b+offset);
        var actual=LinqChain1TestBase.Source().Zip(LinqChain1TestBase.Second(),(a,b) => a+b+offset);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_DelegateSyntax() => second_resultSelector_DelegateSyntaxImpl();

    private static void second_resultSelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Zip(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),delegate(int a,int b) { return a+b; });
        var actual=LinqChain1TestBase.Source().Zip(LinqChain1TestBase.Second(),delegate(int a,int b) { return a+b; });
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Variable() => second_resultSelector_VariableImpl();

    private static void second_resultSelector_VariableImpl() {
        Func<int,int,int> resultSelector=(a,b) => a+b;
        var expected=SystemEnumerable.Zip(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),resultSelector);
        var actual=LinqChain1TestBase.Source().Zip(LinqChain1TestBase.Second(),resultSelector);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
